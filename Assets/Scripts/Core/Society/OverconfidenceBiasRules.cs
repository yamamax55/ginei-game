using UnityEngine;

namespace Ginei
{
    /// <summary>過信バイアスの調整係数。</summary>
    public readonly struct OverconfidenceParams
    {
        /// <summary>過信が成功確率に与える最大の水増し幅（過信1.0で真の確率に最大これだけ上乗せ）。</summary>
        public readonly float maxInflation;
        /// <summary>過信が所要時間/コスト見積もりを削る最大割合（過信1.0で最大これだけ楽観＝倍率の下げ幅）。</summary>
        public readonly float maxPlanningDiscount;
        /// <summary>外部視点（基準率）で内部見積もりを引き戻す重み（0=内部のまま、1=完全に基準率）。</summary>
        public readonly float outsideViewWeight;
        /// <summary>フィードバックの質1.0で過信が校正される割合（質×これだけ過信が下がる）。</summary>
        public readonly float calibrationRate;

        public OverconfidenceParams(float maxInflation, float maxPlanningDiscount, float outsideViewWeight, float calibrationRate)
        {
            this.maxInflation = Mathf.Clamp01(maxInflation);
            this.maxPlanningDiscount = Mathf.Clamp01(maxPlanningDiscount);
            this.outsideViewWeight = Mathf.Clamp01(outsideViewWeight);
            this.calibrationRate = Mathf.Clamp01(calibrationRate);
        }

        /// <summary>既定＝水増し0.3・計画割引0.4・外部視点重み0.5・校正率0.5。</summary>
        public static OverconfidenceParams Default => new OverconfidenceParams(0.3f, 0.4f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 過信バイアスと計画錯誤の純ロジック（カーネマン）。AI/指揮官は自軍の成功確率を系統的に過大評価し、
    /// 所要時間・コスト・リスクを過小評価する（planning fallacy）。WYSIATI（見えるものが全て）で
    /// 都合の良い情報だけ拾い未知の要因を軽視する＝結果として本来見送るべき作戦に踏み込む。良いフィードバック
    /// （質の高い反省）で過信は校正され得る。基準値は非破壊＝主観倍率・主観確率を返す（実効値パターン）。
    /// 分担：<see cref="OperationPlanRules"/>（立案の質＝能力×準備、接敵後の陳腐化）とは別＝
    /// 見積もりそのものの楽観バイアスに特化／同EPIC KAHN の損失回避（ProspectRules）・判断ノイズ
    /// （JudgmentNoiseRules＝ランダムなばらつき）とは別＝過信は系統的バイアス。AIの作戦決定に
    /// 「踏み込みすぎ」を生む土台。盤面非依存の plain 引数。乱数なし・決定論（必要なら roll を渡す）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OverconfidenceBiasRules
    {
        /// <summary>
        /// 主観成功確率（0..1）＝真の確率を過信で水増し。過信0で真値、過信1で残り余地の maxInflation 分だけ
        /// 上へ寄せる（true + (1-true)×overconfidence×maxInflation＝天井1を超えない）。
        /// </summary>
        public static float InflatedSuccessEstimate(float trueSuccessChance, float overconfidence, OverconfidenceParams p)
        {
            float t = Mathf.Clamp01(trueSuccessChance);
            float oc = Mathf.Clamp01(overconfidence);
            return Mathf.Clamp01(t + (1f - t) * oc * p.maxInflation);
        }

        public static float InflatedSuccessEstimate(float trueSuccessChance, float overconfidence)
            => InflatedSuccessEstimate(trueSuccessChance, overconfidence, OverconfidenceParams.Default);

        /// <summary>
        /// 計画錯誤倍率（&lt;1＝楽観）。所要時間/コストを過信で過小に見積もる係数＝1-過信×maxPlanningDiscount。
        /// この倍率を真の所要に掛けたものが主観見積もり＝過信が高いほど短く安く見える。
        /// </summary>
        public static float PlanningFallacyFactor(float overconfidence, OverconfidenceParams p)
        {
            float oc = Mathf.Clamp01(overconfidence);
            return Mathf.Clamp01(1f - oc * p.maxPlanningDiscount);
        }

        public static float PlanningFallacyFactor(float overconfidence)
            => PlanningFallacyFactor(overconfidence, OverconfidenceParams.Default);

        /// <summary>主観と現実のギャップ（過信の代償の素）。負＝楽観し過ぎ、0＝正確。絶対値で乖離の大きさ。</summary>
        public static float EstimateGap(float inflatedEstimate, float trueValue)
        {
            return inflatedEstimate - trueValue;
        }

        /// <summary>
        /// WYSIATI による未知の軽視度（0..1）。見えない要因の重み unknownsWeight を過信が割り引いて無視する＝
        /// unknownsWeight×過信（過信が高いほど未知を視野から外す）。返り値が大きいほど未知を軽んじている。
        /// </summary>
        public static float WysiatiNeglect(float unknownsWeight, float overconfidence)
        {
            return Mathf.Clamp01(Mathf.Clamp01(unknownsWeight) * Mathf.Clamp01(overconfidence));
        }

        /// <summary>
        /// 外部視点（基準率）補正。内部の楽観見積もりを過去の基準率へ outsideViewWeight で引き戻す＝
        /// Lerp(inside, baseRate, weight)。同種作戦の実績（baseRate）を混ぜるほど楽観が抑えられる。
        /// </summary>
        public static float OutsideViewCorrection(float insideEstimate, float baseRate, OverconfidenceParams p)
        {
            return Mathf.Lerp(insideEstimate, baseRate, p.outsideViewWeight);
        }

        public static float OutsideViewCorrection(float insideEstimate, float baseRate)
            => OutsideViewCorrection(insideEstimate, baseRate, OverconfidenceParams.Default);

        /// <summary>
        /// 踏み込み判定。主観成功確率が閾値を超えれば作戦に踏み切る＝過信で水増しした確率が高いほど
        /// 本来見送るべき賭けに突っ込む（呼び出し側は InflatedSuccessEstimate の値を渡す）。
        /// </summary>
        public static bool OverreachDecision(float inflatedSuccessEstimate, float threshold)
        {
            return inflatedSuccessEstimate >= threshold;
        }

        /// <summary>
        /// フィードバックで校正された過信（0..1）。失敗の反省＝feedbackQuality(0..1)×calibrationRate だけ
        /// 過信が下がる。質の悪い反省（≈0）では過信は維持される＝痛い目を正しく学ぶと過信は和らぐ。
        /// </summary>
        public static float ConfidenceCalibration(float overconfidence, float feedbackQuality, OverconfidenceParams p)
        {
            float oc = Mathf.Clamp01(overconfidence);
            float reduction = Mathf.Clamp01(feedbackQuality) * p.calibrationRate;
            return Mathf.Clamp01(oc * (1f - reduction));
        }

        public static float ConfidenceCalibration(float overconfidence, float feedbackQuality)
            => ConfidenceCalibration(overconfidence, feedbackQuality, OverconfidenceParams.Default);

        /// <summary>
        /// 過信の代償。主観と現実のギャップ（楽観なら負）の大きさ×賭け金 stakes＝見積もりを外した分だけ高くつく。
        /// 方向を問わない（過小評価でも過大評価でも乖離はコスト）ので絶対値で取る。
        /// </summary>
        public static float CostOfOverconfidence(float estimateGap, float stakes)
        {
            return Mathf.Abs(estimateGap) * Mathf.Max(0f, stakes);
        }

        /// <summary>過信が閾値を超えるか（系統的バイアスが効いている水準か）。</summary>
        public static bool IsOverconfident(float overconfidence, float threshold)
        {
            return Mathf.Clamp01(overconfidence) > threshold;
        }
    }
}
