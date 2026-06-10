using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 住民投票を固定する：票＝民意＋不公正×動員の振れ（公正なら民意のまま）、監視団の公正底上げ、
    /// 可決閾値、結果の正統性＝公正×票差の説得力、僅差×不正の禍根判定。境界を担保。
    /// </summary>
    public class PlebisciteRulesTests
    {
        private static readonly PlebisciteParams P = PlebisciteParams.Default;
        // 可決0.5/不正振れ0.3/監視0.1/禍根余白0.1

        [Test]
        public void EffectiveFairness_ObserversRaiseIt()
        {
            Assert.AreEqual(0.5f, PlebisciteRules.EffectiveFairness(0.3f, 2, P), 1e-5f);
            Assert.AreEqual(1f, PlebisciteRules.EffectiveFairness(0.8f, 5, P), 1e-5f); // 上限1
            Assert.AreEqual(0.3f, PlebisciteRules.EffectiveFairness(0.3f, 0, P), 1e-5f);
        }

        [Test]
        public void VoteShare_FairVoteReflectsTruth()
        {
            // 公正1＝民意がそのまま出る（動員しても振れない）
            Assert.AreEqual(0.4f, PlebisciteRules.VoteShare(0.4f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void VoteShare_RiggingSwingsResult()
        {
            // 公正0×賛成側動員＝+0.3：民意0.4 → 票0.7
            Assert.AreEqual(0.7f, PlebisciteRules.VoteShare(0.4f, 1f, 0f, P), 1e-5f);
            // 反対側の動員は逆へ振る
            Assert.AreEqual(0.1f, PlebisciteRules.VoteShare(0.4f, -1f, 0f, P), 1e-5f);
        }

        [Test]
        public void Passes_AtThreshold()
        {
            Assert.IsTrue(PlebisciteRules.Passes(0.5f, P));
            Assert.IsFalse(PlebisciteRules.Passes(0.49f, P));
        }

        [Test]
        public void ResultLegitimacy_FairLandslideIsUnquestionable()
        {
            // 公正×大差＝満点
            Assert.AreEqual(1f, PlebisciteRules.ResultLegitimacy(0.8f, 1f, P), 1e-5f);
            // 公正でも閾値ちょうどの僅差＝半分の説得力
            Assert.AreEqual(0.5f, PlebisciteRules.ResultLegitimacy(0.5f, 1f, P), 1e-5f);
            // 不正くさい大差＝不正の分だけ割引
            Assert.AreEqual(0.3f, PlebisciteRules.ResultLegitimacy(0.9f, 0.3f, P), 1e-5f);
        }

        [Test]
        public void IsContested_NarrowAndDirty()
        {
            // 僅差（0.55）×低公正（0.3）＝禍根
            Assert.IsTrue(PlebisciteRules.IsContested(0.55f, 0.3f, P));
            // 大差なら不正くさくても結果は動かない
            Assert.IsFalse(PlebisciteRules.IsContested(0.8f, 0.3f, P));
            // 僅差でも公正なら受け入れられる
            Assert.IsFalse(PlebisciteRules.IsContested(0.55f, 0.8f, P));
        }
    }
}
