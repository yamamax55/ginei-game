using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 人質交換（政治的人質の授受と信義）のパラメータ。基準値は ctor で全クランプ。
    /// </summary>
    public readonly struct HostageExchangeParams
    {
        /// <summary>政治的地位が人質価値へ効く重み。</summary>
        public readonly float rankWeight;
        /// <summary>情的絆が人質価値へ効く重み。</summary>
        public readonly float tiesWeight;
        /// <summary>同盟の担保力を測る既定しきい値（これ未満は安定の担保に足りない）。</summary>
        public readonly float guaranteeThreshold;
        /// <summary>出す側の不安が安定を崩す既定しきい値（これ超で不安定化）。</summary>
        public readonly float anxietyThreshold;
        /// <summary>殺害威嚇の逆効果の最大強度（信義喪失・敵の結束の上限）。</summary>
        public readonly float backlashCeiling;

        public HostageExchangeParams(
            float rankWeight,
            float tiesWeight,
            float guaranteeThreshold,
            float anxietyThreshold,
            float backlashCeiling)
        {
            // 価値の重みは正・上限つき。
            this.rankWeight = Mathf.Clamp(rankWeight, 0.1f, 2f);
            this.tiesWeight = Mathf.Clamp(tiesWeight, 0.1f, 2f);
            this.guaranteeThreshold = Mathf.Clamp01(guaranteeThreshold);
            this.anxietyThreshold = Mathf.Clamp01(anxietyThreshold);
            this.backlashCeiling = Mathf.Clamp01(backlashCeiling);
        }

        /// <summary>既定値（地位/絆を均等重み・担保しきい0.5・不安しきい0.6・逆効果上限1.0）。</summary>
        public static HostageExchangeParams Default =>
            new HostageExchangeParams(0.5f, 0.5f, 0.5f, 0.6f, 1f);
    }

    /// <summary>
    /// 人質交換（政治的人質の授受と信義）の純ロジック（盤面非依存・plain引数）。
    /// 政治的人質（王族・要人）の価値、人質による同盟の担保、待遇、取る側の信義、出す側の不安、
    /// 解放の条件、殺害威嚇のてことその逆効果をモデル化する。
    ///
    /// 分担：
    /// - 預かる側の「信義」(captorReliability) が担保力を上げ／出す側の不安を下げる軸。
    /// - 殺害威嚇は短期のてこ（leverage）になるが、実行すれば逆効果（backlash）で信義喪失・敵結束。
    /// すべて Mathf のみ・LINQ/乱数なし・実効値パターン（入力は非破壊）。入力は全て clamp。
    /// </summary>
    public static class HostageExchangeRules
    {
        // ---- 人質の価値 ----

        /// <summary>政治的地位×情的絆で人質の価値（0..1）。Params版。</summary>
        public static float HostageValue(float politicalRank, float emotionalTies, HostageExchangeParams p)
        {
            float rank = Mathf.Clamp01(politicalRank);
            float ties = Mathf.Clamp01(emotionalTies);
            // 重み付き和を重み総和で正規化＝両者が高いほど価値が高い。
            float wSum = p.rankWeight + p.tiesWeight;
            if (wSum <= 0.0001f) return 0f;
            float v = (rank * p.rankWeight + ties * p.tiesWeight) / wSum;
            return Mathf.Clamp01(v);
        }

        /// <summary>人質の価値（既定Params）。</summary>
        public static float HostageValue(float politicalRank, float emotionalTies) =>
            HostageValue(politicalRank, emotionalTies, HostageExchangeParams.Default);

        // ---- 同盟の担保 ----

        /// <summary>人質価値×預かる側の信義で同盟の担保力（0..1）。Params版。</summary>
        public static float AllianceGuarantee(float hostageValue, float captorReliability, HostageExchangeParams p)
        {
            float v = Mathf.Clamp01(hostageValue);
            float rel = Mathf.Clamp01(captorReliability);
            // 価値が高くても信義が無ければ担保にならない＝積。
            return Mathf.Clamp01(v * rel);
        }

        /// <summary>同盟の担保力（既定Params）。</summary>
        public static float AllianceGuarantee(float hostageValue, float captorReliability) =>
            AllianceGuarantee(hostageValue, captorReliability, HostageExchangeParams.Default);

        // ---- 待遇 ----

        /// <summary>預かる側の度量×人質価値で待遇（0..1＝高いほど厚遇）。Params版。</summary>
        public static float HostageTreatment(float captorMagnanimity, float hostageValue, HostageExchangeParams p)
        {
            float mag = Mathf.Clamp01(captorMagnanimity);
            float v = Mathf.Clamp01(hostageValue);
            // 度量が基調・価値の高い人質はさらに丁重に扱われる（平均で底上げ）。
            return Mathf.Clamp01(Mathf.Lerp(mag, 1f, v) * mag);
        }

        /// <summary>待遇（既定Params）。</summary>
        public static float HostageTreatment(float captorMagnanimity, float hostageValue) =>
            HostageTreatment(captorMagnanimity, hostageValue, HostageExchangeParams.Default);

        // ---- 出す側の不安 ----

        /// <summary>人質価値×(1-信義)で出す側の不安（0..1）。Params版。</summary>
        public static float HostageGiverAnxiety(float hostageValue, float captorReliability, HostageExchangeParams p)
        {
            float v = Mathf.Clamp01(hostageValue);
            float rel = Mathf.Clamp01(captorReliability);
            // 大事な人質ほど・相手が信用ならぬほど不安が大きい。
            return Mathf.Clamp01(v * (1f - rel));
        }

        /// <summary>出す側の不安（既定Params）。</summary>
        public static float HostageGiverAnxiety(float hostageValue, float captorReliability) =>
            HostageGiverAnxiety(hostageValue, captorReliability, HostageExchangeParams.Default);

        // ---- 解放の条件 ----

        /// <summary>義務履行×相互信頼で解放の条件充足（0..1）。Params版。</summary>
        public static float ReleaseCondition(float obligationFulfilled, float mutualTrust, HostageExchangeParams p)
        {
            float obl = Mathf.Clamp01(obligationFulfilled);
            float trust = Mathf.Clamp01(mutualTrust);
            // 義務が果たされ・信頼が成れば解放へ＝積（どちらか欠けると進まない）。
            return Mathf.Clamp01(obl * trust);
        }

        /// <summary>解放の条件充足（既定Params）。</summary>
        public static float ReleaseCondition(float obligationFulfilled, float mutualTrust) =>
            ReleaseCondition(obligationFulfilled, mutualTrust, HostageExchangeParams.Default);

        // ---- 殺害威嚇のてこ ----

        /// <summary>人質価値×威嚇の信憑で殺害威嚇のてこ（0..1）。Params版。</summary>
        public static float ExecutionThreatLeverage(float hostageValue, float threatCredibility, HostageExchangeParams p)
        {
            float v = Mathf.Clamp01(hostageValue);
            float cred = Mathf.Clamp01(threatCredibility);
            // 価値の高い人質を・本気で殺すと信じさせるほど短期のてこになる。
            return Mathf.Clamp01(v * cred);
        }

        /// <summary>殺害威嚇のてこ（既定Params）。</summary>
        public static float ExecutionThreatLeverage(float hostageValue, float threatCredibility) =>
            ExecutionThreatLeverage(hostageValue, threatCredibility, HostageExchangeParams.Default);

        // ---- 威嚇の逆効果 ----

        /// <summary>
        /// 威嚇が強いほど（殺せば）逆効果（0..1＝信義喪失・敵の結束）。Params版。
        /// てこが強いほど実行時の反動も大きい＝上限は backlashCeiling。
        /// </summary>
        public static float ThreatBacklash(float executionThreatLeverage, HostageExchangeParams p)
        {
            float lev = Mathf.Clamp01(executionThreatLeverage);
            return Mathf.Clamp01(lev * p.backlashCeiling);
        }

        /// <summary>威嚇の逆効果（既定Params）。</summary>
        public static float ThreatBacklash(float executionThreatLeverage) =>
            ThreatBacklash(executionThreatLeverage, HostageExchangeParams.Default);

        // ---- 安定判定 ----

        /// <summary>
        /// 人質交換が安定か。担保力がしきい以上・かつ出す側の不安がしきい以下なら安定。
        /// guaranteeThreshold / anxietyThreshold を任意指定。
        /// </summary>
        public static bool IsExchangeStable(
            float allianceGuarantee, float hostageGiverAnxiety,
            float guaranteeThreshold, float anxietyThreshold)
        {
            float g = Mathf.Clamp01(allianceGuarantee);
            float a = Mathf.Clamp01(hostageGiverAnxiety);
            return g >= guaranteeThreshold && a <= anxietyThreshold;
        }

        /// <summary>人質交換が安定か（既定Paramsのしきい値）。</summary>
        public static bool IsExchangeStable(float allianceGuarantee, float hostageGiverAnxiety)
        {
            var p = HostageExchangeParams.Default;
            return IsExchangeStable(allianceGuarantee, hostageGiverAnxiety, p.guaranteeThreshold, p.anxietyThreshold);
        }
    }
}
