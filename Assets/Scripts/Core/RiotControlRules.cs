using UnityEngine;

namespace Ginei
{
    /// <summary>暴動鎮圧の調整係数。</summary>
    public readonly struct RiotControlParams
    {
        /// <summary>群衆の激しさにおける規模の指数（規模の効きの非線形さ）。</summary>
        public readonly float crowdSizeExponent;
        /// <summary>群衆の激しさにおける激昂の重み。</summary>
        public readonly float agitationWeight;
        /// <summary>鎮圧力における装備の重み。</summary>
        public readonly float equipmentWeight;
        /// <summary>鎮圧力における訓練の重み。</summary>
        public readonly float trainingWeight;
        /// <summary>解散効果の最大（鎮圧が圧倒的でも瞬時には散らせない上限）。</summary>
        public readonly float dispersalCap;
        /// <summary>過剰使用が激昂を煽る強さ（鎮圧過剰時に火に油）。</summary>
        public readonly float escalationGain;
        /// <summary>過剰とみなす鎮圧力/激しさの比の閾値（これを超えると煽りが始まる）。</summary>
        public readonly float overforceThreshold;
        /// <summary>武力行使の犠牲への基礎係数。</summary>
        public readonly float casualtyScale;
        /// <summary>報道が反発を増幅する強さ。</summary>
        public readonly float backlashMediaGain;
        /// <summary>戒厳における外出禁止の重み。</summary>
        public readonly float curfewWeight;
        /// <summary>戒厳における軍展開の重み。</summary>
        public readonly float militaryWeight;

        public RiotControlParams(float crowdSizeExponent, float agitationWeight,
                                 float equipmentWeight, float trainingWeight,
                                 float dispersalCap, float escalationGain, float overforceThreshold,
                                 float casualtyScale, float backlashMediaGain,
                                 float curfewWeight, float militaryWeight)
        {
            this.crowdSizeExponent = Mathf.Max(0f, crowdSizeExponent);
            this.agitationWeight = Mathf.Max(0f, agitationWeight);
            this.equipmentWeight = Mathf.Max(0f, equipmentWeight);
            this.trainingWeight = Mathf.Max(0f, trainingWeight);
            this.dispersalCap = Mathf.Clamp01(dispersalCap);
            this.escalationGain = Mathf.Max(0f, escalationGain);
            this.overforceThreshold = Mathf.Max(0f, overforceThreshold);
            this.casualtyScale = Mathf.Max(0f, casualtyScale);
            this.backlashMediaGain = Mathf.Max(0f, backlashMediaGain);
            this.curfewWeight = Mathf.Max(0f, curfewWeight);
            this.militaryWeight = Mathf.Max(0f, militaryWeight);
        }

        /// <summary>既定＝規模指数0.7・激昂重み0.6／装備0.5・訓練0.5／解散上限0.9・煽り0.5・過剰閾1.5／犠牲0.5・報道1.5／外出禁止0.6・軍展開0.4。</summary>
        public static RiotControlParams Default
            => new RiotControlParams(0.7f, 0.6f, 0.5f, 0.5f, 0.9f, 0.5f, 1.5f, 0.5f, 1.5f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 暴動鎮圧の純ロジック＝騒乱への治安対応。群衆（規模×激昂）が騒乱の激しさを生み、
    /// 治安部隊（数×装備×訓練）が鎮圧力を出す。鎮圧力が激しさを上回れば群衆は解散へ向かうが、
    /// 核心＝**鎮圧が強すぎれば逆に激昂を煽る**（過剰使用＝武力の犠牲→報道→世論の反発）。
    /// 戒厳令（外出禁止×軍展開）は強力だが正統性を損なう劇薬。
    /// 乱数は使わず決定論・入力は全て clamp・符号付き出力は -1..1。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RiotControlRules
    {
        /// <summary>
        /// 騒乱の激しさ（0..1）＝群衆規模 crowdSize（0..1正規化）の指数乗 × 激昂 agitation（0..1）の加重。
        /// 規模だけでも激昂だけでも激しくはならない＝両者の積で立ち上がる。
        /// </summary>
        public static float CrowdIntensity(float crowdSize, float agitation, RiotControlParams p)
        {
            float size = Mathf.Pow(Mathf.Clamp01(crowdSize), p.crowdSizeExponent);
            float agit = 1f - p.agitationWeight + p.agitationWeight * Mathf.Clamp01(agitation);
            return Mathf.Clamp01(size * agit);
        }

        public static float CrowdIntensity(float crowdSize, float agitation)
            => CrowdIntensity(crowdSize, agitation, RiotControlParams.Default);

        /// <summary>
        /// 鎮圧力（0..1）＝部隊数 unitCount（0..1正規化）×（装備 equipment・訓練 training の加重平均）。
        /// 数だけ多くても装備・訓練が伴わなければ鎮圧力は出ない。
        /// </summary>
        public static float ControlForce(float unitCount, float equipment, float training, RiotControlParams p)
        {
            float wsum = p.equipmentWeight + p.trainingWeight;
            float quality = wsum > 0f
                ? (Mathf.Clamp01(equipment) * p.equipmentWeight + Mathf.Clamp01(training) * p.trainingWeight) / wsum
                : 0f;
            return Mathf.Clamp01(Mathf.Clamp01(unitCount) * quality);
        }

        public static float ControlForce(float unitCount, float equipment, float training)
            => ControlForce(unitCount, equipment, training, RiotControlParams.Default);

        /// <summary>
        /// 群衆解散の効果（0..1）＝鎮圧力 /（鎮圧力＋騒乱の激しさ）。拮抗で0.5・圧倒で上限へ。
        /// 鎮圧力も激しさもゼロなら解散しようがない＝0。上限は dispersalCap で頭打ち。
        /// </summary>
        public static float DispersalEffect(float controlForce, float crowdIntensity, RiotControlParams p)
        {
            float cf = Mathf.Clamp01(controlForce);
            float ci = Mathf.Clamp01(crowdIntensity);
            float denom = cf + ci;
            float ratio = denom > 0f ? cf / denom : 0f;
            return Mathf.Clamp01(Mathf.Min(ratio, p.dispersalCap));
        }

        public static float DispersalEffect(float controlForce, float crowdIntensity)
            => DispersalEffect(controlForce, crowdIntensity, RiotControlParams.Default);

        /// <summary>
        /// 過剰使用による激化（0..1）＝鎮圧力/激しさの比が過剰閾 overforceThreshold を超えた分 × 煽り係数。
        /// 適度な鎮圧は煽らない（0）。圧倒的すぎる弾圧ほど群衆の激昂を呼ぶ＝過剰使用のリスク。
        /// </summary>
        public static float ForceEscalation(float crowdIntensity, float controlForce, RiotControlParams p)
        {
            float ci = Mathf.Clamp01(crowdIntensity);
            float cf = Mathf.Clamp01(controlForce);
            if (ci <= 0f) return 0f;
            float ratio = cf / ci;
            float excess = Mathf.Max(0f, ratio - p.overforceThreshold);
            return Mathf.Clamp01(excess * p.escalationGain);
        }

        public static float ForceEscalation(float crowdIntensity, float controlForce)
            => ForceEscalation(crowdIntensity, controlForce, RiotControlParams.Default);

        /// <summary>
        /// 鎮圧による犠牲（0..1）＝武力 forceLevel × 群衆 crowdSize ×（1−自制 restraint）× 基礎係数。
        /// 自制が効けば犠牲は抑えられる。武力を振るうほど・群衆が多いほど犠牲は増える。
        /// </summary>
        public static float Casualties(float forceLevel, float crowdSize, float restraint, RiotControlParams p)
        {
            float f = Mathf.Clamp01(forceLevel);
            float c = Mathf.Clamp01(crowdSize);
            float r = 1f - Mathf.Clamp01(restraint);
            return Mathf.Clamp01(f * c * r * p.casualtyScale);
        }

        public static float Casualties(float forceLevel, float crowdSize, float restraint)
            => Casualties(forceLevel, crowdSize, restraint, RiotControlParams.Default);

        /// <summary>
        /// 世論の反発（0..1）＝犠牲 casualties ×（1＋報道 mediaExposure × 増幅係数）。
        /// 犠牲が無ければ反発も無い。同じ犠牲でも報道に晒されるほど反発は大きくなる。
        /// </summary>
        public static float PublicBacklash(float casualties, float mediaExposure, RiotControlParams p)
        {
            float cas = Mathf.Clamp01(casualties);
            float amp = 1f + Mathf.Clamp01(mediaExposure) * p.backlashMediaGain;
            return Mathf.Clamp01(cas * amp);
        }

        public static float PublicBacklash(float casualties, float mediaExposure)
            => PublicBacklash(casualties, mediaExposure, RiotControlParams.Default);

        /// <summary>
        /// 戒厳の鎮圧効果（0..1）＝外出禁止 curfewStrictness・軍展開 militaryPresence（各0..1）の加重平均。
        /// 街頭を封じ軍を出すほど騒乱を抑え込む（正統性の代償は別途・本式は鎮圧効果のみ）。
        /// </summary>
        public static float MartialLawEffect(float curfewStrictness, float militaryPresence, RiotControlParams p)
        {
            float wsum = p.curfewWeight + p.militaryWeight;
            if (wsum <= 0f) return 0f;
            float v = (Mathf.Clamp01(curfewStrictness) * p.curfewWeight
                       + Mathf.Clamp01(militaryPresence) * p.militaryWeight) / wsum;
            return Mathf.Clamp01(v);
        }

        public static float MartialLawEffect(float curfewStrictness, float militaryPresence)
            => MartialLawEffect(curfewStrictness, militaryPresence, RiotControlParams.Default);

        /// <summary>
        /// 暴動を鎮圧できたか（bool）＝解散効果 dispersalEffect が激化 forceEscalation を threshold 以上上回るか。
        /// 解散が激化を超えれば沈静化＝true。煽りすぎて解散が追いつかなければ false。
        /// </summary>
        public static bool IsRiotContained(float dispersalEffect, float forceEscalation, float threshold)
        {
            float disp = Mathf.Clamp01(dispersalEffect);
            float esc = Mathf.Clamp01(forceEscalation);
            return disp - esc >= threshold;
        }

        /// <summary>既定閾値0.1で委譲（解散が激化を僅差以上に上回れば鎮圧成功）。</summary>
        public static bool IsRiotContained(float dispersalEffect, float forceEscalation)
            => IsRiotContained(dispersalEffect, forceEscalation, 0.1f);
    }
}
