using UnityEngine;

namespace Ginei
{
    /// <summary>監視（持続的な対象観察）の調整係数。</summary>
    public readonly struct SurveillanceParams
    {
        /// <summary>監視資産1単位あたりの継続性寄与（資産数を継続性へ写す係数）。</summary>
        public readonly float assetWeight;
        /// <summary>引き継ぎの質が継続性へ効く度合い（0..1）。</summary>
        public readonly float handoffWeight;
        /// <summary>観察時間の蓄積スケール（この時間で逓減が効き始める基準）。</summary>
        public readonly float timeScale;
        /// <summary>発覚リスクの鋭さ（近さ×警戒の積に対する増幅）。</summary>
        public readonly float exposureSharpness;
        /// <summary>蓄積情報量の上限（逓増だが頭打ち）。</summary>
        public readonly float intelCap;

        public SurveillanceParams(float assetWeight, float handoffWeight, float timeScale,
                                  float exposureSharpness, float intelCap)
        {
            this.assetWeight = Mathf.Clamp(assetWeight, 0f, 1f);
            this.handoffWeight = Mathf.Clamp01(handoffWeight);
            this.timeScale = Mathf.Max(0.01f, timeScale);
            this.exposureSharpness = Mathf.Clamp(exposureSharpness, 0f, 4f);
            this.intelCap = Mathf.Max(0f, intelCap);
        }

        /// <summary>既定＝資産重み0.25・引き継ぎ重み0.5・時間スケール60・発覚鋭さ1.0・蓄積上限1.0。</summary>
        public static SurveillanceParams Default => new SurveillanceParams(0.25f, 0.5f, 60f, 1f, 1f);
    }

    /// <summary>
    /// 監視（対象＝敵艦隊・要人・拠点の持続的観察と行動把握）の純ロジック。複数の監視資産を引き継ぎながら
    /// 継続的に張り付き、観察時間とともに対象の行動パターンを把握して情報を蓄積する。一方で監視の近さは
    /// 対象の警戒を呼んで発覚リスクを生み、警戒した対象は回避を試みて監視を撒く（離脱する）。監視が撒かれを
    /// 上回り続ける間だけ監視は機能する。倍率/0..1 のスカラを返すだけ（実効値パターン・基準非破壊）。
    /// 決定論（乱数なし）・配列入力は null/空安全。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SurveillanceRules
    {
        /// <summary>
        /// 監視の継続性（0..1）＝監視資産数(0..)×assetWeight で張り付きの厚みを出し、引き継ぎの質(0..1)で
        /// 途切れを補う。資産が多く引き継ぎが滑らかなほど切れ目のない監視になる。
        /// </summary>
        public static float SurveillanceContinuity(int assetCount, float handoffQuality, SurveillanceParams p)
        {
            int n = assetCount < 0 ? 0 : assetCount;
            float depth = Mathf.Clamp01(n * p.assetWeight);          // 資産の厚み 0..1
            float handoff = Mathf.Clamp01(handoffQuality);           // 引き継ぎの質 0..1
            // 厚みを土台に、引き継ぎの質で残りを底上げ
            return Mathf.Clamp01(depth + (1f - depth) * handoff * p.handoffWeight);
        }

        public static float SurveillanceContinuity(int assetCount, float handoffQuality)
            => SurveillanceContinuity(assetCount, handoffQuality, SurveillanceParams.Default);

        /// <summary>
        /// 行動パターンの把握（0..1）＝観察時間(0..)を timeScale で逓減飽和させ、監視の継続性(0..1)を掛ける。
        /// 長く張り付くほど読めるが頭打ち、継続性が低いと断片しか掴めない。
        /// </summary>
        public static float PatternRecognition(float observationTime, float surveillanceContinuity, SurveillanceParams p)
        {
            float t = Mathf.Max(0f, observationTime);
            float cont = Mathf.Clamp01(surveillanceContinuity);
            // 逓減飽和：t/(t+timeScale) ∈ [0,1)
            float saturate = t / (t + p.timeScale);
            return Mathf.Clamp01(saturate * cont);
        }

        public static float PatternRecognition(float observationTime, float surveillanceContinuity)
            => PatternRecognition(observationTime, surveillanceContinuity, SurveillanceParams.Default);

        /// <summary>
        /// 発覚リスク（0..1）＝監視の近さ(0..1)×対象の警戒(0..1) を exposureSharpness で増幅。近づくほど、
        /// 相手が警戒しているほどバレやすい。どちらかが0なら発覚しない。
        /// </summary>
        public static float ExposureRisk(float surveillanceProximity, float targetVigilance, SurveillanceParams p)
        {
            float prox = Mathf.Clamp01(surveillanceProximity);
            float vig = Mathf.Clamp01(targetVigilance);
            return Mathf.Clamp01(prox * vig * p.exposureSharpness);
        }

        public static float ExposureRisk(float surveillanceProximity, float targetVigilance)
            => ExposureRisk(surveillanceProximity, targetVigilance, SurveillanceParams.Default);

        /// <summary>
        /// 対象の監視回避の試み（0..1）＝対象の警戒(0..1)×発覚リスク(0..1)。警戒していて、かつ尾行に気づいた
        /// （発覚した）ときだけ本格的に巻こうとする。
        /// </summary>
        public static float TargetEvasion(float targetVigilance, float exposureRisk, SurveillanceParams p)
        {
            float vig = Mathf.Clamp01(targetVigilance);
            float exp = Mathf.Clamp01(exposureRisk);
            return Mathf.Clamp01(vig * exp);
        }

        public static float TargetEvasion(float targetVigilance, float exposureRisk)
            => TargetEvasion(targetVigilance, exposureRisk, SurveillanceParams.Default);

        /// <summary>
        /// 監視を撒く成功度（0..1）＝対象の回避/(回避+監視技量)。回避が技量を上回るほど撒かれる。両者0なら撒けない(0)。
        /// </summary>
        public static float ShakeOff(float targetEvasion, float surveillanceSkill, SurveillanceParams p)
        {
            float evade = Mathf.Clamp01(targetEvasion);
            float skill = Mathf.Clamp01(surveillanceSkill);
            float denom = evade + skill;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(evade / denom);
        }

        public static float ShakeOff(float targetEvasion, float surveillanceSkill)
            => ShakeOff(targetEvasion, surveillanceSkill, SurveillanceParams.Default);

        /// <summary>
        /// 蓄積情報量（0..intelCap）＝パターン把握(0..1)×観察時間の飽和で逓増、ただし intelCap で頭打ち。
        /// 長期監視ほど積み上がるが上限がある（同じ対象を見続けても得られる新情報は減る）。
        /// </summary>
        public static float IntelAccumulation(float patternRecognition, float observationTime, SurveillanceParams p)
        {
            float pat = Mathf.Clamp01(patternRecognition);
            float t = Mathf.Max(0f, observationTime);
            float saturate = t / (t + p.timeScale);          // 0..1 逓増飽和
            return Mathf.Clamp(pat * saturate * p.intelCap, 0f, p.intelCap);
        }

        public static float IntelAccumulation(float patternRecognition, float observationTime)
            => IntelAccumulation(patternRecognition, observationTime, SurveillanceParams.Default);

        /// <summary>
        /// 発覚による監視の破綻度（0..1）＝発覚リスクが高いほど対象が察知して行動を止め、監視が無価値になる。
        /// 発覚リスクをそのまま破綻度へ写す（リスク=破綻の度合い）。
        /// </summary>
        public static float CompromiseFromExposure(float exposureRisk, SurveillanceParams p)
        {
            return Mathf.Clamp01(exposureRisk);
        }

        public static float CompromiseFromExposure(float exposureRisk)
            => CompromiseFromExposure(exposureRisk, SurveillanceParams.Default);

        /// <summary>既定の有効判定閾値（蓄積が撒かれをこれ以上上回れば機能）。</summary>
        public const float DefaultEffectiveThreshold = 0.2f;

        /// <summary>
        /// 監視が機能しているか＝蓄積情報量(0..)が撒かれ度(0..1)を threshold 以上上回るなら true。
        /// 撒かれていても蓄積が十分なら（既に読み切っていれば）監視は機能とみなす。
        /// </summary>
        public static bool IsSurveillanceEffective(float intelAccumulation, float shakeOff, float threshold)
        {
            float intel = Mathf.Max(0f, intelAccumulation);
            float shake = Mathf.Clamp01(shakeOff);
            return (intel - shake) >= threshold;
        }

        public static bool IsSurveillanceEffective(float intelAccumulation, float shakeOff)
            => IsSurveillanceEffective(intelAccumulation, shakeOff, DefaultEffectiveThreshold);
    }
}
