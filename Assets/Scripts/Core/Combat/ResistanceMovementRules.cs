using UnityEngine;

namespace Ginei
{
    /// <summary>占領下抵抗運動の調整係数（地下組織の成長・秘匿・支援・士気の重み）。</summary>
    public readonly struct ResistanceMovementParams
    {
        /// <summary>細胞成長の速度（訴求×共感×秘匿に掛ける重み・0..1へ収める係数）。</summary>
        public readonly float growthWeight;
        /// <summary>敵防諜圧が保安を侵食する重み（大きいほど露見しやすい）。</summary>
        public readonly float counterIntelWeight;
        /// <summary>保安計算の分母最小ガード（0除算回避・秘匿0でも有限値）。</summary>
        public readonly float securityFloor;
        /// <summary>外部後ろ盾の重み（亡命政府×外国支援の幾何平均に掛ける）。</summary>
        public readonly float backingWeight;
        /// <summary>破壊活動の産出重み（組織力×保安に掛ける）。</summary>
        public readonly float sabotageWeight;
        /// <summary>弾圧耐性の重み（保安×住民共感の幾何平均に掛ける）。</summary>
        public readonly float resilienceWeight;
        /// <summary>士気のうち外部後ろ盾の寄与（後ろ盾/戦果/希望の合計＝1）。</summary>
        public readonly float moraleBackingWeight;
        /// <summary>士気のうち破壊活動の戦果の寄与。</summary>
        public readonly float moraleSabotageWeight;
        /// <summary>士気のうち解放の希望の寄与。</summary>
        public readonly float moraleHopeWeight;

        public ResistanceMovementParams(float growthWeight, float counterIntelWeight, float securityFloor,
            float backingWeight, float sabotageWeight, float resilienceWeight,
            float moraleBackingWeight, float moraleSabotageWeight, float moraleHopeWeight)
        {
            this.growthWeight = Mathf.Clamp01(growthWeight);
            this.counterIntelWeight = Mathf.Max(0f, counterIntelWeight);
            this.securityFloor = Mathf.Max(1e-4f, securityFloor);
            this.backingWeight = Mathf.Clamp01(backingWeight);
            this.sabotageWeight = Mathf.Clamp01(sabotageWeight);
            this.resilienceWeight = Mathf.Clamp01(resilienceWeight);
            this.moraleBackingWeight = Mathf.Clamp01(moraleBackingWeight);
            this.moraleSabotageWeight = Mathf.Clamp01(moraleSabotageWeight);
            this.moraleHopeWeight = Mathf.Clamp01(moraleHopeWeight);
        }

        /// <summary>
        /// 既定＝成長重み0.6・防諜重み1.0・保安分母ガード0.05・後ろ盾重み1.0・破壊活動重み1.0・弾圧耐性重み1.0・
        /// 士気配分（後ろ盾0.4／戦果0.3／希望0.3）。
        /// </summary>
        public static ResistanceMovementParams Default =>
            new ResistanceMovementParams(0.6f, 1.0f, 0.05f, 1.0f, 1.0f, 1.0f, 0.4f, 0.3f, 0.3f);
    }

    /// <summary>
    /// 占領下で形成される地下抵抗運動（レジスタンス組織）の純ロジック。
    /// 地下細胞の成長、秘匿と保安（密告対策）、外部勢力（亡命政府/敵国）からの支援、
    /// 破壊活動による占領者への打撃、弾圧への耐性、解放への機運（士気）、そして運動の存続を扱う。
    /// <para>
    /// 分担：<see cref="GuerrillaDoctrineRules"/> は武力遊撃戦の戦術モデル、本クラスは組織の存続・成長・士気に特化する
    /// （秘匿された地下組織がどれだけ生き延び、結束し、活動を続けられるか）。
    /// <see cref="InsurgencyRules"/> は外部支援された反乱の組織化＝反乱圧力の増幅、
    /// <see cref="HomelandResistanceRules"/> は侵攻深度に応じた縦深抵抗。
    /// </para>
    /// 全入力クランプ・乱数なし決定論・実効値パターン（基準値非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ResistanceMovementRules
    {
        /// <summary>
        /// 地下細胞の成長（0..1）＝訴求力 recruitmentAppeal×住民共感 populationSympathy×秘匿 secrecy×成長重み。
        /// 訴求・共感・秘匿のどれか1つでも欠ければ細胞は太らない（秘密裏に共感を訴えられて初めて人が集まる）。
        /// </summary>
        public static float CellGrowth(float recruitmentAppeal, float populationSympathy, float secrecy, ResistanceMovementParams p)
        {
            float drive = Mathf.Clamp01(recruitmentAppeal) * Mathf.Clamp01(populationSympathy) * Mathf.Clamp01(secrecy);
            return Mathf.Clamp01(drive * p.growthWeight);
        }

        public static float CellGrowth(float recruitmentAppeal, float populationSympathy, float secrecy)
            => CellGrowth(recruitmentAppeal, populationSympathy, secrecy, ResistanceMovementParams.Default);

        /// <summary>
        /// 作戦保安＝露見しにくさ（0..1）＝分散秘匿 compartmentalization/(分散秘匿＋敵防諜圧×重み)。
        /// 細胞を分散させ秘匿するほど、敵の防諜圧 counterIntelPressure が強いほど露見する＝相対比で保安が決まる。
        /// </summary>
        public static float OperationalSecurity(float compartmentalization, float counterIntelPressure, ResistanceMovementParams p)
        {
            float comp = Mathf.Clamp01(compartmentalization);
            float threat = Mathf.Clamp01(counterIntelPressure) * p.counterIntelWeight;
            float denom = Mathf.Max(p.securityFloor, comp + threat);
            return Mathf.Clamp01(comp / denom);
        }

        public static float OperationalSecurity(float compartmentalization, float counterIntelPressure)
            => OperationalSecurity(compartmentalization, counterIntelPressure, ResistanceMovementParams.Default);

        /// <summary>
        /// 外部後ろ盾（0..1）＝亡命政府 governmentInExile×外国支援 foreignAid の幾何平均×重み。
        /// 亡命政府の正統性と敵国の支援が両方そろうほど後ろ盾になる（どちらか0なら後ろ盾なし＝幾何平均）。
        /// </summary>
        public static float ExternalBacking(float governmentInExile, float foreignAid, ResistanceMovementParams p)
        {
            float gie = Mathf.Clamp01(governmentInExile);
            float aid = Mathf.Clamp01(foreignAid);
            float geo = Mathf.Sqrt(gie * aid);
            return Mathf.Clamp01(geo * p.backingWeight);
        }

        public static float ExternalBacking(float governmentInExile, float foreignAid)
            => ExternalBacking(governmentInExile, foreignAid, ResistanceMovementParams.Default);

        /// <summary>
        /// 破壊活動の産出（0..1）＝組織力 cellStrength×作戦保安 operationalSecurity×重み。
        /// 安全に動けるほど（保安が高いほど）打撃を出せる＝露見を恐れる組織は活動を出せない。
        /// </summary>
        public static float SabotageOutput(float cellStrength, float operationalSecurity, ResistanceMovementParams p)
        {
            float t = Mathf.Clamp01(cellStrength) * Mathf.Clamp01(operationalSecurity);
            return Mathf.Clamp01(t * p.sabotageWeight);
        }

        public static float SabotageOutput(float cellStrength, float operationalSecurity)
            => SabotageOutput(cellStrength, operationalSecurity, ResistanceMovementParams.Default);

        /// <summary>
        /// 弾圧への耐性（0..1）＝作戦保安 operationalSecurity×住民共感 populationSympathy の幾何平均×重み。
        /// 露見しにくく（保安）かつ民が匿う（共感）ほど弾圧を生き延びる（どちらか0なら耐えられない）。
        /// </summary>
        public static float RepressionResilience(float operationalSecurity, float populationSympathy, ResistanceMovementParams p)
        {
            float sec = Mathf.Clamp01(operationalSecurity);
            float sym = Mathf.Clamp01(populationSympathy);
            float geo = Mathf.Sqrt(sec * sym);
            return Mathf.Clamp01(geo * p.resilienceWeight);
        }

        public static float RepressionResilience(float operationalSecurity, float populationSympathy)
            => RepressionResilience(operationalSecurity, populationSympathy, ResistanceMovementParams.Default);

        /// <summary>
        /// 組織の士気（0..1）＝外部後ろ盾 externalBacking×戦果 sabotageOutput×解放の希望 liberationHope の重み付き和。
        /// 後ろ盾があり、活動が実り、解放が近いと感じられるほど結束する（孤立して無為なら士気は萎む）。
        /// </summary>
        public static float OrganizationMorale(float externalBacking, float sabotageOutput, float liberationHope, ResistanceMovementParams p)
        {
            float morale = Mathf.Clamp01(externalBacking) * p.moraleBackingWeight
                         + Mathf.Clamp01(sabotageOutput) * p.moraleSabotageWeight
                         + Mathf.Clamp01(liberationHope) * p.moraleHopeWeight;
            return Mathf.Clamp01(morale);
        }

        public static float OrganizationMorale(float externalBacking, float sabotageOutput, float liberationHope)
            => OrganizationMorale(externalBacking, sabotageOutput, liberationHope, ResistanceMovementParams.Default);

        /// <summary>
        /// 運動の総合勢力（0..1）＝細胞成長 cellGrowth×弾圧耐性 repressionResilience×士気 morale の幾何平均（三乗根）。
        /// 成長・耐性・士気の三本柱がそろうほど勢力が立つ＝どれか1本でも崩れると総合勢力は伸びない（幾何平均）。
        /// </summary>
        public static float MovementStrength(float cellGrowth, float repressionResilience, float morale, ResistanceMovementParams p)
        {
            float prod = Mathf.Clamp01(cellGrowth) * Mathf.Clamp01(repressionResilience) * Mathf.Clamp01(morale);
            float geo = Mathf.Pow(Mathf.Max(0f, prod), 1f / 3f);
            return Mathf.Clamp01(geo);
        }

        public static float MovementStrength(float cellGrowth, float repressionResilience, float morale)
            => MovementStrength(cellGrowth, repressionResilience, morale, ResistanceMovementParams.Default);

        /// <summary>既定の存続閾値（運動勢力×保安がこれ以上で抵抗運動は存続できる）。</summary>
        public const float DefaultViabilityThreshold = 0.25f;

        /// <summary>
        /// 抵抗運動が存続できるか＝運動勢力 movementStrength×作戦保安 operationalSecurity が threshold 以上。
        /// 勢力があっても露見すれば潰され、保安があっても勢力が無ければ消える＝両方が要る。
        /// </summary>
        public static bool IsResistanceViable(float movementStrength, float operationalSecurity, float threshold)
        {
            float t = Mathf.Clamp01(movementStrength) * Mathf.Clamp01(operationalSecurity);
            return t >= Mathf.Clamp01(threshold);
        }

        public static bool IsResistanceViable(float movementStrength, float operationalSecurity)
            => IsResistanceViable(movementStrength, operationalSecurity, DefaultViabilityThreshold);
    }
}
