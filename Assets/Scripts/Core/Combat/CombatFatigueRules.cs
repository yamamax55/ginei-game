using UnityEngine;

namespace Ginei
{
    /// <summary>累積戦闘疲弊（戦闘疲労・シェルショック）の調整係数。</summary>
    public readonly struct CombatFatigueParams
    {
        /// <summary>激戦（intensity 1.0）が1tickあたり積む疲弊量（per dt）。</summary>
        public readonly float intensityAccumRate;
        /// <summary>損害（casualties 1.0）が1tickあたり積む疲弊量（per dt）。</summary>
        public readonly float casualtyAccumRate;
        /// <summary>十分な休息（restDuration 1.0）が1tickあたり抜く疲弊量（per dt）。</summary>
        public readonly float restRecoveryRate;
        /// <summary>休息でも抜けきらず残る疲弊の床（戦争の傷＝完全には癒えない）。</summary>
        public readonly float irreducibleFloor;
        /// <summary>累積疲弊が士気・戦闘効率を削る最大幅（疲弊1.0で最大ペナルティ）。</summary>
        public readonly float moralePenaltyScale;
        /// <summary>疲弊が極まると無感覚・厭戦になり脱走が増える、その効き始める疲弊水準。</summary>
        public readonly float numbnessOnset;
        /// <summary>無感覚・厭戦が脱走・自壊を上乗せする増分（onset 超過1あたり）。</summary>
        public readonly float attritionScale;
        /// <summary>歴戦（experience 1.0）が疲弊蓄積・ペナルティを軽減する最大割合（限界あり＝1未満）。</summary>
        public readonly float veteranMitigation;
        /// <summary>戦力にならない「燃え尽き」とみなす疲弊の既定閾値。</summary>
        public readonly float burnoutThreshold;

        public CombatFatigueParams(float intensityAccumRate, float casualtyAccumRate, float restRecoveryRate,
                                   float irreducibleFloor, float moralePenaltyScale, float numbnessOnset,
                                   float attritionScale, float veteranMitigation, float burnoutThreshold)
        {
            this.intensityAccumRate = Mathf.Max(0f, intensityAccumRate);
            this.casualtyAccumRate = Mathf.Max(0f, casualtyAccumRate);
            this.restRecoveryRate = Mathf.Max(0f, restRecoveryRate);
            this.irreducibleFloor = Mathf.Clamp01(irreducibleFloor);
            this.moralePenaltyScale = Mathf.Clamp01(moralePenaltyScale);
            this.numbnessOnset = Mathf.Clamp01(numbnessOnset);
            this.attritionScale = Mathf.Max(0f, attritionScale);
            this.veteranMitigation = Mathf.Clamp01(veteranMitigation);
            this.burnoutThreshold = Mathf.Clamp01(burnoutThreshold);
        }

        /// <summary>
        /// 既定＝激戦蓄積0.03/損害蓄積0.04/休息回復0.025・癒えぬ床0.1・士気ペナルティ幅0.6・
        /// 無感覚開始0.7・脱走増分0.05・歴戦軽減0.3・燃え尽き閾値0.85。
        /// 蓄積（激戦＋損害）が休息回復をわずかに上回る＝連戦は回復が追いつかない。
        /// </summary>
        public static CombatFatigueParams Default =>
            new CombatFatigueParams(0.03f, 0.04f, 0.025f, 0.1f, 0.6f, 0.7f, 0.05f, 0.3f, 0.85f);
    }

    /// <summary>
    /// 累積戦闘疲弊（戦闘疲労・シェルショック）の純ロジック（#1403・レマルク『西部戦線異状なし』型）。
    /// 戦争が長引くと、個々の会戦の勝敗とは別に、兵士の心身に持続的な疲弊が蓄積する＝休息で部分回復するが
    /// 連戦が続くと回復が追いつかず士気が摩耗し、やがて無感覚・厭戦・脱走へ至る。会戦をまたいで持続する。
    /// <see cref="FleetMorale"/>（会戦内の士気管理）・<see cref="ReadinessRules"/>（即応態勢の短期疲労）とは別＝
    /// 連戦による会戦をまたぐ持続的な士気の摩耗。極まった疲弊の脱走・自壊は <see cref="DesertionRules"/>（脱走）へ、
    /// 歴戦の耐性は <see cref="VeterancyRules"/>（練度）の experience を入力に取る想定。
    /// 倍率・ペナルティは基準値に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CombatFatigueRules
    {
        /// <summary>
        /// 戦闘疲弊の蓄積（戻り＝1tick後の累積疲弊0..1）。激戦（battleIntensity）と損害（casualties）が
        /// それぞれの蓄積率で疲弊を積む＝会戦をまたいで溜まり続ける。
        /// </summary>
        public static float FatigueAccumulation(float currentFatigue, float battleIntensity, float casualties, float dt, CombatFatigueParams p)
        {
            float add = (Mathf.Clamp01(battleIntensity) * p.intensityAccumRate
                       + Mathf.Clamp01(casualties) * p.casualtyAccumRate) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(currentFatigue) + add);
        }

        public static float FatigueAccumulation(float currentFatigue, float battleIntensity, float casualties, float dt)
            => FatigueAccumulation(currentFatigue, battleIntensity, casualties, dt, CombatFatigueParams.Default);

        /// <summary>
        /// 休息による部分回復（戻り＝1tick後の累積疲弊0..1）。restDuration ぶん疲弊が抜けるが、
        /// 癒えぬ床（irreducibleFloor）までしか戻らない＝戦争の傷は完全には抜けない。
        /// </summary>
        public static float RestRecovery(float currentFatigue, float restDuration, float dt, CombatFatigueParams p)
        {
            float recovered = Mathf.Clamp01(currentFatigue) - Mathf.Clamp01(restDuration) * p.restRecoveryRate * Mathf.Max(0f, dt);
            // 床までしか戻らない（ただし既に床より低ければそのまま）
            float floor = Mathf.Min(p.irreducibleFloor, Mathf.Clamp01(currentFatigue));
            return Mathf.Clamp01(Mathf.Max(floor, recovered));
        }

        public static float RestRecovery(float currentFatigue, float restDuration, float dt)
            => RestRecovery(currentFatigue, restDuration, dt, CombatFatigueParams.Default);

        /// <summary>
        /// 蓄積と回復の差し引き（戻り＝1tick後の累積疲弊0..1）。激戦で積み・休息で抜く。
        /// 既定では連戦（高 intensity・低 rest）だと蓄積が回復を上回り、疲弊が増え続ける＝回復が追いつかない。
        /// 損害は別経路（<see cref="FatigueAccumulation"/>）で足す想定でここでは intensity と rest の純差し引き。
        /// </summary>
        public static float NetFatigueTick(float currentFatigue, float battleIntensity, float restDuration, float dt, CombatFatigueParams p)
        {
            float accum = Mathf.Clamp01(battleIntensity) * p.intensityAccumRate;
            float recov = Mathf.Clamp01(restDuration) * p.restRecoveryRate;
            float delta = (accum - recov) * Mathf.Max(0f, dt);
            float next = Mathf.Clamp01(currentFatigue) + delta;
            // 回復しても癒えぬ床は残る（既に床より低ければ維持）
            float floor = Mathf.Min(p.irreducibleFloor, Mathf.Clamp01(currentFatigue));
            return Mathf.Clamp01(Mathf.Max(floor, next));
        }

        public static float NetFatigueTick(float currentFatigue, float battleIntensity, float restDuration, float dt)
            => NetFatigueTick(currentFatigue, battleIntensity, restDuration, dt, CombatFatigueParams.Default);

        /// <summary>
        /// 累積疲弊が士気を持続的に削る倍率（0..1・実効値）。<see cref="FleetMorale"/> の士気係数に掛ける想定＝
        /// 1−疲弊×moralePenaltyScale。疲弊が深いほど士気が摩耗する。
        /// </summary>
        public static float MoralePenaltyFromFatigue(float fatigue, CombatFatigueParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(fatigue) * p.moralePenaltyScale);
        }

        public static float MoralePenaltyFromFatigue(float fatigue)
            => MoralePenaltyFromFatigue(fatigue, CombatFatigueParams.Default);

        /// <summary>
        /// 疲弊が戦闘効率を落とす倍率（0..1・実効値）＝1−疲弊×moralePenaltyScale。
        /// 疲れた兵は弱い。攻撃・命中などの基準に掛ける想定（基準非破壊）。
        /// </summary>
        public static float CombatEffectivenessDecay(float fatigue, CombatFatigueParams p)
        {
            return Mathf.Clamp01(1f - Mathf.Clamp01(fatigue) * p.moralePenaltyScale);
        }

        public static float CombatEffectivenessDecay(float fatigue)
            => CombatEffectivenessDecay(fatigue, CombatFatigueParams.Default);

        /// <summary>
        /// シェルショック（心的外傷）の発生リスク（0..1）＝累積疲弊×突発的衝撃。
        /// 極度に疲弊した兵が突然の惨禍（suddenHorror）に晒されると心が折れる。乱数なし＝確率を返すのみ。
        /// </summary>
        public static float ShellShockRisk(float fatigue, float suddenHorror)
        {
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) * Mathf.Clamp01(suddenHorror));
        }

        /// <summary>
        /// 無感覚・厭戦による脱走・自壊の上乗せ率（per dt）。疲弊が numbnessOnset を超えると、
        /// 超過ぶん×attritionScale で脱走・自壊が増える＝<see cref="DesertionRules"/> の脱走率へ足す想定。
        /// </summary>
        public static float NumbnessAndAttrition(float fatigue, float dt, CombatFatigueParams p)
        {
            float over = Mathf.Max(0f, Mathf.Clamp01(fatigue) - p.numbnessOnset);
            return over * p.attritionScale * Mathf.Max(0f, dt);
        }

        public static float NumbnessAndAttrition(float fatigue, float dt)
            => NumbnessAndAttrition(fatigue, dt, CombatFatigueParams.Default);

        /// <summary>
        /// 歴戦の耐性を織り込んだ実効疲弊（0..1）。経験（experience）が疲弊を veteranMitigation まで軽減するが、
        /// 軽減には上限があり完全には消えない＝ベテランも壊れる。
        /// </summary>
        public static float VeteranResilience(float fatigue, float experience, CombatFatigueParams p)
        {
            float mitigation = Mathf.Clamp01(experience) * p.veteranMitigation;
            return Mathf.Clamp01(Mathf.Clamp01(fatigue) * (1f - mitigation));
        }

        public static float VeteranResilience(float fatigue, float experience)
            => VeteranResilience(fatigue, experience, CombatFatigueParams.Default);

        /// <summary>
        /// 戦闘疲弊が限界に達し戦力にならない「燃え尽き」判定＝疲弊が threshold 以上。
        /// 休ませる・後方へ下げる判断材料。
        /// </summary>
        public static bool IsBurnedOut(float fatigue, float threshold)
        {
            return Mathf.Clamp01(fatigue) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="CombatFatigueParams.burnoutThreshold"/>）での燃え尽き判定。</summary>
        public static bool IsBurnedOut(float fatigue, CombatFatigueParams p)
            => IsBurnedOut(fatigue, p.burnoutThreshold);

        public static bool IsBurnedOut(float fatigue)
            => IsBurnedOut(fatigue, CombatFatigueParams.Default.burnoutThreshold);
    }
}
