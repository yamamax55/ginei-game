using UnityEngine;

namespace Ginei
{
    /// <summary>テラフォーミングの調整係数。</summary>
    public readonly struct TerraformingParams
    {
        /// <summary>投資による進捗の速度係数（投資1.0×改造可能性1.0あたりの進捗/時間）。</summary>
        public readonly float investProgressRate;
        /// <summary>環境の巻き返し速度（進捗/時間・投資が途絶えると進捗が逆行する基準値）。</summary>
        public readonly float regressionRate;
        /// <summary>改造可能な過酷さの上限（hostility がこれ以上の星は改造不能＝進捗ゼロ・費用無限大）。</summary>
        public readonly float maxHostility;
        /// <summary>居住可能化の進捗閾値（progress がこれ以上で habitable）。</summary>
        public readonly float habitableThreshold;
        /// <summary>「手を付けている」と見なす最小投資（これ未満は放棄扱い＝環境が巻き返す）。</summary>
        public readonly float minActiveInvestment;

        public TerraformingParams(float investProgressRate, float regressionRate,
            float maxHostility, float habitableThreshold, float minActiveInvestment)
        {
            this.investProgressRate = Mathf.Max(0f, investProgressRate);
            this.regressionRate = Mathf.Max(0f, regressionRate);
            this.maxHostility = Mathf.Clamp01(maxHostility);
            this.habitableThreshold = Mathf.Clamp01(habitableThreshold);
            this.minActiveInvestment = Mathf.Clamp01(minActiveInvestment);
        }

        /// <summary>既定＝投資進捗係数0.1・巻き返し0.01/時間・過酷さ上限0.9・居住可能閾値1.0・最小投資0.05。</summary>
        public static TerraformingParams Default => new TerraformingParams(0.1f, 0.01f, 0.9f, 1f, 0.05f);
    }

    /// <summary>
    /// テラフォーミングの純ロジック。非居住可能星系（hostility 0..1＝環境の過酷さ）への長期投資で
    /// 居住可能（habitable）化＝入植先そのものを作る。「過酷な星ほど遅く・高くつく」「途中放棄は
    /// 自然に巻き戻される（完成した星は自立した生態系＝退行しない）」を式に出す。
    /// 居住可能星系への入植（<see cref="ColonizationRules"/> #129）の<b>前段＝星造り</b>で別系統：
    /// こちらが habitable を作り、入植はその habitable な星に住む。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TerraformingRules
    {
        /// <summary>
        /// 改造可能性（0..1）。過酷さが増すほど線形に下がり、上限（maxHostility）以上は0＝改造不能。
        /// 進捗速度・費用の双方に効く＝「過酷な星ほど遅く・高くつく」の源泉。
        /// </summary>
        public static float Feasibility(float hostility, TerraformingParams p)
        {
            if (p.maxHostility <= 0f) return 0f;
            return Mathf.Clamp01(1f - Mathf.Clamp01(hostility) / p.maxHostility);
        }

        public static float Feasibility(float hostility) => Feasibility(hostility, TerraformingParams.Default);

        /// <summary>
        /// テラフォーミングの1tick後の進捗（0..1）。投資が最小投資（minActiveInvestment）以上なら
        /// 投資×係数×改造可能性 のぶん進む（過酷なほど遅い・改造不能の星は進まない）。
        /// 未満は放棄扱い＝環境の巻き返しでわずかに逆行する（過酷な星ほど巻き返しも強い）。
        /// </summary>
        public static float ProgressTick(float progress, float investment, float hostility, float dt, TerraformingParams p)
        {
            float prog = Mathf.Clamp01(progress);
            float inv = Mathf.Clamp01(investment);
            float host = Mathf.Clamp01(hostility);
            float time = Mathf.Max(0f, dt);
            if (inv >= p.minActiveInvestment)
            {
                return Mathf.Clamp01(prog + inv * p.investProgressRate * Feasibility(host, p) * time);
            }
            return Mathf.Clamp01(prog - p.regressionRate * (1f + host) * time);
        }

        public static float ProgressTick(float progress, float investment, float hostility, float dt)
            => ProgressTick(progress, investment, hostility, dt, TerraformingParams.Default);

        /// <summary>居住可能化したか＝進捗が閾値（habitableThreshold）以上。成立後は <see cref="StarSystem.habitable"/> を立てて入植（<see cref="ColonizationRules"/>）へ引き継ぐ想定。</summary>
        public static bool IsHabitable(float progress, TerraformingParams p)
        {
            return Mathf.Clamp01(progress) >= p.habitableThreshold;
        }

        public static bool IsHabitable(float progress) => IsHabitable(progress, TerraformingParams.Default);

        /// <summary>
        /// 完了までの総投資目安（投資1.0を続けた場合の所要時間＝残り進捗／（係数×改造可能性））。
        /// 過酷な星ほど改造可能性が低く高くつき、改造不能（可能性0）は無限大＝どれだけ注いでも星にならない。
        /// 完了済み（閾値以上）は0。
        /// </summary>
        public static float CostToComplete(float progress, float hostility, TerraformingParams p)
        {
            float remaining = p.habitableThreshold - Mathf.Clamp01(progress);
            if (remaining <= 0f) return 0f;
            float rate = p.investProgressRate * Feasibility(hostility, p);
            if (rate <= 0f) return float.PositiveInfinity;
            return remaining / rate;
        }

        public static float CostToComplete(float progress, float hostility)
            => CostToComplete(progress, hostility, TerraformingParams.Default);

        /// <summary>
        /// 放棄後の進捗退行（自然が取り戻す）。途中放棄は放置時間×巻き返し速度のぶん巻き戻される（0で下限クランプ）。
        /// 完成済み（居住可能化＝閾値以上）の星は自立した生態系＝退行しない＝据え置き。
        /// </summary>
        public static float AbandonmentRegression(float progress, float neglectDuration, TerraformingParams p)
        {
            float prog = Mathf.Clamp01(progress);
            if (IsHabitable(prog, p)) return prog;
            return Mathf.Clamp01(prog - p.regressionRate * Mathf.Max(0f, neglectDuration));
        }

        public static float AbandonmentRegression(float progress, float neglectDuration)
            => AbandonmentRegression(progress, neglectDuration, TerraformingParams.Default);
    }
}
