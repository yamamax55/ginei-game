using UnityEngine;

namespace Ginei
{
    /// <summary>予備兵力投入（reserve deployment・決定的瞬間の切り札）の調整係数。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct ReserveDeploymentParams
    {
        /// <summary>予備の価値に「新鋭さ（疲弊なし）」が効く重み（0..1）。0なら兵力のみ・1なら新鋭さで価値が大きく上下。</summary>
        public readonly float freshnessWeight;
        /// <summary>早すぎる投入の無駄の最大幅（適時性0で投入したときの無駄上限）。</summary>
        public readonly float prematureWasteScale;
        /// <summary>遅すぎる投入の損失の最大幅（戦況が崩壊しきってからの投入で取り返せない損失上限）。</summary>
        public readonly float tooLateLossScale;
        /// <summary>予備を使い切った枯渇リスクの最大幅（次の危機/好機に対応不能になる度合い上限）。</summary>
        public readonly float exhaustionScale;
        /// <summary>投入すべきと判定する適時性の既定しきい値（これ以上で切り札を切る）。</summary>
        public readonly float commitThreshold;

        public ReserveDeploymentParams(float freshnessWeight, float prematureWasteScale,
                                       float tooLateLossScale, float exhaustionScale, float commitThreshold)
        {
            this.freshnessWeight = Mathf.Clamp01(freshnessWeight);
            this.prematureWasteScale = Mathf.Clamp01(prematureWasteScale);
            this.tooLateLossScale = Mathf.Clamp01(tooLateLossScale);
            this.exhaustionScale = Mathf.Clamp01(exhaustionScale);
            this.commitThreshold = Mathf.Clamp01(commitThreshold);
        }

        /// <summary>
        /// 既定＝新鋭さ重み0.5/早撃ち無駄上限0.6/遅延損失上限0.8/枯渇リスク上限0.7/投入しきい値0.5。
        /// </summary>
        public static ReserveDeploymentParams Default =>
            new ReserveDeploymentParams(0.5f, 0.6f, 0.8f, 0.7f, 0.5f);
    }

    /// <summary>
    /// 予備兵力の投入（reserve deployment・#予備投入）の純ロジック。会戦では予備（リザーブ）を温存し、
    /// <b>決定的な瞬間に投入して勝敗を決める</b>切り札。早すぎる投入は無駄になり、遅すぎると手遅れになる。
    /// 崩れかけた戦線の補強（守勢）にも、突破口を広げる拡張（攻勢）にも使える＝「いつ切るか」の判断。
    /// <see cref="ReinforcementRules"/>（増援＝盤外からの戦力到着・補充）とは別＝こちらは<b>手元に温存した予備をいつ切るか</b>。
    /// 盤面非依存の plain 引数（兵力/戦況は 0..1 正規化）。乱数なし決定論・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReserveDeploymentRules
    {
        /// <summary>
        /// 温存された予備の価値 0..1：兵力規模 reserveStrength を土台に、新鋭さ freshness（疲弊なし＝1）で上下させる。
        /// freshness=1 なら兵力満額の価値、freshness=0 なら freshnessWeight ぶん割り引いた価値（消耗した予備は切り札にならない）。
        /// 入力はいずれも 0..1。
        /// </summary>
        public static float ReserveValue(float reserveStrength, float freshness, ReserveDeploymentParams p)
        {
            float s = Mathf.Clamp01(reserveStrength);
            float f = Mathf.Clamp01(freshness);
            float factor = (1f - p.freshnessWeight) + p.freshnessWeight * f;
            return Mathf.Clamp01(s * factor);
        }

        public static float ReserveValue(float reserveStrength, float freshness)
            => ReserveValue(reserveStrength, freshness, ReserveDeploymentParams.Default);

        /// <summary>
        /// 投入の適時性 0..1：戦況の切迫度 battleCriticality と予備の価値 reserveValue の積。
        /// 危機（or 好機）が高まり、かつ温存した予備が新鋭なときに最も投入適時性が高い（決定的瞬間）。
        /// </summary>
        public static float CommitTiming(float battleCriticality, float reserveValue)
        {
            return Mathf.Clamp01(Mathf.Clamp01(battleCriticality) * Mathf.Clamp01(reserveValue));
        }

        /// <summary>
        /// 崩れかけた戦区を補強する効果 0..1（守勢の使い方）：戦区の不足 weakSectorDeficit を予備兵力 reserveStrength が
        /// どれだけ埋めるか。不足を上回れば 1（戦線を立て直す）、足りなければ埋めた割合に留まる。
        /// </summary>
        public static float ReinforceEffect(float reserveStrength, float weakSectorDeficit)
        {
            float s = Mathf.Clamp01(reserveStrength);
            float deficit = Mathf.Clamp01(weakSectorDeficit);
            if (deficit <= 0f) return 1f; // 不足が無ければ完全に支えられる
            return Mathf.Clamp01(s / deficit);
        }

        /// <summary>
        /// 好機（突破口）を拡張する効果 0..1（攻勢の使い方）：突破口 breakthroughOpening と予備兵力 reserveStrength の積。
        /// 突破口が開いていて（opening 大）かつ予備があるとき大きく戦果を拡張する。どちらか欠けると効果は薄い（乗算）。
        /// </summary>
        public static float ExploitEffect(float reserveStrength, float breakthroughOpening)
        {
            return Mathf.Clamp01(Mathf.Clamp01(reserveStrength) * Mathf.Clamp01(breakthroughOpening));
        }

        /// <summary>
        /// 早すぎる投入の無駄 0..1：適時性 commitTiming が低いうちに切り札を切ると無駄になる。
        /// 適時性が低いほど無駄が大きく（最大 prematureWasteScale）、適時性が満ちると無駄は 0。
        /// </summary>
        public static float PrematureCommitWaste(float commitTiming, ReserveDeploymentParams p)
        {
            float t = Mathf.Clamp01(commitTiming);
            return Mathf.Clamp01((1f - t) * p.prematureWasteScale);
        }

        public static float PrematureCommitWaste(float commitTiming)
            => PrematureCommitWaste(commitTiming, ReserveDeploymentParams.Default);

        /// <summary>
        /// 遅すぎて手遅れになる損失 0..1：戦況の切迫度 battleCriticality が高まりきってからの投入は取り返せない。
        /// 切迫度が高いほど（戦線が崩壊しきっているほど）損失が大きい（最大 tooLateLossScale）。
        /// </summary>
        public static float TooLateCommitLoss(float battleCriticality, ReserveDeploymentParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(battleCriticality) * p.tooLateLossScale);
        }

        public static float TooLateCommitLoss(float battleCriticality)
            => TooLateCommitLoss(battleCriticality, ReserveDeploymentParams.Default);

        /// <summary>
        /// 予備の枯渇リスク 0..1：予備を投入した割合 committedFraction が大きいほど、後がなくなる（次の危機/好機に対応不能）。
        /// 二乗で効く＝使い切り（フル投入）に近づくほど急にリスクが立ち上がる（exhaustionScale で上限を抑える）。
        /// </summary>
        public static float ReserveExhaustionRisk(float committedFraction, ReserveDeploymentParams p)
        {
            float c = Mathf.Clamp01(committedFraction);
            return Mathf.Clamp01(c * c * p.exhaustionScale);
        }

        public static float ReserveExhaustionRisk(float committedFraction)
            => ReserveExhaustionRisk(committedFraction, ReserveDeploymentParams.Default);

        /// <summary>
        /// 今、予備を投入すべきか：投入の適時性（切迫度×予備価値）が threshold 以上なら true（決定的瞬間に切り札を切る）。
        /// </summary>
        public static bool ShouldCommitReserve(float battleCriticality, float reserveValue, float threshold)
        {
            return CommitTiming(battleCriticality, reserveValue) >= Mathf.Clamp01(threshold);
        }

        /// <summary>既定しきい値（<see cref="ReserveDeploymentParams.commitThreshold"/>）での投入判定。</summary>
        public static bool ShouldCommitReserve(float battleCriticality, float reserveValue)
            => ShouldCommitReserve(battleCriticality, reserveValue, ReserveDeploymentParams.Default.commitThreshold);
    }
}
