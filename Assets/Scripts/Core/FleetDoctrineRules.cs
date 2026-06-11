using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊ドクトリン＝海軍戦略思想（マハン/コーベット/リサ学派）の艦隊運用思想（#1432）。
    /// ①艦隊決戦＝敵主力を一挙に撃滅（マハン的・主力決戦至上）／②漸減邀撃＝敵を消耗させてから決戦（旧日本海軍の邀撃漸減作戦）／
    /// ③通商破壊＝敵の海上交通を断つ（ゲリラ的・コーベット/リサ学派の通商破壊）／④現存艦隊＝弱者が決戦を避け艦隊の存在で牽制（fleet in being）。
    /// </summary>
    public enum FleetDoctrine
    {
        /// <summary>艦隊決戦＝敵主力を一挙に撃滅する攻勢的ドクトリン。</summary>
        艦隊決戦,
        /// <summary>漸減邀撃＝敵を消耗させてから決戦に持ち込む邀撃的ドクトリン。</summary>
        漸減邀撃,
        /// <summary>通商破壊＝敵の海上交通を断つ襲撃的ドクトリン。</summary>
        通商破壊,
        /// <summary>現存艦隊＝決戦を避け艦隊の存在で牽制する温存的ドクトリン（fleet in being）。</summary>
        現存艦隊,
    }

    /// <summary>
    /// AI行動の傾き（各ドクトリンの性格）。攻勢的＝決戦を求める／守勢的＝邀撃で受ける／襲撃的＝通商を狙う／温存的＝決戦を避ける。
    /// </summary>
    public enum DoctrineBias
    {
        /// <summary>攻勢的（艦隊決戦）。</summary>
        攻勢的,
        /// <summary>守勢的（漸減邀撃）。</summary>
        守勢的,
        /// <summary>襲撃的（通商破壊）。</summary>
        襲撃的,
        /// <summary>温存的（現存艦隊）。</summary>
        温存的,
    }

    /// <summary>
    /// 艦隊ドクトリン選択＝海軍戦略思想の純ロジック（SKUN-2 #1432・坂の上の雲/海軍戦略）。
    /// 「艦隊運用には決戦・漸減・通商破壊・現存艦隊のドクトリンがあり、各々が決戦/消耗/襲撃/回避の AI 行動重みを決め、
    /// 状況（戦力比・補給依存）と敵ドクトリンとの相性（ジャンケン的）がある」を式に出す。
    /// 各ドクトリンの行動重み（<see cref="EngagementWeight"/>/<see cref="AttritionWeight"/>/<see cref="CommerceRaidWeight"/>/<see cref="AvoidanceWeight"/>）が
    /// AI の行動傾向を決め、<see cref="DoctrineMatchup"/> が敵ドクトリンとの相性、<see cref="SituationalFitness"/> が状況適合度を返し、
    /// <see cref="OptimalDoctrine"/> が状況に応じた最適ドクトリンを推奨する。
    /// 分担：<see cref="SunziDoctrineRules"/> は孫子の戦略手段（謀略＞外交＞野戦＞攻城）の選択、
    /// <see cref="OperationalAptitudeRules"/> は提督個人の戦闘類型への適性、
    /// <see cref="CommerceRaidingRules"/> は通商破壊の実体（船団撃破/補給遮断）、
    /// <see cref="FleetCapRules"/> は指揮容量を担う。ここは「海軍ドクトリンの選択とドクトリン相性（AI 行動重み）」のみ。
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FleetDoctrineRules
    {
        /// <summary>ドクトリンの数（重み配列の長さ）。</summary>
        public const int DoctrineCount = 4;

        /// <summary>
        /// 決戦を求める度合い＝敵主力との一挙決戦を志向する重み（艦隊決戦＝高・現存艦隊＝低）。
        /// </summary>
        public static float EngagementWeight(FleetDoctrine doctrine, FleetDoctrineParams p)
        {
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦: return p.DecisiveEngagementWeight;
                case FleetDoctrine.漸減邀撃: return p.AttritionEngagementWeight;
                case FleetDoctrine.通商破壊: return p.RaidEngagementWeight;
                case FleetDoctrine.現存艦隊:
                default: return p.FleetInBeingEngagementWeight;
            }
        }

        public static float EngagementWeight(FleetDoctrine doctrine)
            => EngagementWeight(doctrine, FleetDoctrineParams.Default);

        /// <summary>
        /// 消耗戦・邀撃を重視する度合い＝決戦前に敵を漸減して受ける重み（漸減邀撃＝高）。
        /// </summary>
        public static float AttritionWeight(FleetDoctrine doctrine, FleetDoctrineParams p)
        {
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦: return p.DecisiveAttritionWeight;
                case FleetDoctrine.漸減邀撃: return p.AttritionAttritionWeight;
                case FleetDoctrine.通商破壊: return p.RaidAttritionWeight;
                case FleetDoctrine.現存艦隊:
                default: return p.FleetInBeingAttritionWeight;
            }
        }

        public static float AttritionWeight(FleetDoctrine doctrine)
            => AttritionWeight(doctrine, FleetDoctrineParams.Default);

        /// <summary>
        /// 通商破壊を重視する度合い＝敵の海上交通を断つ襲撃を志向する重み（通商破壊＝高）。
        /// </summary>
        public static float CommerceRaidWeight(FleetDoctrine doctrine, FleetDoctrineParams p)
        {
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦: return p.DecisiveRaidWeight;
                case FleetDoctrine.漸減邀撃: return p.AttritionRaidWeight;
                case FleetDoctrine.通商破壊: return p.RaidRaidWeight;
                case FleetDoctrine.現存艦隊:
                default: return p.FleetInBeingRaidWeight;
            }
        }

        public static float CommerceRaidWeight(FleetDoctrine doctrine)
            => CommerceRaidWeight(doctrine, FleetDoctrineParams.Default);

        /// <summary>
        /// 決戦回避・温存を重視する度合い＝決戦を避け艦隊の存在で牽制する重み（現存艦隊＝高）。
        /// </summary>
        public static float AvoidanceWeight(FleetDoctrine doctrine, FleetDoctrineParams p)
        {
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦: return p.DecisiveAvoidanceWeight;
                case FleetDoctrine.漸減邀撃: return p.AttritionAvoidanceWeight;
                case FleetDoctrine.通商破壊: return p.RaidAvoidanceWeight;
                case FleetDoctrine.現存艦隊:
                default: return p.FleetInBeingAvoidanceWeight;
            }
        }

        public static float AvoidanceWeight(FleetDoctrine doctrine)
            => AvoidanceWeight(doctrine, FleetDoctrineParams.Default);

        /// <summary>
        /// 各ドクトリンのAI行動の傾き＝攻勢的（艦隊決戦）/守勢的（漸減邀撃）/襲撃的（通商破壊）/温存的（現存艦隊）。
        /// </summary>
        public static DoctrineBias AIBehaviorBias(FleetDoctrine doctrine)
        {
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦: return DoctrineBias.攻勢的;
                case FleetDoctrine.漸減邀撃: return DoctrineBias.守勢的;
                case FleetDoctrine.通商破壊: return DoctrineBias.襲撃的;
                case FleetDoctrine.現存艦隊:
                default: return DoctrineBias.温存的;
            }
        }

        /// <summary>
        /// 自軍ドクトリンと敵ドクトリンの相性（ジャンケン的）。1.0＝互角、1超＝有利、1未満＝不利。
        /// ・決戦は決戦に応じる（決戦を望む敵には決戦が噛み合う）。
        /// ・漸減邀撃は決戦志向の敵に有利（来る敵を漸減して受ける）。
        /// ・通商破壊は現存艦隊に有利（決戦を避け温存する敵の交通線を突く）。
        /// ・現存艦隊は通商破壊に弱い（決戦を避けても交通線を狩られる）。
        /// </summary>
        public static float DoctrineMatchup(FleetDoctrine own, FleetDoctrine enemy, FleetDoctrineParams p)
        {
            // ジャンケン的相性表：own×enemy → 倍率。
            switch (own)
            {
                case FleetDoctrine.艦隊決戦:
                    // 決戦は決戦に応じる（互角）。回避志向（現存艦隊）は捕まえられず不利、漸減邀撃には消耗させられ不利。
                    switch (enemy)
                    {
                        case FleetDoctrine.艦隊決戦: return 1.0f;
                        case FleetDoctrine.漸減邀撃: return p.Disadvantage;
                        case FleetDoctrine.通商破壊: return p.Advantage;
                        default: return p.Disadvantage; // 現存艦隊＝捕捉できず不利
                    }
                case FleetDoctrine.漸減邀撃:
                    // 来る敵を漸減して受ける＝決戦志向に有利。襲撃的な通商破壊には掴みどころが無く不利。
                    switch (enemy)
                    {
                        case FleetDoctrine.艦隊決戦: return p.Advantage;
                        case FleetDoctrine.漸減邀撃: return 1.0f;
                        case FleetDoctrine.通商破壊: return p.Disadvantage;
                        default: return p.Advantage; // 現存艦隊＝牽制しても邀撃で押せる
                    }
                case FleetDoctrine.通商破壊:
                    // 交通線を断つ＝温存・回避する敵（現存艦隊）に有利。漸減邀撃の網には掛かりやすく不利。
                    switch (enemy)
                    {
                        case FleetDoctrine.艦隊決戦: return p.Disadvantage;
                        case FleetDoctrine.漸減邀撃: return p.Advantage;
                        case FleetDoctrine.通商破壊: return 1.0f;
                        default: return p.Advantage; // 現存艦隊に有利
                    }
                case FleetDoctrine.現存艦隊:
                default:
                    // 決戦を避け牽制＝決戦志向の敵を空転させ有利。通商破壊には交通線を狩られ弱い。
                    switch (enemy)
                    {
                        case FleetDoctrine.艦隊決戦: return p.Advantage;
                        case FleetDoctrine.漸減邀撃: return p.Disadvantage;
                        case FleetDoctrine.通商破壊: return p.Disadvantage; // 現存艦隊は通商破壊に弱い
                        default: return 1.0f;
                    }
            }
        }

        public static float DoctrineMatchup(FleetDoctrine own, FleetDoctrine enemy)
            => DoctrineMatchup(own, enemy, FleetDoctrineParams.Default);

        /// <summary>
        /// 戦力比と補給依存に応じたドクトリンの適合度(0..1)。
        /// forceRatio＝自軍戦力／（自軍＋敵）の比(0..1)＝0.5で互角・1で圧倒的優勢・0で劣勢。
        /// supplyDependence＝敵の補給依存度(0..1)＝高いほど通商破壊が刺さる。
        /// ・艦隊決戦＝優勢（forceRatio高）ほど適合（一挙撃滅できる）。
        /// ・漸減邀撃＝互角付近で最も適合（受けて削る）。
        /// ・通商破壊＝敵の補給依存が高いほど適合（交通線を断てる）。
        /// ・現存艦隊＝劣勢（forceRatio低）ほど適合（決戦を避け牽制）。
        /// </summary>
        public static float SituationalFitness(FleetDoctrine doctrine, float forceRatio, float supplyDependence, FleetDoctrineParams p)
        {
            float ratio = Mathf.Clamp01(forceRatio);
            float supply = Mathf.Clamp01(supplyDependence);
            switch (doctrine)
            {
                case FleetDoctrine.艦隊決戦:
                    // 優勢ほど適合（劣勢での決戦は無謀）。
                    return ratio;
                case FleetDoctrine.漸減邀撃:
                    // 互角付近(0.5)で最大＝1、両極で小さい三角ピーク。
                    return Mathf.Clamp01(1f - p.AttritionPeakSharpness * Mathf.Abs(ratio - 0.5f));
                case FleetDoctrine.通商破壊:
                    // 敵の補給依存が高いほど適合。劣勢でも交通線は突ける（戦力比は弱く効く）。
                    return Mathf.Clamp01(supply * p.RaidSupplyWeight + (1f - ratio) * (1f - p.RaidSupplyWeight));
                case FleetDoctrine.現存艦隊:
                default:
                    // 劣勢ほど適合（弱者が決戦を避け艦隊の存在で牽制）。
                    return 1f - ratio;
            }
        }

        public static float SituationalFitness(FleetDoctrine doctrine, float forceRatio, float supplyDependence)
            => SituationalFitness(doctrine, forceRatio, supplyDependence, FleetDoctrineParams.Default);

        /// <summary>
        /// 状況に応じた最適ドクトリンの推奨＝4ドクトリンの状況適合度が最大のものを返す。
        /// forceRatio＝自軍戦力比(0..1)、enemySupplyDependence＝敵の補給依存(0..1)、ownSupplyDependence＝自軍の補給依存(0..1)。
        /// 自軍の補給依存が高いほど通商破壊（自分も交通線を晒す）と現存艦隊（決戦より温存）へ傾けず慎重に評価する。
        /// 同点なら序列が上（艦隊決戦＞漸減邀撃＞通商破壊＞現存艦隊）を優先＝攻勢を温存。
        /// </summary>
        public static FleetDoctrine OptimalDoctrine(float forceRatio, float enemySupplyDependence, float ownSupplyDependence, FleetDoctrineParams p)
        {
            float ratio = Mathf.Clamp01(forceRatio);
            float enemySupply = Mathf.Clamp01(enemySupplyDependence);
            float ownSupply = Mathf.Clamp01(ownSupplyDependence);

            FleetDoctrine best = FleetDoctrine.艦隊決戦;
            float bestScore = -1f;
            // 序列順（艦隊決戦→現存艦隊）に走査し、厳密に上回ったときだけ更新＝同点は攻勢側を温存。
            for (int i = 0; i < DoctrineCount; i++)
            {
                var doctrine = (FleetDoctrine)i;
                float fitness = SituationalFitness(doctrine, ratio, enemySupply, p);
                // 自軍の補給依存ペナルティ＝通商破壊・現存艦隊は自軍の交通線・温存を晒すぶん割り引く。
                if (doctrine == FleetDoctrine.通商破壊 || doctrine == FleetDoctrine.現存艦隊)
                    fitness *= 1f - p.OwnSupplyPenalty * ownSupply;

                if (fitness > bestScore)
                {
                    bestScore = fitness;
                    best = doctrine;
                }
            }
            return best;
        }

        public static FleetDoctrine OptimalDoctrine(float forceRatio, float enemySupplyDependence, float ownSupplyDependence)
            => OptimalDoctrine(forceRatio, enemySupplyDependence, ownSupplyDependence, FleetDoctrineParams.Default);
    }

    /// <summary>
    /// FleetDoctrineRules の調整値（#1432・マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 各ドクトリンの行動重み（決戦/消耗/襲撃/回避）＋相性倍率＋状況適合の係数を持つ。
    /// </summary>
    public readonly struct FleetDoctrineParams
    {
        // --- 艦隊決戦の行動重み ---
        /// <summary>艦隊決戦の決戦重み（高＝主力決戦を強く志向）。</summary>
        public readonly float DecisiveEngagementWeight;
        /// <summary>艦隊決戦の消耗重み（低＝漸減より一挙撃滅）。</summary>
        public readonly float DecisiveAttritionWeight;
        /// <summary>艦隊決戦の襲撃重み（低）。</summary>
        public readonly float DecisiveRaidWeight;
        /// <summary>艦隊決戦の回避重み（低＝決戦を避けない）。</summary>
        public readonly float DecisiveAvoidanceWeight;

        // --- 漸減邀撃の行動重み ---
        /// <summary>漸減邀撃の決戦重み（中＝漸減後の決戦）。</summary>
        public readonly float AttritionEngagementWeight;
        /// <summary>漸減邀撃の消耗重み（高＝敵を漸減して受ける）。</summary>
        public readonly float AttritionAttritionWeight;
        /// <summary>漸減邀撃の襲撃重み（中＝邀撃の前哨）。</summary>
        public readonly float AttritionRaidWeight;
        /// <summary>漸減邀撃の回避重み（中＝決戦を遅らせる）。</summary>
        public readonly float AttritionAvoidanceWeight;

        // --- 通商破壊の行動重み ---
        /// <summary>通商破壊の決戦重み（低＝主力決戦は避ける）。</summary>
        public readonly float RaidEngagementWeight;
        /// <summary>通商破壊の消耗重み（中＝じわじわ削る）。</summary>
        public readonly float RaidAttritionWeight;
        /// <summary>通商破壊の襲撃重み（高＝交通線を断つ）。</summary>
        public readonly float RaidRaidWeight;
        /// <summary>通商破壊の回避重み（中＝主力との接触を避ける）。</summary>
        public readonly float RaidAvoidanceWeight;

        // --- 現存艦隊の行動重み ---
        /// <summary>現存艦隊の決戦重み（低＝決戦を避ける）。</summary>
        public readonly float FleetInBeingEngagementWeight;
        /// <summary>現存艦隊の消耗重み（低）。</summary>
        public readonly float FleetInBeingAttritionWeight;
        /// <summary>現存艦隊の襲撃重み（低）。</summary>
        public readonly float FleetInBeingRaidWeight;
        /// <summary>現存艦隊の回避重み（高＝艦隊の存在で牽制し温存）。</summary>
        public readonly float FleetInBeingAvoidanceWeight;

        // --- 相性・状況係数 ---
        /// <summary>相性で有利なときの倍率（>1）。</summary>
        public readonly float Advantage;
        /// <summary>相性で不利なときの倍率（<1）。</summary>
        public readonly float Disadvantage;
        /// <summary>漸減邀撃の互角ピークの鋭さ（戦力比が0.5から離れるほど適合が落ちる傾き）。</summary>
        public readonly float AttritionPeakSharpness;
        /// <summary>通商破壊の適合で敵補給依存が占める重み（残りは劣勢度）。</summary>
        public readonly float RaidSupplyWeight;
        /// <summary>自軍補給依存が通商破壊・現存艦隊の適合を割り引く最大係数。</summary>
        public readonly float OwnSupplyPenalty;

        public FleetDoctrineParams(
            float decisiveEngagementWeight, float decisiveAttritionWeight, float decisiveRaidWeight, float decisiveAvoidanceWeight,
            float attritionEngagementWeight, float attritionAttritionWeight, float attritionRaidWeight, float attritionAvoidanceWeight,
            float raidEngagementWeight, float raidAttritionWeight, float raidRaidWeight, float raidAvoidanceWeight,
            float fleetInBeingEngagementWeight, float fleetInBeingAttritionWeight, float fleetInBeingRaidWeight, float fleetInBeingAvoidanceWeight,
            float advantage, float disadvantage,
            float attritionPeakSharpness, float raidSupplyWeight, float ownSupplyPenalty)
        {
            DecisiveEngagementWeight = decisiveEngagementWeight;
            DecisiveAttritionWeight = decisiveAttritionWeight;
            DecisiveRaidWeight = decisiveRaidWeight;
            DecisiveAvoidanceWeight = decisiveAvoidanceWeight;
            AttritionEngagementWeight = attritionEngagementWeight;
            AttritionAttritionWeight = attritionAttritionWeight;
            AttritionRaidWeight = attritionRaidWeight;
            AttritionAvoidanceWeight = attritionAvoidanceWeight;
            RaidEngagementWeight = raidEngagementWeight;
            RaidAttritionWeight = raidAttritionWeight;
            RaidRaidWeight = raidRaidWeight;
            RaidAvoidanceWeight = raidAvoidanceWeight;
            FleetInBeingEngagementWeight = fleetInBeingEngagementWeight;
            FleetInBeingAttritionWeight = fleetInBeingAttritionWeight;
            FleetInBeingRaidWeight = fleetInBeingRaidWeight;
            FleetInBeingAvoidanceWeight = fleetInBeingAvoidanceWeight;
            Advantage = advantage;
            Disadvantage = disadvantage;
            AttritionPeakSharpness = attritionPeakSharpness;
            RaidSupplyWeight = raidSupplyWeight;
            OwnSupplyPenalty = ownSupplyPenalty;
        }

        /// <summary>
        /// 既定。行動重みは各ドクトリンの性格を反映：
        /// 艦隊決戦＝決戦1.0/消耗0.2/襲撃0.1/回避0.0、漸減邀撃＝決戦0.5/消耗1.0/襲撃0.4/回避0.5、
        /// 通商破壊＝決戦0.1/消耗0.4/襲撃1.0/回避0.5、現存艦隊＝決戦0.0/消耗0.1/襲撃0.2/回避1.0。
        /// 相性＝有利1.3/不利0.75。漸減ピーク鋭さ1.5、通商の補給重み0.7、自軍補給ペナルティ0.4。
        /// </summary>
        public static FleetDoctrineParams Default => new FleetDoctrineParams(
            decisiveEngagementWeight: 1.0f, decisiveAttritionWeight: 0.2f, decisiveRaidWeight: 0.1f, decisiveAvoidanceWeight: 0.0f,
            attritionEngagementWeight: 0.5f, attritionAttritionWeight: 1.0f, attritionRaidWeight: 0.4f, attritionAvoidanceWeight: 0.5f,
            raidEngagementWeight: 0.1f, raidAttritionWeight: 0.4f, raidRaidWeight: 1.0f, raidAvoidanceWeight: 0.5f,
            fleetInBeingEngagementWeight: 0.0f, fleetInBeingAttritionWeight: 0.1f, fleetInBeingRaidWeight: 0.2f, fleetInBeingAvoidanceWeight: 1.0f,
            advantage: 1.3f, disadvantage: 0.75f,
            attritionPeakSharpness: 1.5f, raidSupplyWeight: 0.7f, ownSupplyPenalty: 0.4f);
    }
}
