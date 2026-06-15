using UnityEngine;

namespace Ginei
{
    /// <summary>屯田制（軍事農業植民地）の調整係数。</summary>
    public readonly struct MilitaryColonyParams
    {
        /// <summary>最も肥沃な土地で全兵を農耕に回したときの食糧自給率の上限（1で完全自給）。</summary>
        public readonly float yieldScale;
        /// <summary>農耕に回した兵の割合が戦闘即応性を削ぐ強さ（1で全員農耕＝即応ゼロ）。</summary>
        public readonly float readinessPenalty;
        /// <summary>農耕に回さない兵でも保つ最低限の即応性（守備兵まで畑には出さない）。</summary>
        public readonly float baseReadiness;
        /// <summary>屯田が成熟する速さ（開墾から収穫までの育成＝1で1tickで成熟）。</summary>
        public readonly float maturityRate;
        /// <summary>恒久入植化に必要な成熟度の閾値（屯田が根付く境目）。</summary>
        public readonly float settlementMaturity;
        /// <summary>恒久入植化に必要な定着年数（根付くには時間が要る）。</summary>
        public readonly float settlementYears;
        /// <summary>自給拠点1つが前線へ与える戦略縦深の規模（補給線なき進撃を支える）。</summary>
        public readonly float depthPerColony;

        public MilitaryColonyParams(float yieldScale, float readinessPenalty, float baseReadiness,
            float maturityRate, float settlementMaturity, float settlementYears, float depthPerColony)
        {
            this.yieldScale = Mathf.Max(0f, yieldScale);
            this.readinessPenalty = Mathf.Clamp01(readinessPenalty);
            this.baseReadiness = Mathf.Clamp01(baseReadiness);
            this.maturityRate = Mathf.Max(0f, maturityRate);
            this.settlementMaturity = Mathf.Clamp01(settlementMaturity);
            this.settlementYears = Mathf.Max(0f, settlementYears);
            this.depthPerColony = Mathf.Max(0f, depthPerColony);
        }

        /// <summary>既定＝自給上限1.0・即応低下0.8・基礎即応0.2・成熟速度0.5・入植成熟0.8・定着年数3・縦深0.25。</summary>
        public static MilitaryColonyParams Default =>
            new MilitaryColonyParams(1f, 0.8f, 0.2f, 0.5f, 0.8f, 3f, 0.25f);
    }

    /// <summary>
    /// 屯田制（軍事農業植民地）の純ロジック（#1107）。占領地で兵士が平時に農耕し戦時に戦う＝駐留地で食糧を
    /// 自給することで長い補給線への依存を恒久的に断つ（曹操の屯田制）。「兵が農を兼ねる＝自給すれば補給線は
    /// 要らぬが、農耕に出した兵はすぐ戦えない＝戦力と引き換え」を式に出す：①農耕に回す兵が多く土地が肥沃なほど
    /// 食糧が自給でき（<see cref="FoodSelfSufficiency"/>）、自給ぶん後方輸送が要らなくなる（<see cref="SupplyLineRelief"/>＝
    /// <see cref="SupplyRules"/> への加算）が、②農耕中の兵は動員に時間が掛かり即応性が落ち（<see cref="CombatReadinessPenalty"/>）、
    /// ③屯田は開墾から収穫まで育成期間が要り初年は実らず（<see cref="ColonyMaturityTick"/>）、
    /// ④根付けば占領地が自国領になり（<see cref="PermanentSettlement"/>＝<see cref="ColonizationRules"/> へ接続）、
    /// ⑤自給拠点が前線を支えて補給線なき進撃を可能にする（<see cref="StrategicDepth"/>）。
    /// 一時的な徴発（糧を敵に因る）は <see cref="ForageRules"/> が、後方からの補給線そのものは <see cref="SupplyRules"/>（L-2）が、
    /// 兵の徴募（人口→兵力）は <see cref="ConscriptionRules"/> が扱う＝こちらは恒久的な自給体制の量とトレードオフのみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MilitaryColonyRules
    {
        /// <summary>
        /// 食糧自給率（0..1）＝自給上限×農耕に回す兵の割合×土地の肥沃度×dt（dtは育成テンポ＝1で満額）。
        /// 兵を農耕に回すほど・土地が肥沃なほど自給できる＝兵農のトレードオフの「農」側。dt≤0 は0（まだ実らない）。
        /// </summary>
        public static float FoodSelfSufficiency(float colonistRatio, float landFertility, float dt, MilitaryColonyParams p)
        {
            if (dt <= 0f) return 0f;
            float ratio = Mathf.Clamp01(colonistRatio);
            float fertility = Mathf.Clamp01(landFertility);
            float tempo = Mathf.Clamp01(dt);
            return Mathf.Clamp01(p.yieldScale * ratio * fertility * tempo);
        }

        public static float FoodSelfSufficiency(float colonistRatio, float landFertility, float dt)
            => FoodSelfSufficiency(colonistRatio, landFertility, dt, MilitaryColonyParams.Default);

        /// <summary>
        /// 補給線の負担軽減＝自給ぶん後方輸送が要らなくなる量（0..garrisonDemand）。
        /// 駐留需要 garrisonDemand のうち自給率 selfSufficiency で賄えた分を返す（過剰自給は輸送軽減には効かない）。
        /// <see cref="SupplyRules.TickFront"/> の resupplyRate に加算する想定＝補給線が短くて済む。
        /// </summary>
        public static float SupplyLineRelief(float selfSufficiency, float garrisonDemand)
        {
            float demand = Mathf.Max(0f, garrisonDemand);
            float supplied = Mathf.Clamp01(selfSufficiency);
            return demand * supplied;
        }

        /// <summary>
        /// 戦闘即応性の低下による実効即応性（0..1）＝基礎即応＋（1−基礎即応）×（1−農耕割合×即応低下）。
        /// 農耕に回した兵はすぐ戦えない＝動員に時間が掛かる（兵農のトレードオフの「兵」側）。
        /// 農耕割合0で1.0（全力即応）、全員農耕なら基礎即応まで落ちる。実効値パターン（基準戦力は非破壊）。
        /// </summary>
        public static float CombatReadinessPenalty(float colonistRatio, MilitaryColonyParams p)
        {
            float ratio = Mathf.Clamp01(colonistRatio);
            float drop = 1f - ratio * p.readinessPenalty;
            return p.baseReadiness + (1f - p.baseReadiness) * drop;
        }

        public static float CombatReadinessPenalty(float colonistRatio)
            => CombatReadinessPenalty(colonistRatio, MilitaryColonyParams.Default);

        /// <summary>
        /// 屯田の成熟（0..1）＝開墾から収穫までの育成期間。残り未成熟分に成熟速度×dt を掛けて漸近的に育つ。
        /// 初年は実らない（成熟が低いと <see cref="FoodSelfSufficiency"/> の効きが薄い想定）＝育成には時間が要る。
        /// 成熟度を返す（上限1）。dt≤0 は据え置き。
        /// </summary>
        public static float ColonyMaturityTick(float maturity, float dt, MilitaryColonyParams p)
        {
            float m = Mathf.Clamp01(maturity);
            if (dt <= 0f) return m;
            float grow = (1f - m) * p.maturityRate * dt;
            return Mathf.Clamp01(m + grow);
        }

        public static float ColonyMaturityTick(float maturity, float dt)
            => ColonyMaturityTick(maturity, dt, MilitaryColonyParams.Default);

        /// <summary>
        /// 恒久的入植化の成否＝屯田が十分に成熟し（settlementMaturity 以上）かつ定着年数を満たした（settlementYears 以上）か。
        /// 根付けば占領地が自国領になる＝<see cref="ColonizationRules.Establish"/> 相当の入植成立条件（兵が住民に変わる）。
        /// </summary>
        public static bool PermanentSettlement(float maturity, float yearsEstablished, MilitaryColonyParams p)
        {
            float m = Mathf.Clamp01(maturity);
            float years = Mathf.Max(0f, yearsEstablished);
            return m >= p.settlementMaturity && years >= p.settlementYears;
        }

        public static bool PermanentSettlement(float maturity, float yearsEstablished)
            => PermanentSettlement(maturity, yearsEstablished, MilitaryColonyParams.Default);

        /// <summary>
        /// 戦略縦深＝自給拠点が前線を支える深さ＝拠点数×拠点あたり縦深×自給率。
        /// 自給できる屯田が多いほど補給線なき進撃を支えられる（前線の後ろに食糧庫が連なる）。
        /// 拠点数0または自給ゼロでは縦深ゼロ（食えない拠点は前線を支えない）。
        /// </summary>
        public static float StrategicDepth(int colonyCount, float selfSufficiency, MilitaryColonyParams p)
        {
            int count = Mathf.Max(0, colonyCount);
            float supplied = Mathf.Clamp01(selfSufficiency);
            return count * p.depthPerColony * supplied;
        }

        public static float StrategicDepth(int colonyCount, float selfSufficiency)
            => StrategicDepth(colonyCount, selfSufficiency, MilitaryColonyParams.Default);
    }
}
