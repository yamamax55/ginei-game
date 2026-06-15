using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// プロスペクト理論の調整係数（KAHN-1 #1833・カーネマン＆トヴェルスキー）。
    /// 価値関数の凹凸（alpha）、損失回避係数（lambda≈2.25）、確率重み付けの曲率（beta）、
    /// 参照点の順応速度を持つ。コンストラクタで全フィールドを安全域へクランプ（実効値パターン）。
    /// </summary>
    public readonly struct ProspectParams
    {
        /// <summary>価値関数の指数（凹凸・0&lt;alpha≤1。1で線形、小さいほど逓減が強い）。</summary>
        public readonly float alpha;
        /// <summary>損失回避係数（同額の損失を利得より何倍重く感じるか・lambda≥1。既定≈2.25）。</summary>
        public readonly float lambda;
        /// <summary>確率重み付け関数の曲率（0&lt;beta≤1。小さいほど逆S字が強い＝低確率を過大評価）。</summary>
        public readonly float beta;
        /// <summary>参照点が最近の結果へ順応する速度（0..1。0で固定、1で即追従）。</summary>
        public readonly float adaptationRate;

        public ProspectParams(float alpha, float lambda, float beta, float adaptationRate)
        {
            this.alpha = Mathf.Clamp(alpha, 0.1f, 1f);
            this.lambda = Mathf.Max(1f, lambda);
            this.beta = Mathf.Clamp(beta, 0.1f, 1f);
            this.adaptationRate = Mathf.Clamp01(adaptationRate);
        }

        /// <summary>カーネマン＆トヴェルスキー(1992)の推定値に準ずる既定値。</summary>
        public static ProspectParams Default => new ProspectParams(0.88f, 2.25f, 0.61f, 0.3f);
    }

    /// <summary>
    /// プロスペクト理論の意思決定状態（KAHN-1 #1833・純データ）。
    /// 評価の基準となる参照点と、直近の結果を保持する。コンストラクタで参照点をクランプ。
    /// </summary>
    public readonly struct ProspectState
    {
        /// <summary>価値評価の基準点（この値からの変化で利得／損失が決まる）。</summary>
        public readonly float referencePoint;
        /// <summary>直近に観測した結果（参照点の順応に使う）。</summary>
        public readonly float lastOutcome;

        public ProspectState(float referencePoint, float lastOutcome)
        {
            // 参照点・直近結果は広域だが NaN/極端値を避けるため有限域へクランプ
            this.referencePoint = Mathf.Clamp(referencePoint, -1e9f, 1e9f);
            this.lastOutcome = Mathf.Clamp(lastOutcome, -1e9f, 1e9f);
        }
    }

    /// <summary>
    /// プロスペクト理論の純ロジック（KAHN-1 #1833・カーネマン）。損失回避と参照点依存＝
    /// 人は絶対量でなく参照点からの変化で価値を評価し、損失は同額の利得より約2倍重く感じ
    /// （損失回避）、利得局面ではリスク回避的・損失局面ではリスク選好的（確実な損を避けて賭ける）。
    /// 価値関数は利得で凹・損失で凸（lambda 倍の傾き）、決定重みは低確率を過大評価する逆S字。
    /// <para>分担：<see cref="NeedsRules"/>（マズロー欲求階層＝充足の絶対水準）とは別＝こちらは参照点からの
    /// 変化に対する価値関数。<see cref="NeedsRules.MoraleContribution"/> 系の士気寄与とも別系統。
    /// 提督AIの意思決定に損失回避を効かせる土台。同EPIC KAHN の FramingRules（提示枠＝同じ結果の見せ方）とも別。</para>
    /// 盤面非依存の plain 引数・決定論（乱数なし）・基準値非破壊（実効値パターン）。非 MonoBehaviour（test-first）。
    /// </summary>
    public static class ProspectRules
    {
        /// <summary>
        /// 参照点からの相対的な結果（変化量）。正＝利得、負＝損失、0＝参照点どおり。
        /// </summary>
        public static float RelativeOutcome(float absoluteValue, float referencePoint)
        {
            return absoluteValue - referencePoint;
        }

        /// <summary>
        /// 価値関数 v(x)。利得は凹（逓減）、損失は凸かつ lambda 倍の傾き（損失回避）。
        /// v(x) = x^alpha           (x ≥ 0)
        /// v(x) = -lambda * |x|^alpha (x &lt; 0)
        /// 符号は別管理し、Pow の基底は常に非負（|x|）にする。
        /// </summary>
        public static float PerceivedValue(float relativeOutcome, ProspectParams p)
        {
            float magnitude = Mathf.Abs(relativeOutcome);
            float curved = Mathf.Pow(magnitude, p.alpha);
            if (relativeOutcome >= 0f) return curved;          // 利得：凹
            return -p.lambda * curved;                          // 損失：凸＆lambda倍で重い
        }

        public static float PerceivedValue(float relativeOutcome) =>
            PerceivedValue(relativeOutcome, ProspectParams.Default);

        /// <summary>損失回避係数（同額の損失が利得より何倍重いか）。</summary>
        public static float LossAversionRatio(ProspectParams p) => p.lambda;

        public static float LossAversionRatio() => LossAversionRatio(ProspectParams.Default);

        /// <summary>
        /// リスク態度（-1..1）。利得局面（relativeOutcome&gt;0）はリスク回避的（正）、
        /// 損失局面（&lt;0）はリスク選好的（負）、参照点上（0）は中立。
        /// 変化の絶対値が大きいほど飽和して ±1 へ近づく（tanh の Pow 近似＝x/(1+|x|)）。
        /// </summary>
        public static float RiskAttitude(float relativeOutcome)
        {
            if (relativeOutcome > 0f)
            {
                float g = relativeOutcome;
                return Mathf.Clamp(g / (1f + g), 0f, 1f);       // 利得＝リスク回避（正）
            }
            if (relativeOutcome < 0f)
            {
                float l = -relativeOutcome;
                return Mathf.Clamp(-(l / (1f + l)), -1f, 0f);   // 損失＝リスク選好（負）
            }
            return 0f;
        }

        /// <summary>
        /// 決定重み w(prob)。低確率を過大評価・高確率を過小評価する逆S字。
        /// w(p) = p^beta / (p^beta + (1-p)^beta)^(1/beta)（KT1992 の重み付け関数）。
        /// 確率は 0..1 にクランプ。端点（0/1）はそのまま 0/1 を返す。
        /// </summary>
        public static float DecisionWeight(float probability, ProspectParams p)
        {
            float prob = Mathf.Clamp01(probability);
            if (prob <= 0f) return 0f;
            if (prob >= 1f) return 1f;

            float pb = Mathf.Pow(prob, p.beta);
            float qb = Mathf.Pow(1f - prob, p.beta);
            float denom = Mathf.Pow(pb + qb, 1f / p.beta);
            if (denom <= 0f) return prob;                        // ゼロ割回避（理論上起きない）
            return Mathf.Clamp01(pb / denom);
        }

        public static float DecisionWeight(float probability) =>
            DecisionWeight(probability, ProspectParams.Default);

        /// <summary>
        /// 見込み価値＝価値関数 × 決定重み。確率的な見込みに対する主観価値。
        /// </summary>
        public static float ProspectValue(float relativeOutcome, float probability, ProspectParams p)
        {
            return PerceivedValue(relativeOutcome, p) * DecisionWeight(probability, p);
        }

        public static float ProspectValue(float relativeOutcome, float probability) =>
            ProspectValue(relativeOutcome, probability, ProspectParams.Default);

        /// <summary>
        /// 保有効果。手放す痛みは損失として lambda 倍重い＝所有物の主観評価は名目より大きい。
        /// 名目価値 ownedValue（&lt;0 はクランプ）を損失フレームの価値関数に通した絶対値を返す。
        /// </summary>
        public static float EndowmentEffect(float ownedValue, ProspectParams p)
        {
            float owned = Mathf.Max(0f, ownedValue);
            // 手放す＝-owned の損失。その重さ（絶対値）が保有による主観的上乗せ価値。
            return Mathf.Abs(PerceivedValue(-owned, p));
        }

        public static float EndowmentEffect(float ownedValue) =>
            EndowmentEffect(ownedValue, ProspectParams.Default);

        /// <summary>
        /// 参照点の順応。直近の結果（recentOutcome）へ adaptationRate ぶん寄る＝
        /// 期待水準が現実に慣れてずれていく（昨日の利得が今日の基準になる）。
        /// </summary>
        public static float ReferencePointShift(float referencePoint, float recentOutcome, ProspectParams p)
        {
            return Mathf.Lerp(referencePoint, recentOutcome, p.adaptationRate);
        }

        public static float ReferencePointShift(float referencePoint, float recentOutcome) =>
            ReferencePointShift(referencePoint, recentOutcome, ProspectParams.Default);

        /// <summary>損失フレーム判定。参照点を下回れば（相対結果が負なら）損失フレーム。</summary>
        public static bool IsLossFrame(float relativeOutcome) => relativeOutcome < 0f;
    }
}
