using UnityEngine;

namespace Ginei
{
    /// <summary>宮廷陰謀（讒言・権謀術数）の調整係数。</summary>
    public readonly struct CourtIntrigueParams
    {
        /// <summary>讒言の効果における君主の暗愚さの重み（1で信憑性をそのまま増幅）。</summary>
        public readonly float gullibilityWeight;
        /// <summary>共謀者1人あたりの陰謀の精巧さ寄与（人数が多いほど精巧だが脆い）。</summary>
        public readonly float complexityPerConspirator;
        /// <summary>連携の低さでも残る精巧さの下限（0..1・連携skill=0でもこの割合は維持）。</summary>
        public readonly float coordinationFloor;
        /// <summary>精巧さが露見リスクを下げる強さ（0..1・1で精巧さ満点なら丸ごと隠せる）。</summary>
        public readonly float complexityConceals;
        /// <summary>共謀者1人あたりの露見リスク上積み（口が増えるほど漏れる）。</summary>
        public readonly float headcountRisk;
        /// <summary>敵の警戒が露見リスクを押し上げる重み。</summary>
        public readonly float vigilanceWeight;
        /// <summary>失脚工作における政敵の隙の重み（讒言が刺さる土壌）。</summary>
        public readonly float vulnerabilityWeight;
        /// <summary>宮廷の立ち回りにおける与党人脈の重み（政治力に人脈を掛ける）。</summary>
        public readonly float networkWeight;
        /// <summary>反動（墓穴）における君主の不興の重み。</summary>
        public readonly float displeasureWeight;
        /// <summary>正味成果における立ち回りの寄与重み（失脚は等倍・立ち回りはこの倍率）。</summary>
        public readonly float maneuverGainWeight;
        /// <summary>陰謀成功とみなす正味成果の既定閾値。</summary>
        public readonly float successThreshold;

        public CourtIntrigueParams(float gullibilityWeight, float complexityPerConspirator,
                                   float coordinationFloor, float complexityConceals,
                                   float headcountRisk, float vigilanceWeight,
                                   float vulnerabilityWeight, float networkWeight,
                                   float displeasureWeight, float maneuverGainWeight,
                                   float successThreshold)
        {
            this.gullibilityWeight = Mathf.Max(0f, gullibilityWeight);
            this.complexityPerConspirator = Mathf.Max(0f, complexityPerConspirator);
            this.coordinationFloor = Mathf.Clamp01(coordinationFloor);
            this.complexityConceals = Mathf.Clamp01(complexityConceals);
            this.headcountRisk = Mathf.Max(0f, headcountRisk);
            this.vigilanceWeight = Mathf.Max(0f, vigilanceWeight);
            this.vulnerabilityWeight = Mathf.Max(0f, vulnerabilityWeight);
            this.networkWeight = Mathf.Max(0f, networkWeight);
            this.displeasureWeight = Mathf.Max(0f, displeasureWeight);
            this.maneuverGainWeight = Mathf.Max(0f, maneuverGainWeight);
            this.successThreshold = Mathf.Clamp01(successThreshold);
        }

        /// <summary>
        /// 既定＝暗愚1.0・精巧さ0.2/人＋連携下限0.4・精巧さ隠蔽0.3・口リスク0.1/人・警戒0.6・
        /// 政敵の隙1.0・人脈0.5・不興1.0・立ち回り寄与0.4・成功閾値0.3。
        /// </summary>
        public static CourtIntrigueParams Default
            => new CourtIntrigueParams(1.0f, 0.2f, 0.4f, 0.3f, 0.1f, 0.6f, 1.0f, 0.5f, 1.0f, 0.4f, 0.3f);
    }

    /// <summary>
    /// 宮廷陰謀（讒言・権謀術数）の純ロジック＝門閥貴族・帝国宮廷の暗闘。
    /// 君主の寵を巡る暗闘を数式で扱う：讒言（中傷）は信憑性と君主の暗愚さで刺さるが、
    /// 標的の声望が高いほど跳ね返される。陰謀は共謀者を増やすほど精巧になる一方、
    /// 口が増えるほど露見し（精巧さは隠すが人数と敵の警戒は暴く）、露見すれば君主の不興で
    /// 反動（墓穴）を招く。失脚工作は讒言×政敵の隙で決まり、宮廷の立ち回りは政治力×人脈で稼ぐ。
    /// 正味成果＝失脚＋立ち回り−反動（-1..1）で、閾値を越えれば陰謀成功。
    /// 乱数は使わず決定論。実効値パターン（引数は全て clamp）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CourtIntrigueRules
    {
        /// <summary>
        /// 讒言の効果（0..1）＝信憑性 plausibility × 君主の暗愚さ gullibility ÷（信憑性＋標的の声望 targetReputation）。
        /// 君主が暗愚なほど中傷を真に受け、標的の声望が高いほど中傷は薄まり跳ね返される。
        /// 分母が0（信憑性も声望も0）のときは効果0。
        /// </summary>
        public static float SlanderEffect(float plausibility, float sovereignGullibility, float targetReputation, CourtIntrigueParams p)
        {
            float pl = Mathf.Clamp01(plausibility);
            float gu = Mathf.Clamp01(sovereignGullibility);
            float rep = Mathf.Clamp01(targetReputation);
            float denom = pl + rep;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(pl * gu * p.gullibilityWeight / denom);
        }

        public static float SlanderEffect(float plausibility, float sovereignGullibility, float targetReputation)
            => SlanderEffect(plausibility, sovereignGullibility, targetReputation, CourtIntrigueParams.Default);

        /// <summary>
        /// 陰謀の精巧さ（0..1）＝共謀者数×1人あたり寄与（規模）に、連携 coordinationSkill による
        /// 仕上げ（下限 floor〜1）を掛ける。人数が多いほど精巧だが、後段で露見もしやすくなる。
        /// </summary>
        public static float SchemeComplexity(float conspiratorCount, float coordinationSkill, CourtIntrigueParams p)
        {
            float scale = Mathf.Clamp01(Mathf.Max(0f, conspiratorCount) * p.complexityPerConspirator);
            float coord = Mathf.Lerp(p.coordinationFloor, 1f, Mathf.Clamp01(coordinationSkill));
            return Mathf.Clamp01(scale * coord);
        }

        public static float SchemeComplexity(float conspiratorCount, float coordinationSkill)
            => SchemeComplexity(conspiratorCount, coordinationSkill, CourtIntrigueParams.Default);

        /// <summary>
        /// 露見リスク（0..1）＝（共謀者数×口リスク＋敵の警戒×重み）を、精巧さ schemeComplexity が隠す。
        /// 人数と敵の警戒は暴く方向、精巧さは（1−精巧さ×隠蔽係数）で抑える方向に働く。
        /// </summary>
        public static float ExposureRisk(float schemeComplexity, float conspiratorCount, float enemyVigilance, CourtIntrigueParams p)
        {
            float raw = Mathf.Max(0f, conspiratorCount) * p.headcountRisk
                        + Mathf.Clamp01(enemyVigilance) * p.vigilanceWeight;
            float conceal = 1f - Mathf.Clamp01(schemeComplexity) * p.complexityConceals;
            return Mathf.Clamp01(raw * conceal);
        }

        public static float ExposureRisk(float schemeComplexity, float conspiratorCount, float enemyVigilance)
            => ExposureRisk(schemeComplexity, conspiratorCount, enemyVigilance, CourtIntrigueParams.Default);

        /// <summary>
        /// 失脚工作の成否（0..1）＝讒言の効果 slanderEffect × 政敵の隙 rivalVulnerability（の重み付き）。
        /// 中傷が刺さっても政敵に隙が無ければ失脚しない（隙という土壌に讒言の種が落ちて初めて実る）。
        /// </summary>
        public static float RivalDownfall(float slanderEffect, float rivalVulnerability, CourtIntrigueParams p)
        {
            float vuln = Mathf.Clamp01(Mathf.Clamp01(rivalVulnerability) * p.vulnerabilityWeight);
            return Mathf.Clamp01(Mathf.Clamp01(slanderEffect) * vuln);
        }

        public static float RivalDownfall(float slanderEffect, float rivalVulnerability)
            => RivalDownfall(slanderEffect, rivalVulnerability, CourtIntrigueParams.Default);

        /// <summary>
        /// 宮廷の立ち回り（0..1）＝政治力 politicalSkill × 与党人脈 allyNetwork（の重み付き）。
        /// 人脈の薄い切れ者は宮廷で空回りする＝政治力と人脈は掛け算（どちらか欠けると伸びない）。
        /// </summary>
        public static float CourtManeuvering(float politicalSkill, float allyNetwork, CourtIntrigueParams p)
        {
            float net = Mathf.Clamp01(Mathf.Clamp01(allyNetwork) * p.networkWeight);
            return Mathf.Clamp01(Mathf.Clamp01(politicalSkill) * net);
        }

        public static float CourtManeuvering(float politicalSkill, float allyNetwork)
            => CourtManeuvering(politicalSkill, allyNetwork, CourtIntrigueParams.Default);

        /// <summary>
        /// 陰謀の反動＝墓穴（0..1）＝露見リスク exposureRisk × 君主の不興 sovereignDispleasure（の重み付き）。
        /// 露見しても君主が不興でなければ咎められず、不興でも露見しなければ反動は無い（両方揃って墓穴）。
        /// </summary>
        public static float Backfire(float exposureRisk, float sovereignDispleasure, CourtIntrigueParams p)
        {
            float disp = Mathf.Clamp01(Mathf.Clamp01(sovereignDispleasure) * p.displeasureWeight);
            return Mathf.Clamp01(Mathf.Clamp01(exposureRisk) * disp);
        }

        public static float Backfire(float exposureRisk, float sovereignDispleasure)
            => Backfire(exposureRisk, sovereignDispleasure, CourtIntrigueParams.Default);

        /// <summary>
        /// 陰謀の正味成果（-1..1）＝失脚工作 rivalDownfall（等倍）＋立ち回り courtManeuvering（重み付き）−反動 backfire。
        /// 政敵を蹴落とし宮廷で立ち回って得たものから、墓穴の損失を差し引く。墓穴が勝てば負値（自滅）。
        /// </summary>
        public static float NetIntrigueGain(float rivalDownfall, float courtManeuvering, float backfire, CourtIntrigueParams p)
        {
            float gain = Mathf.Clamp01(rivalDownfall)
                         + Mathf.Clamp01(courtManeuvering) * p.maneuverGainWeight
                         - Mathf.Clamp01(backfire);
            return Mathf.Clamp(gain, -1f, 1f);
        }

        public static float NetIntrigueGain(float rivalDownfall, float courtManeuvering, float backfire)
            => NetIntrigueGain(rivalDownfall, courtManeuvering, backfire, CourtIntrigueParams.Default);

        /// <summary>陰謀が成功したか＝正味成果が閾値を上回るなら true。</summary>
        public static bool IsIntrigueSuccessful(float netIntrigueGain, float threshold)
        {
            return Mathf.Clamp(netIntrigueGain, -1f, 1f) > Mathf.Clamp01(threshold);
        }

        public static bool IsIntrigueSuccessful(float netIntrigueGain)
            => IsIntrigueSuccessful(netIntrigueGain, CourtIntrigueParams.Default.successThreshold);
    }
}
