using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦時生産の調整値（#戦時生産）＝艦艇・兵器を量産する際の学習曲線・規模の経済・再編時間の係数。
    /// 量産が進むほど習熟で単価が下がり（学習曲線）、大ロットほど規模の経済が効き、品目を切り替えると再編に時間がかかる。
    /// すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct WarProductionParams
    {
        /// <summary>学習曲線の指数（累積生産が増すほど効率が上がる強さ。0=学習なし、大きいほど習熟が速い）。</summary>
        public readonly float learningExponent;
        /// <summary>学習の基準累積量（この量を超えたあたりで効率が目立ち始める＝学習の規模感）。</summary>
        public readonly float learningReference;
        /// <summary>学習効率の上限（習熟しても無限には上がらない）。</summary>
        public readonly float maxLearningEfficiency;
        /// <summary>規模の経済の指数（ロットが大きいほど効率が上がる強さ）。</summary>
        public readonly float scaleExponent;
        /// <summary>規模の経済の基準ロット（このロットあたりで規模効果が目立ち始める）。</summary>
        public readonly float scaleReference;
        /// <summary>規模の経済の上限（大量生産でも頭打ち）。</summary>
        public readonly float maxScaleFactor;
        /// <summary>再編の基礎時間（品目を変えなくても掛かる段取り）。</summary>
        public readonly float retoolingBase;
        /// <summary>変更量1あたりの再編追加時間（品目を大きく変えるほど長い）。</summary>
        public readonly float retoolingPerChange;
        /// <summary>生産が詰まっているとみなすボトルネック深刻度の既定閾値（0..1）。</summary>
        public readonly float bottleneckThreshold;

        public WarProductionParams(float learningExponent, float learningReference, float maxLearningEfficiency,
            float scaleExponent, float scaleReference, float maxScaleFactor,
            float retoolingBase, float retoolingPerChange, float bottleneckThreshold)
        {
            this.learningExponent = Mathf.Clamp(learningExponent, 0f, 1f);
            this.learningReference = Mathf.Max(0.01f, learningReference);
            this.maxLearningEfficiency = Mathf.Clamp(maxLearningEfficiency, 1f, 10f);
            this.scaleExponent = Mathf.Clamp(scaleExponent, 0f, 1f);
            this.scaleReference = Mathf.Max(0.01f, scaleReference);
            this.maxScaleFactor = Mathf.Clamp(maxScaleFactor, 1f, 10f);
            this.retoolingBase = Mathf.Max(0f, retoolingBase);
            this.retoolingPerChange = Mathf.Max(0f, retoolingPerChange);
            this.bottleneckThreshold = Mathf.Clamp01(bottleneckThreshold);
        }

        /// <summary>既定：学習指数0.15・学習基準100・学習上限3・規模指数0.1・規模基準10・規模上限2・再編基礎5・変更追加10・ボトルネック閾値0.7。</summary>
        public static WarProductionParams Default => new WarProductionParams(
            DefaultLearningExponent, DefaultLearningReference, DefaultMaxLearningEfficiency,
            DefaultScaleExponent, DefaultScaleReference, DefaultMaxScaleFactor,
            DefaultRetoolingBase, DefaultRetoolingPerChange, DefaultBottleneckThreshold);

        public const float DefaultLearningExponent = 0.15f;
        public const float DefaultLearningReference = 100f;
        public const float DefaultMaxLearningEfficiency = 3f;
        public const float DefaultScaleExponent = 0.1f;
        public const float DefaultScaleReference = 10f;
        public const float DefaultMaxScaleFactor = 2f;
        public const float DefaultRetoolingBase = 5f;
        public const float DefaultRetoolingPerChange = 10f;
        public const float DefaultBottleneckThreshold = 0.7f;
    }

    /// <summary>
    /// 戦時生産の純ロジック（#戦時生産・唯一の窓口）＝<b>戦時に艦艇・兵器を量産する</b>運用判断の数値。
    /// 生産は工場容量・資源・労働力の<b>最小律速</b>で決まり（<see cref="ProductionRate"/>）、量産が進むほど習熟で効率が上がり
    /// （学習曲線＝<see cref="LearningCurveEfficiency"/>）、大ロットほど規模の経済が効く（<see cref="EconomyOfScale"/>）。
    /// 最も不足する資源がボトルネックとなって全体を律速し（<see cref="BottleneckResource"/>）、品目の切替には再編時間が要る
    /// （<see cref="RetoolingDelay"/>）。量産は品質とのトレードオフを伴う（<see cref="ProductionVsQuality"/>）。
    /// <b>分担</b>：造船の完成・就役（<see cref="ShipyardRules"/>）や惑星の資源産出（<see cref="ResourceProductionRules"/>）とは別＝
    /// こちらは<b>戦時量産の学習曲線・規模の経済・再編・律速</b>そのものの係数。戦時の財政・動員（WarEconomyRules）とも別。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・乱数なし・LINQなし・test-first。
    /// </summary>
    public static class WarProductionRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 生産速度（最小律速） ──────────────────────────────────

        /// <summary>生産速度＝工場容量・資源・労働力のうち最も少ないもの（最小律速＝足りない要素が全体を縛る）。</summary>
        public static float ProductionRate(float factoryCapacity, float resourceAvailability, float laborForce)
            => ProductionRate(factoryCapacity, resourceAvailability, laborForce, WarProductionParams.Default);

        /// <summary>生産速度＝min(容量,資源,労働力)。負入力は0扱い・非負。</summary>
        public static float ProductionRate(float factoryCapacity, float resourceAvailability, float laborForce, WarProductionParams p)
        {
            float cap = Mathf.Max(0f, factoryCapacity);
            float res = Mathf.Max(0f, resourceAvailability);
            float lab = Mathf.Max(0f, laborForce);
            return Mathf.Min(cap, Mathf.Min(res, lab));
        }

        // ── 学習曲線（習熟効率） ──────────────────────────────────

        /// <summary>量産による習熟効率（1以上）＝累積生産が増すほど単価が下がり効率が上がる（学習曲線）。累積0で1.0。</summary>
        public static float LearningCurveEfficiency(float cumulativeProduction)
            => LearningCurveEfficiency(cumulativeProduction, WarProductionParams.Default);

        /// <summary>習熟効率＝pow((累積＋基準)/基準, 学習指数)。累積0で1.0、上限 maxLearningEfficiency。負累積は0扱い。</summary>
        public static float LearningCurveEfficiency(float cumulativeProduction, WarProductionParams p)
        {
            float cum = Mathf.Max(0f, cumulativeProduction);
            float ratio = (cum + p.learningReference) / p.learningReference;
            float eff = Mathf.Pow(ratio, p.learningExponent);
            return Mathf.Clamp(eff, 1f, p.maxLearningEfficiency);
        }

        // ── 実効生産 ──────────────────────────────────────────────

        /// <summary>習熟を加味した実効生産＝生産速度×習熟効率（学んだぶん多く作れる）。</summary>
        public static float EffectiveOutput(float productionRate, float learningCurveEfficiency)
            => Mathf.Max(0f, productionRate) * Mathf.Max(0f, learningCurveEfficiency);

        // ── ボトルネック資源（律速） ──────────────────────────────

        /// <summary>
        /// 最も不足する律速資源の添字＝各資源の(在庫/需要)比が最小のものを返す。需要0以下の資源は無視（律速にならない）。
        /// 配列 null/長さ不一致/空、または有効な資源が無ければ -1。
        /// </summary>
        public static int BottleneckResource(float[] resourceLevels, float[] demands)
        {
            if (resourceLevels == null || demands == null) return -1;
            int n = Mathf.Min(resourceLevels.Length, demands.Length);
            if (n <= 0) return -1;

            int worst = -1;
            float worstRatio = 0f;
            for (int i = 0; i < n; i++)
            {
                float demand = demands[i];
                if (demand <= 0f) continue; // 需要が無い資源は律速対象外
                float level = Mathf.Max(0f, resourceLevels[i]);
                float ratio = level / demand;
                if (worst < 0 || ratio < worstRatio)
                {
                    worst = i;
                    worstRatio = ratio;
                }
            }
            return worst;
        }

        // ── 規模の経済 ────────────────────────────────────────────

        /// <summary>大量生産の規模の経済（1以上）＝ロットが大きいほど効率が上がる。ロット0で1.0。</summary>
        public static float EconomyOfScale(float batchSize)
            => EconomyOfScale(batchSize, WarProductionParams.Default);

        /// <summary>規模の経済＝pow((ロット＋基準)/基準, 規模指数)。ロット0で1.0、上限 maxScaleFactor。負ロットは0扱い。</summary>
        public static float EconomyOfScale(float batchSize, WarProductionParams p)
        {
            float batch = Mathf.Max(0f, batchSize);
            float ratio = (batch + p.scaleReference) / p.scaleReference;
            float factor = Mathf.Pow(ratio, p.scaleExponent);
            return Mathf.Clamp(factor, 1f, p.maxScaleFactor);
        }

        // ── 再編時間（品目切替） ──────────────────────────────────

        /// <summary>生産品目の切り替えに要る再編時間＝基礎時間＋変更量×追加時間（大きく変えるほど長い）。</summary>
        public static float RetoolingDelay(float modelChange)
            => RetoolingDelay(modelChange, WarProductionParams.Default);

        /// <summary>再編時間＝retoolingBase＋clamp01(変更量)×retoolingPerChange。変更量は0..1にクランプ・非負。</summary>
        public static float RetoolingDelay(float modelChange, WarProductionParams p)
        {
            float change = Mathf.Clamp01(modelChange);
            return p.retoolingBase + change * p.retoolingPerChange;
        }

        // ── 量と品質のトレードオフ ────────────────────────────────

        /// <summary>
        /// 量産と品質のトレードオフ（符号付き -1..1）＝量産率が高いほど＋1（量重視）、低いほど−1（品質重視）。
        /// 量産率は0..1の正規化値。
        /// </summary>
        public static float ProductionVsQuality(float massProductionRate)
            => 2f * Mathf.Clamp01(massProductionRate) - 1f;

        // ── ボトルネック判定 ──────────────────────────────────────

        /// <summary>生産がボトルネックで詰まっているか＝深刻度が既定閾値 `bottleneckThreshold` 以上（bool）。</summary>
        public static bool IsProductionBottlenecked(float bottleneckSeverity)
            => IsProductionBottlenecked(bottleneckSeverity, WarProductionParams.Default.bottleneckThreshold);

        /// <summary>生産がボトルネックで詰まっているか＝深刻度が閾値以上（bool）。深刻度0..1想定。</summary>
        public static bool IsProductionBottlenecked(float bottleneckSeverity, float threshold)
            => Mathf.Clamp01(bottleneckSeverity) >= threshold;
    }
}
