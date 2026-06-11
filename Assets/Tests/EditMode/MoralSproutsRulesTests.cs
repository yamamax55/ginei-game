using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>四端モデル（孟子の四端・MENC-1 #1564）の EditMode テスト。善政の涵養・暴政の萎縮・端→徳の写像・公徳心・社会調和・性善説の下限・義憤を担保。</summary>
    public class MoralSproutsRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>善政（goodGovernance=1）が四端を育てる＝既定 rate0.15×dt1 で目標へ近づき各端が上がる。</summary>
        [Test]
        public void Cultivation_GoodGovernance_GrowsSprouts()
        {
            var s = new MoralSprouts(0.2f, 0.2f, 0.2f, 0.2f);
            var r = MoralSproutsRules.CultivationTick(s, 1f, 1f);
            // 0.2 + (1-0.2)*0.15 = 0.2 + 0.12 = 0.32
            Assert.AreEqual(0.32f, r.compassion, Eps);
            Assert.AreEqual(0.32f, r.shame, Eps);
            Assert.Greater(r.Average, s.Average);
        }

        /// <summary>暴政（tyranny=1）が四端を萎ませる＝既定 rate0.2×dt1 で各端が下がる。</summary>
        [Test]
        public void Withering_Tyranny_ShrinksSprouts()
        {
            var s = new MoralSprouts(0.6f, 0.6f, 0.6f, 0.6f);
            var r = MoralSproutsRules.WitheringTick(s, 1f, 1f);
            // 0.6 - 0.2 = 0.4
            Assert.AreEqual(0.4f, r.compassion, Eps);
            Assert.Less(r.Average, s.Average);
        }

        /// <summary>性善説の下限＝暴政でも芽は innateFloor(0.1) を割らない＝完全な悪にはならない。</summary>
        [Test]
        public void Withering_NeverBelowInnateFloor()
        {
            var s = new MoralSprouts(0.15f, 0.15f, 0.15f, 0.15f);
            var r = MoralSproutsRules.WitheringTick(s, 1f, 1f); // -0.2 だが下限0.1で止まる
            Assert.AreEqual(0.1f, r.compassion, Eps);
            Assert.GreaterOrEqual(MoralSproutsRules.InnateGoodness(r), 0.1f);
        }

        /// <summary>端→徳の写像＝芽(0.5)が育って徳(0.5)になる（既定 gain1.0）。</summary>
        [Test]
        public void VirtueFromSprouts_MapsSproutToVirtue()
        {
            Assert.AreEqual(0.5f, MoralSproutsRules.VirtueFromSprouts(0.5f), Eps);
            Assert.AreEqual(0f, MoralSproutsRules.VirtueFromSprouts(0f), Eps);
            Assert.AreEqual(1f, MoralSproutsRules.VirtueFromSprouts(1f), Eps);
        }

        /// <summary>民の公徳心＝四端を徳へ写像した平均（社会の道徳資本）。</summary>
        [Test]
        public void CivicVirtue_AveragesFourVirtues()
        {
            var s = new MoralSprouts(0.4f, 0.6f, 0.8f, 0.2f);
            // gain1.0 → (0.4+0.6+0.8+0.2)/4 = 0.5
            Assert.AreEqual(0.5f, MoralSproutsRules.CivicVirtue(s), Eps);
        }

        /// <summary>社会調和＝民の徳と統治の徳が合うほど高い（積）。どちらか欠ければ0。</summary>
        [Test]
        public void SocialHarmony_IsProductOfVirtues()
        {
            Assert.AreEqual(0.5f, MoralSproutsRules.SocialHarmony(0.8f, 0.625f), Eps);
            Assert.AreEqual(0f, MoralSproutsRules.SocialHarmony(0.8f, 0f), Eps);
            Assert.AreEqual(0f, MoralSproutsRules.SocialHarmony(0f, 0.8f), Eps);
        }

        /// <summary>義憤＝羞悪の心と不正の積＝暴政への抵抗の芽。不正が無ければ義憤も無い。</summary>
        [Test]
        public void RighteousIndignation_RequiresShameAndInjustice()
        {
            Assert.AreEqual(0.42f, MoralSproutsRules.RighteousIndignation(0.6f, 0.7f), Eps);
            Assert.AreEqual(0f, MoralSproutsRules.RighteousIndignation(0.6f, 0f), Eps);
            Assert.AreEqual(0f, MoralSproutsRules.RighteousIndignation(0f, 0.7f), Eps);
        }

        /// <summary>道徳的覚醒＝公徳心が既定閾値0.6以上で true（仁政が四端を開花させた）。</summary>
        [Test]
        public void IsMoralAwakening_AtThreshold()
        {
            Assert.IsTrue(MoralSproutsRules.IsMoralAwakening(0.6f));
            Assert.IsTrue(MoralSproutsRules.IsMoralAwakening(0.7f));
            Assert.IsFalse(MoralSproutsRules.IsMoralAwakening(0.59f));
        }
    }
}
