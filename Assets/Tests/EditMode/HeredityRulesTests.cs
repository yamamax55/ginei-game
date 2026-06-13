using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 能力の遺伝（結婚と出産システム基盤）を固定する：子は両親と相関しつつばらつく（中間親値＋平均回帰＋乱数）。
    /// <b>倫理ガード：優生学NG</b>＝遺伝率1未満で必ず平均回帰し、有能な親同士でも上限へラチェットできず、
    /// 乱数で低能力の親から優れた子・高能力の親から凡庸な子が生まれうる（選別による品種改良が成立しない）。
    /// </summary>
    public class HeredityRulesTests
    {
        static HeredityRules.HeredityParams P => HeredityRules.HeredityParams.Default; // h0.5 / mean50 / spread12 / max100

        [Test]
        public void ExpectedStat_RegressesTowardMean()
        {
            // 中間親値が平均(50)から遺伝率(0.5)ぶんだけ寄る＝平均回帰
            Assert.AreEqual(75f, HeredityRules.ExpectedStat(100, 100, P), 1e-4f); // 50+0.5*(100-50)
            Assert.AreEqual(25f, HeredityRules.ExpectedStat(0, 0, P), 1e-4f);     // 50+0.5*(0-50)
            Assert.AreEqual(50f, HeredityRules.ExpectedStat(50, 50, P), 1e-4f);
            Assert.AreEqual(50f, HeredityRules.MidParent(20, 80), 1e-4f);
        }

        [Test]
        public void InheritStat_CorrelatesButSpreads()
        {
            // roll=0.5 で無ノイズ＝期待値、roll=1/0 で±spread に散る
            Assert.AreEqual(75, HeredityRules.InheritStat(100, 100, 0.5f, P));
            Assert.AreEqual(87, HeredityRules.InheritStat(100, 100, 1f, P)); // +12
            Assert.AreEqual(63, HeredityRules.InheritStat(100, 100, 0f, P)); // -12
        }

        [Test]
        public void NoEugenicRatchet_TwoElitesNeverReachCap()
        {
            // 親が二人とも100でも、平均回帰＋限られたばらつきで上限100には届かない＝世代で能力を吊り上げられない
            for (float r = 0f; r <= 1f; r += 0.1f)
                Assert.Less(HeredityRules.InheritStat(100, 100, r, P), 100);
        }

        [Test]
        public void UpwardMobility_TwoLowParentsCanExceedThemselves()
        {
            // 親が二人とも0でも、上振れで親を超える子が生まれうる＝低能力の家系も埋もれない（優生学的選別の否定）
            Assert.Greater(HeredityRules.InheritStat(0, 0, 1f, P), 0);
        }

        [Test]
        public void InheritFinancialTrait_RandomParentOrMutation()
        {
            // 突然変異なし（mutateRoll≥率）＝どちらかの親からランダムに受け継ぐ（pickRoll<0.5 で親A）
            Assert.AreEqual(FinancialTrait.投資,
                HeredityRules.InheritFinancialTrait(FinancialTrait.投資, FinancialTrait.浪費, 0.2f, 0.9f, 0.1f));
            Assert.AreEqual(FinancialTrait.浪費,
                HeredityRules.InheritFinancialTrait(FinancialTrait.投資, FinancialTrait.浪費, 0.8f, 0.9f, 0.1f));
            // 突然変異（mutateRoll<率）＝親と無関係な3値から（多様性・選別でない）
            Assert.AreEqual(FinancialTrait.貯金,
                HeredityRules.InheritFinancialTrait(FinancialTrait.投資, FinancialTrait.投資, 0.0f, 0.05f, 0.1f));
            Assert.AreEqual(FinancialTrait.浪費,
                HeredityRules.InheritFinancialTrait(FinancialTrait.投資, FinancialTrait.投資, 0.99f, 0.05f, 0.1f));
        }

        [Test]
        public void RecessiveCarrier_PassesMaskedTalentDownTheLine()
        {
            var r = HeredityRules.RecessiveParams.Default; // 減衰0.9 / 開花下限75
            // 親が潜在90を持てば、世代減衰して81が子へ受け継がれる（埋もれて残る）
            Assert.AreEqual(81, HeredityRules.InheritRecessiveCarrier(90, 0, 50, 50, r));
            // 親の高い発現(100)が子(70)に出なければ、その差30が潜在として劣性化する
            Assert.AreEqual(30, HeredityRules.InheritRecessiveCarrier(0, 0, 100, 70, r));
            // 何も無ければ0
            Assert.AreEqual(0, HeredityRules.InheritRecessiveCarrier(0, 0, 50, 50, r));
        }

        [Test]
        public void ExpressRecessive_BloomsRarely_ElseStaysMasked()
        {
            var r = HeredityRules.RecessiveParams.Default; // 発現8% / 下限75
            // 発現：潜在81・bloomRoll<0.08・潜在>現発現 → 潜在値±ノイズへ開花（noise0で81）
            Assert.IsTrue(HeredityRules.WouldBloom(50, 81, 0.0f, r));
            Assert.AreEqual(81, HeredityRules.ExpressRecessive(50, 81, 0.0f, 0.5f, r, P));
            // ノイズで散る
            Assert.AreEqual(93, HeredityRules.ExpressRecessive(50, 81, 0.0f, 1.0f, r, P)); // +12
            // 非発現：bloomRoll が確率以上 → マスクのまま
            Assert.IsFalse(HeredityRules.WouldBloom(50, 81, 0.5f, r));
            Assert.AreEqual(50, HeredityRules.ExpressRecessive(50, 81, 0.5f, 0.5f, r, P));
            // 潜在が下限未満なら開花しない
            Assert.AreEqual(50, HeredityRules.ExpressRecessive(50, 60, 0.0f, 0.5f, r, P));
            // 既に発現が潜在以上なら変化なし
            Assert.AreEqual(90, HeredityRules.ExpressRecessive(90, 81, 0.0f, 0.5f, r, P));
        }

        [Test]
        public void Clamps_ToZeroAndMax()
        {
            var hi = new HeredityRules.HeredityParams(1f, 50f, 100f, 100f); // 遺伝率1・大ばらつき
            Assert.AreEqual(100, HeredityRules.InheritStat(100, 100, 1f, hi)); // 100+100 → clamp100
            Assert.AreEqual(0, HeredityRules.InheritStat(0, 0, 0f, hi));       // 0-100 → clamp0
        }
    }
}
