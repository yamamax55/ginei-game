using UnityEngine;

namespace Ginei
{
    /// <summary>国勢調査・統計精度の調整係数（可視性の維持コストと盲目のペナルティ）。</summary>
    public readonly struct CensusParams
    {
        /// <summary>調査投資1.0あたりの可視性上昇速度（per 時間単位）。</summary>
        public readonly float investGain;
        /// <summary>社会変化1.0あたりの可視性の陳腐化速度（統計は生もの＝放置で古びる）。</summary>
        public readonly float decayRate;
        /// <summary>可視性ゼロ時の人口推定誤差の最大割合（±maxError まで外す）。</summary>
        public readonly float maxError;
        /// <summary>可視性ゼロでも徴収できる最低限の徴税効率（見えなくても関所・現物徴発ぶんは取れる）。</summary>
        public readonly float minTaxEfficiency;
        /// <summary>可視性ゼロ時の政策失敗リスクの上限（盲目の政府は的を外す）。</summary>
        public readonly float maxMisfireRisk;
        /// <summary>可視性ゼロ時に国家から見えない人口割合の上限（闇経済・未登録）。</summary>
        public readonly float maxShadow;
        /// <summary>監視への反発が始まる可視性の閾値（これ以下なら社会は気にしない）。</summary>
        public readonly float surveillanceThreshold;

        public CensusParams(float investGain, float decayRate, float maxError,
                            float minTaxEfficiency, float maxMisfireRisk, float maxShadow,
                            float surveillanceThreshold)
        {
            this.investGain = Mathf.Max(0f, investGain);
            this.decayRate = Mathf.Max(0f, decayRate);
            this.maxError = Mathf.Max(0f, maxError);
            this.minTaxEfficiency = Mathf.Clamp01(minTaxEfficiency);
            this.maxMisfireRisk = Mathf.Clamp01(maxMisfireRisk);
            this.maxShadow = Mathf.Clamp01(maxShadow);
            // 1.0 ちょうどだと反発が定義できない（超過幅0除算）ため 0.99 を上限にクランプ
            this.surveillanceThreshold = Mathf.Clamp(surveillanceThreshold, 0f, 0.99f);
        }

        /// <summary>既定＝投資上昇0.2/陳腐化0.1/誤差±50%/最低徴税効率0.3/政策失敗上限0.8/闇人口上限0.4/監視閾値0.6。</summary>
        public static CensusParams Default => new CensusParams(0.2f, 0.1f, 0.5f, 0.3f, 0.8f, 0.4f, 0.6f);
    }

    /// <summary>
    /// 国勢調査・統計精度の純ロジック（唯一の窓口）。国家が自国をどれだけ「見えて」いるか＝可視性 legibility(0..1) を扱う。
    /// <see cref="DemographicsRules"/> が<b>実際の人口動態</b>（真値）を回すのに対し、こちらは<b>政府の認識</b>＝真値とのズレを出す
    /// （統計が粗いと徴税・徴募・政策が外れる＝見えない国は治められない）。可視性は調査投資で上がり社会変化で陳腐化する
    /// （統計は生もの）。一方で可視性が高すぎると監視への反発を生む＝<b>見えない国は治められないが、見えすぎる国は息が詰まる</b>。
    /// 推定は乱数を持たず外から与える roll で決定論的に解決（<see cref="ReconRules.EstimateStrength"/> と同型のバイアス構造）。
    /// 真値は非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CensusRules
    {
        /// <summary>
        /// 可視性の時間更新。調査投資 censusInvestment(0..1) で上がり、社会の変化速度 socialChange(0..1) で陳腐化する
        /// （投資を止めた統計は社会の変化に置き去りにされる）。結果は 0..1 にクランプ。dt は負を許さない。
        /// </summary>
        public static float LegibilityTick(float legibility, float censusInvestment, float socialChange, float dt, CensusParams p)
        {
            float l = Mathf.Clamp01(legibility);
            float invest = Mathf.Clamp01(censusInvestment);
            float change = Mathf.Clamp01(socialChange);
            float delta = (p.investGain * invest - p.decayRate * change) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(l + delta);
        }

        public static float LegibilityTick(float legibility, float censusInvestment, float socialChange, float dt)
            => LegibilityTick(legibility, censusInvestment, socialChange, dt, CensusParams.Default);

        /// <summary>人口推定誤差の割合（0..maxError）。legibility=1 で 0（正確）、legibility=0 で maxError（最大）。</summary>
        public static float ErrorFraction(float legibility, CensusParams p)
        {
            return p.maxError * (1f - Mathf.Clamp01(legibility));
        }

        public static float ErrorFraction(float legibility) => ErrorFraction(legibility, CensusParams.Default);

        /// <summary>
        /// 政府が見ている人口（点推定）。biasRoll∈[-1,1] で真値を ±ErrorFraction の幅でずらす
        /// （roll=0 で真値・+1 で過大の上端・-1 で過小の下端）。負にはならない。真値 truePopulation は非破壊。
        /// </summary>
        public static float PerceivedPopulation(float truePopulation, float legibility, float biasRoll, CensusParams p)
        {
            float err = ErrorFraction(legibility, p);
            float b = Mathf.Clamp(biasRoll, -1f, 1f);
            return Mathf.Max(0f, Mathf.Max(0f, truePopulation) * (1f + err * b));
        }

        public static float PerceivedPopulation(float truePopulation, float legibility, float biasRoll)
            => PerceivedPopulation(truePopulation, legibility, biasRoll, CensusParams.Default);

        /// <summary>
        /// 徴税効率 0..1。見えない富からは取れない＝可視性に比例して minTaxEfficiency〜1 を線形補間
        /// （legibility=0 でも最低限は取れる＝関所・現物徴発）。財政(#163)の歳入係数に掛ける想定。
        /// </summary>
        public static float TaxCollectionEfficiency(float legibility, CensusParams p)
        {
            return p.minTaxEfficiency + (1f - p.minTaxEfficiency) * Mathf.Clamp01(legibility);
        }

        public static float TaxCollectionEfficiency(float legibility)
            => TaxCollectionEfficiency(legibility, CensusParams.Default);

        /// <summary>政策失敗リスク 0..maxMisfireRisk。統計が粗いほど政策は的を外す（legibility=1 で 0）。</summary>
        public static float PolicyMisfireRisk(float legibility, CensusParams p)
        {
            return p.maxMisfireRisk * (1f - Mathf.Clamp01(legibility));
        }

        public static float PolicyMisfireRisk(float legibility) => PolicyMisfireRisk(legibility, CensusParams.Default);

        /// <summary>政策判定。roll∈[0,1) が失敗リスク未満なら失敗＝true（決定論）。</summary>
        public static bool PolicyMisfires(float legibility, float roll, CensusParams p)
        {
            return roll < PolicyMisfireRisk(legibility, p);
        }

        public static bool PolicyMisfires(float legibility, float roll)
            => PolicyMisfires(legibility, roll, CensusParams.Default);

        /// <summary>
        /// 国家に見えない人口割合 0..maxShadow（闇経済・未登録＝動員も課税も届かない）。
        /// legibility=0 で maxShadow、legibility=1 で 0。徴募(#96)・動員の母数を割り引く想定。
        /// </summary>
        public static float ShadowPopulation(float legibility, CensusParams p)
        {
            return p.maxShadow * (1f - Mathf.Clamp01(legibility));
        }

        public static float ShadowPopulation(float legibility) => ShadowPopulation(legibility, CensusParams.Default);

        /// <summary>
        /// 監視への反発 0..1（可視性のコスト＝見られすぎる社会は息が詰まる）。可視性が surveillanceThreshold を
        /// 超えた割合に、自由主義的な社会ほど強く反発する libertySentiment(0..1) を掛ける。
        /// 閾値以下なら 0＝ほどほどの統計は嫌われない。安定度(#109)・支持(#113)から引く想定。
        /// </summary>
        public static float SurveillanceResentment(float legibility, float libertySentiment, CensusParams p)
        {
            float l = Mathf.Clamp01(legibility);
            if (l <= p.surveillanceThreshold) return 0f;
            float excess = (l - p.surveillanceThreshold) / (1f - p.surveillanceThreshold);
            return Mathf.Clamp01(excess * Mathf.Clamp01(libertySentiment));
        }

        public static float SurveillanceResentment(float legibility, float libertySentiment)
            => SurveillanceResentment(legibility, libertySentiment, CensusParams.Default);
    }
}
