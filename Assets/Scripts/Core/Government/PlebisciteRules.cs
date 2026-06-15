using UnityEngine;

namespace Ginei
{
    /// <summary>住民投票の調整係数（バーラト和約型＝領土・体制の一回性投票）。</summary>
    public readonly struct PlebisciteParams
    {
        /// <summary>可決の閾値（賛成票シェアがこれ以上で成立）。</summary>
        public readonly float passThreshold;
        /// <summary>不公正が動員側へ票を振らせる最大幅（公正0のとき動員努力がこれだけ票を作る）。</summary>
        public readonly float riggingSwing;
        /// <summary>監視団1単位が公正度を底上げする量。</summary>
        public readonly float observerEffect;
        /// <summary>「禍根が残る」とみなす票差の閾値（これ未満の僅差×低公正＝結果が呪われる）。</summary>
        public readonly float contestedMargin;

        public PlebisciteParams(float passThreshold, float riggingSwing, float observerEffect, float contestedMargin)
        {
            this.passThreshold = Mathf.Clamp01(passThreshold);
            this.riggingSwing = Mathf.Clamp01(riggingSwing);
            this.observerEffect = Mathf.Clamp01(observerEffect);
            this.contestedMargin = Mathf.Clamp01(contestedMargin);
        }

        /// <summary>既定＝可決閾値0.5・不正振れ幅0.3・監視効果0.1/団・禍根余白0.1。</summary>
        public static PlebisciteParams Default => new PlebisciteParams(0.5f, 0.3f, 0.1f, 0.1f);
    }

    /// <summary>
    /// 住民投票の純ロジック（バーラト和約型）。領土帰属・体制選択を票で決める一回性の投票＝
    /// 票は「本当の支持」に「動員×不公正」のバイアスが乗って出てくる。監視団は公正度を底上げし、
    /// 結果の正統性は「公正さ×票差」で決まる＝不正くさい僅差の勝利は勝っても呪われる（禍根）。
    /// 定例選挙（<see cref="PartyRules"/>）とは別系統。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PlebisciteRules
    {
        /// <summary>監視団による実効公正度（0..1）＝素の公正度＋監視団数×効果。</summary>
        public static float EffectiveFairness(float baseFairness, int observers, PlebisciteParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(baseFairness) + Mathf.Max(0, observers) * p.observerEffect);
        }

        public static float EffectiveFairness(float baseFairness, int observers)
            => EffectiveFairness(baseFairness, observers, PlebisciteParams.Default);

        /// <summary>
        /// 開票結果の賛成シェア（0..1）＝本当の支持 genuineSupport に、不公正分（1−公正度）×動員努力
        /// mobilization(-1..1＝賛成側+/反対側−) の振れを乗せる。公正な投票は民意がそのまま出る。
        /// </summary>
        public static float VoteShare(float genuineSupport, float mobilization, float fairness, PlebisciteParams p)
        {
            float rig = (1f - Mathf.Clamp01(fairness)) * Mathf.Clamp(mobilization, -1f, 1f) * p.riggingSwing;
            return Mathf.Clamp01(Mathf.Clamp01(genuineSupport) + rig);
        }

        public static float VoteShare(float genuineSupport, float mobilization, float fairness)
            => VoteShare(genuineSupport, mobilization, fairness, PlebisciteParams.Default);

        /// <summary>可決か＝賛成シェアが閾値以上。</summary>
        public static bool Passes(float voteShare, PlebisciteParams p)
        {
            return Mathf.Clamp01(voteShare) >= p.passThreshold;
        }

        public static bool Passes(float voteShare) => Passes(voteShare, PlebisciteParams.Default);

        /// <summary>
        /// 結果の正統性（0..1）＝公正度×票差の説得力。大差×公正＝誰も文句を言えない、
        /// 僅差×不正くさい＝紙の上だけの決着。
        /// </summary>
        public static float ResultLegitimacy(float voteShare, float fairness, PlebisciteParams p)
        {
            float margin = Mathf.Abs(Mathf.Clamp01(voteShare) - p.passThreshold);
            float marginWeight = Mathf.Clamp01(margin / Mathf.Max(0.0001f, p.contestedMargin)); // 禍根余白で正規化（超えたら満点）
            return Mathf.Clamp01(fairness) * Mathf.Clamp01(0.5f + 0.5f * marginWeight);
        }

        public static float ResultLegitimacy(float voteShare, float fairness)
            => ResultLegitimacy(voteShare, fairness, PlebisciteParams.Default);

        /// <summary>禍根が残るか＝僅差（contestedMargin 未満）かつ公正度が0.5未満（=結果が呪われる）。</summary>
        public static bool IsContested(float voteShare, float fairness, PlebisciteParams p)
        {
            float margin = Mathf.Abs(Mathf.Clamp01(voteShare) - p.passThreshold);
            return margin < p.contestedMargin && Mathf.Clamp01(fairness) < 0.5f;
        }

        public static bool IsContested(float voteShare, float fairness)
            => IsContested(voteShare, fairness, PlebisciteParams.Default);
    }
}
