using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 動員テンポのパラメータ（動員の速度・先手・不完全動員のリスクを支配する係数）。
    /// 全フィールド ctor で Clamp。盤面非依存・readonly。
    /// </summary>
    public readonly struct MobilizationTempoParams
    {
        /// <summary>不完全動員ペナルティの最大（進捗0で開戦したときの戦力低下＝0..1）。</summary>
        public readonly float maxPrematurePenalty;
        /// <summary>動員解除コストの基準（動員したぶんを平時に戻す摩擦＝後戻りしにくさ）。</summary>
        public readonly float demobilizationCostScale;
        /// <summary>完全動員とみなす既定しきい値（IsFullyMobilized の既定）。</summary>
        public readonly float fullThreshold;
        /// <summary>急速動員の経済負担スケール（動員率/経済余力 に乗じる）。</summary>
        public readonly float strainScale;

        public MobilizationTempoParams(
            float maxPrematurePenalty,
            float demobilizationCostScale,
            float fullThreshold,
            float strainScale)
        {
            this.maxPrematurePenalty = Mathf.Clamp01(maxPrematurePenalty);
            this.demobilizationCostScale = Mathf.Max(0f, demobilizationCostScale);
            this.fullThreshold = Mathf.Clamp01(fullThreshold);
            this.strainScale = Mathf.Max(0f, strainScale);
        }

        /// <summary>既定値：不完全ペナルティ最大0.6／解除コスト0.5／完全動員0.95／負担スケール1。</summary>
        public static MobilizationTempoParams Default
            => new MobilizationTempoParams(0.6f, 0.5f, 0.95f, 1f);
    }

    /// <summary>
    /// 動員の速度と先手（戦時体制への移行の速さ・純ロジック）。
    /// 平時から戦時へ＝予備役招集・生産転換・艦隊展開を進める「総動員の速度」に特化。
    /// 速く動員すれば先手を取れるが、不完全動員で開戦すると戦力が揃わず、完全動員には時間がかかる。
    /// 既存 <see cref="MobilizationRules"/>（徴募↔生産労働の競合＝動員「量」と産出/支持の綱引き）とは別責務、
    /// <c>ConscriptionRules</c>（個々の徴募）とも別＝こちらは総動員の「テンポ」。盤面非依存の plain 引数。
    /// 符号付き出力は -1..1。実効値パターン（基準値を破壊せず進捗で計算）。test-first。
    /// </summary>
    public static class MobilizationTempoRules
    {
        /// <summary>動員の進捗を dt ぶん進める＝clamp01(現在 + 動員率×dt)。残りは過剰分を捨てる。</summary>
        public static float MobilizationProgress(float currentMobilization, float mobilizationRate, float dt, MobilizationTempoParams p)
        {
            float cur = Mathf.Clamp01(currentMobilization);
            float rate = Mathf.Max(0f, mobilizationRate);
            float t = Mathf.Max(0f, dt);
            return Mathf.Clamp01(cur + rate * t);
        }

        public static float MobilizationProgress(float currentMobilization, float mobilizationRate, float dt)
            => MobilizationProgress(currentMobilization, mobilizationRate, dt, MobilizationTempoParams.Default);

        /// <summary>今動員できている戦力＝総戦力×進捗。</summary>
        public static float MobilizedStrength(float fullStrength, float mobilizationProgress)
            => Mathf.Max(0f, fullStrength) * Mathf.Clamp01(mobilizationProgress);

        /// <summary>目標水準まで動員する所要時間＝目標/動員能力（能力0以下なら到達不能＝無限大）。</summary>
        public static float MobilizationTime(float targetLevel, float mobilizationCapacity)
        {
            float target = Mathf.Clamp01(targetLevel);
            float cap = mobilizationCapacity;
            if (cap <= 0f)
                return Mathf.Infinity;
            return target / cap;
        }

        /// <summary>先に動員した側の優位（-1..1）＝自進捗−敵進捗。正＝自分が先行。</summary>
        public static float FirstMoverAdvantage(float ownMobilization, float enemyMobilization)
        {
            float own = Mathf.Clamp01(ownMobilization);
            float enemy = Mathf.Clamp01(enemyMobilization);
            return Mathf.Clamp(own - enemy, -1f, 1f);
        }

        /// <summary>不完全動員での開戦ペナルティ＝(1−進捗)×最大ペナルティ（0..maxPrematurePenalty）。進捗1で0。</summary>
        public static float PrematureWarPenalty(float mobilizationProgress, MobilizationTempoParams p)
        {
            float prog = Mathf.Clamp01(mobilizationProgress);
            return (1f - prog) * p.maxPrematurePenalty;
        }

        public static float PrematureWarPenalty(float mobilizationProgress)
            => PrematureWarPenalty(mobilizationProgress, MobilizationTempoParams.Default);

        /// <summary>急速動員が経済に与える負担＝動員率/max(eps,経済余力)×負担スケール。余力が薄いほど重い。下限0。</summary>
        public static float MobilizationStrain(float mobilizationRate, float economyCapacity, MobilizationTempoParams p)
        {
            float rate = Mathf.Max(0f, mobilizationRate);
            float cap = Mathf.Max(0.0001f, economyCapacity);
            return rate / cap * p.strainScale;
        }

        public static float MobilizationStrain(float mobilizationRate, float economyCapacity)
            => MobilizationStrain(mobilizationRate, economyCapacity, MobilizationTempoParams.Default);

        /// <summary>動員を解く（平時へ戻す）コスト＝進捗×解除コストスケール（動員したら後戻りしにくい）。下限0。</summary>
        public static float DemobilizationCost(float mobilizationProgress, MobilizationTempoParams p)
            => Mathf.Clamp01(mobilizationProgress) * p.demobilizationCostScale;

        public static float DemobilizationCost(float mobilizationProgress)
            => DemobilizationCost(mobilizationProgress, MobilizationTempoParams.Default);

        /// <summary>完全動員したか＝進捗 ≥ しきい値。</summary>
        public static bool IsFullyMobilized(float mobilizationProgress, float threshold)
            => Mathf.Clamp01(mobilizationProgress) >= Mathf.Clamp01(threshold);

        public static bool IsFullyMobilized(float mobilizationProgress)
            => IsFullyMobilized(mobilizationProgress, MobilizationTempoParams.Default.fullThreshold);
    }
}
