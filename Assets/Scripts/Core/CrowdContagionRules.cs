using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 群衆感染（群衆化の相転移）の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。
    /// </summary>
    public readonly struct CrowdContagionParams
    {
        /// <summary>群衆強度の合成で密度に掛ける重み。</summary>
        public readonly float densityWeight;
        /// <summary>群衆強度の合成で感情的興奮に掛ける重み。</summary>
        public readonly float arousalWeight;
        /// <summary>群衆強度の合成で匿名性に掛ける重み。</summary>
        public readonly float anonymityWeight;
        /// <summary>理性低下の最大幅（群衆強度1で理性がこのぶん落ちる・0..1）。</summary>
        public readonly float maxRationalityDrop;
        /// <summary>相転移ヒステリシスの戻り幅（群衆化は崩れにくい＝この幅だけ閾値を下げて戻りにくくする・0..1）。</summary>
        public readonly float hysteresisGap;

        public CrowdContagionParams(float densityWeight, float arousalWeight, float anonymityWeight,
            float maxRationalityDrop, float hysteresisGap)
        {
            this.densityWeight = Mathf.Max(0f, densityWeight);
            this.arousalWeight = Mathf.Max(0f, arousalWeight);
            this.anonymityWeight = Mathf.Max(0f, anonymityWeight);
            this.maxRationalityDrop = Mathf.Clamp01(maxRationalityDrop);
            this.hysteresisGap = Mathf.Clamp01(hysteresisGap);
        }

        /// <summary>既定の調整値（密度/興奮/匿名性を均等重み・理性は最大8割落ち・ヒステリシスで戻り閾値を0.2下げる）。</summary>
        public static CrowdContagionParams Default => new CrowdContagionParams(
            densityWeight: 1f,
            arousalWeight: 1f,
            anonymityWeight: 1f,
            maxRationalityDrop: 0.8f,
            hysteresisGap: 0.2f);
    }

    /// <summary>
    /// 群衆心理＝群衆化の相転移と集団精神（CRWD-1 #1820・ル・ボン『群衆心理』参考・純ロジック test-first）。
    /// 個人が群衆に溶け込むと「集団精神」が立ち上がり、被暗示性が上がり理性が下がる（個↔群の level-shift）。
    /// 密度・感情的興奮・匿名性が閾値を超えると相転移し、個人は群衆の一部として振る舞う。
    /// 役割分担：<see cref="ManiaRules"/>＝信念の SIR 感染（感受性/感染/回復の数理）とは別＝こちらは
    /// 「個から群への相転移」と「集団精神の立ち上がり」を担う。<see cref="PropagandaRules"/>（発信側の到達×信用）とも別。
    /// 同 EPIC CRWD の PanicCascadeRules/CrowdReversalRules の土台＝本クラスが crowdIntensity を供給する。
    /// 全入力クランプ・乱数なし決定論・盤面非依存の plain 引数。Game層非依存＝Core 純ロジック。
    /// </summary>
    public static class CrowdContagionRules
    {
        /// <summary>
        /// 群衆強度(0..1)：密度×感情的興奮×匿名性を重み付き平均で1つの強度に束ねる。
        /// どれも高いほど群衆として燃え、どれかが欠ければ全体の熱が下がる。重み和0なら0。
        /// </summary>
        public static float CrowdIntensity(float density, float emotionalArousal, float anonymity, CrowdContagionParams p)
        {
            float d = Mathf.Clamp01(density);
            float a = Mathf.Clamp01(emotionalArousal);
            float an = Mathf.Clamp01(anonymity);
            float wd = p.densityWeight;
            float wa = p.arousalWeight;
            float wan = p.anonymityWeight;
            float wsum = wd + wa + wan;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((d * wd + a * wa + an * wan) / wsum);
        }

        /// <summary>群衆強度が閾値を超えると個→群へ相転移（true＝群衆化）。</summary>
        public static bool PhaseTransition(float crowdIntensity, float threshold)
        {
            return Mathf.Clamp01(crowdIntensity) > Mathf.Clamp01(threshold);
        }

        /// <summary>
        /// 被暗示性(0..1)：群衆強度が高いほど暗示にかかりやすくなる。
        /// 単独より急に上がるよう、低強度では緩く高強度で跳ねる二次の立ち上がり（intensity^1.5 近似＝√×自身）。
        /// </summary>
        public static float Suggestibility(float crowdIntensity)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            // c^1.5 = c * sqrt(c)：log/exp を使わない代数近似。
            return Mathf.Clamp01(c * Mathf.Sqrt(c));
        }

        /// <summary>
        /// 理性の低下幅(0..1)：群衆強度が高いほど個人の理性が下がる。
        /// 最大幅は p.maxRationalityDrop。群衆強度に線形で効く（強度1で最大ぶん落ちる）。
        /// </summary>
        public static float RationalityDrop(float crowdIntensity, CrowdContagionParams p)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            return Mathf.Clamp01(c * p.maxRationalityDrop);
        }

        /// <summary>
        /// 集団精神の立ち上がり(0..1)：群衆強度に共通の焦点（sharedFocus）が乗ると強い。
        /// 焦点が無ければ単なる雑踏（弱い）、焦点があると一つの心として束ねられる＝強度×焦点。
        /// </summary>
        public static float CollectiveMind(float crowdIntensity, float sharedFocus)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            float f = Mathf.Clamp01(sharedFocus);
            return Mathf.Clamp01(c * f);
        }

        /// <summary>
        /// 個の埋没度(0..1)：群衆強度が高いほど個が溶けるが、強い自我は溶けにくい。
        /// individualIdentity（自我の強さ）が抵抗＝強度×(1−自我)。強い自我は群衆に呑まれにくい。
        /// </summary>
        public static float IndividualSubmersion(float crowdIntensity, float individualIdentity)
        {
            float c = Mathf.Clamp01(crowdIntensity);
            float id = Mathf.Clamp01(individualIdentity);
            return Mathf.Clamp01(c * (1f - id));
        }

        /// <summary>
        /// 相転移のヒステリシス：いま群衆状態にあるかどうかで使う閾値が変わる（群衆化は崩れにくい）。
        /// 非群衆→群衆は素の threshold を超える必要があるが、群衆→非群衆は threshold−hysteresisGap まで
        /// 下がらないと戻らない＝一度群衆化すると低い熱でも維持される。戻り閾値は0未満にしない。
        /// </summary>
        public static bool LevelShiftHysteresis(bool currentlyCrowd, float intensity, float threshold, CrowdContagionParams p)
        {
            float c = Mathf.Clamp01(intensity);
            float up = Mathf.Clamp01(threshold);
            if (currentlyCrowd)
            {
                float down = Mathf.Max(0f, up - p.hysteresisGap);   // 戻りは低い閾値＝崩れにくい
                return c > down;
            }
            return c > up;
        }

        /// <summary>
        /// 感染しやすさ(0..1)：被暗示性が高いほど感染し、批判的思考が抗う。
        /// suggestibility×(1−criticalThinking)＝暗示にかかりやすく、かつ批判力が弱いほど感染する。
        /// </summary>
        public static float ContagionSusceptibility(float suggestibility, float criticalThinking)
        {
            float s = Mathf.Clamp01(suggestibility);
            float ct = Mathf.Clamp01(criticalThinking);
            return Mathf.Clamp01(s * (1f - ct));
        }

        /// <summary>群衆状態にあるか＝群衆強度が閾値を超えている（<see cref="PhaseTransition"/> と同義の判定窓口）。</summary>
        public static bool IsCrowdState(float crowdIntensity, float threshold)
        {
            return Mathf.Clamp01(crowdIntensity) > Mathf.Clamp01(threshold);
        }

        // ---- Default 委譲版オーバーロード ----

        /// <summary>既定 Params で群衆強度を出す委譲版。</summary>
        public static float CrowdIntensity(float density, float emotionalArousal, float anonymity)
            => CrowdIntensity(density, emotionalArousal, anonymity, CrowdContagionParams.Default);

        /// <summary>既定 Params で理性低下幅を出す委譲版。</summary>
        public static float RationalityDrop(float crowdIntensity)
            => RationalityDrop(crowdIntensity, CrowdContagionParams.Default);

        /// <summary>既定 Params で相転移ヒステリシスを判定する委譲版。</summary>
        public static bool LevelShiftHysteresis(bool currentlyCrowd, float intensity, float threshold)
            => LevelShiftHysteresis(currentlyCrowd, intensity, threshold, CrowdContagionParams.Default);
    }
}
