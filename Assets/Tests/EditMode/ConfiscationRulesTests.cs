using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 財産没収を固定する：執行遅延で資産が逃げる、国庫収入＝残存×範囲×回収効率、資産家の反発、
    /// 投資萎縮、純効果＝弱い門閥から速く狭く。境界を担保。
    /// </summary>
    public class ConfiscationRulesTests
    {
        private static readonly ConfiscationParams P = ConfiscationParams.Default;
        // 回収0.7/反発0.5/逃避0.1/萎縮0.4

        [Test]
        public void RemainingAssets_CapitalFlees()
        {
            // 即日執行＝全額残る
            Assert.AreEqual(1000f, ConfiscationRules.RemainingAssets(1000f, 0f, P), 1e-4f);
            // 遅延5＝半分逃げる
            Assert.AreEqual(500f, ConfiscationRules.RemainingAssets(1000f, 5f, P), 1e-4f);
            // 遅延10以上＝全部逃げる
            Assert.AreEqual(0f, ConfiscationRules.RemainingAssets(1000f, 10f, P), 1e-5f);
        }

        [Test]
        public void TreasuryGain_LeaksThroughExecution()
        {
            // 即日・全面＝1000×1×0.7=700（執行は漏れる）
            Assert.AreEqual(700f, ConfiscationRules.TreasuryGain(1000f, 1f, 0f, P), 1e-4f);
            // 遅延5＝残存500×0.7=350
            Assert.AreEqual(350f, ConfiscationRules.TreasuryGain(1000f, 1f, 5f, P), 1e-4f);
            Assert.AreEqual(0f, ConfiscationRules.TreasuryGain(1000f, 0f, 0f, P), 1e-5f);
        }

        [Test]
        public void EliteBacklash_StrongTargetsBiteBack()
        {
            Assert.AreEqual(0.5f, ConfiscationRules.EliteBacklash(1f, 1f, P), 1e-5f);
            // 弱い門閥からなら反発は小さい
            Assert.AreEqual(0.1f, ConfiscationRules.EliteBacklash(1f, 0.2f, P), 1e-5f);
        }

        [Test]
        public void InvestmentChill_FearSpreads()
        {
            Assert.AreEqual(0.4f, ConfiscationRules.InvestmentChill(1f, P), 1e-5f);
            Assert.AreEqual(0.2f, ConfiscationRules.InvestmentChill(0.5f, P), 1e-5f);
        }

        [Test]
        public void NetEffect_FastNarrowAgainstWeakElites()
        {
            // 弱い門閥（0.2）・即日・全面：0.7−0.1−0.4=+0.2＝引き合う
            Assert.AreEqual(0.2f, ConfiscationRules.NetEffect(1000f, 1f, 0f, 0.2f, P), 1e-5f);
            // 強い門閥（1.0）・遅延5：0.35−0.5−0.4=−0.55＝高くつく
            Assert.AreEqual(-0.55f, ConfiscationRules.NetEffect(1000f, 1f, 5f, 1f, P), 1e-5f);
            // 資産ゼロ＝何も起きない
            Assert.AreEqual(0f, ConfiscationRules.NetEffect(0f, 1f, 0f, 1f, P), 1e-5f);
        }
    }
}
