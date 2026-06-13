using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// KriegsherrRules（軍事請負将軍＝ヴァレンシュタイン型・#1424）の純ロジック検証。
    /// 私的融資の財務レバレッジ→軍の所有→政治的要求→国家への脅威の連鎖を担保する。
    /// </summary>
    public class KriegsherrRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>財務レバレッジ＝私的信用×期待略奪（借金で軍を起こす）。</summary>
        [Test]
        public void FinancialLeverage_私的信用と略奪期待の積()
        {
            // 信用0.8×略奪期待0.5＝0.4
            Assert.AreEqual(0.4f, KriegsherrRules.FinancialLeverage(0.8f, 0.5f), Eps);
            // どちらか欠ければ軍は起こせない
            Assert.AreEqual(0f, KriegsherrRules.FinancialLeverage(0f, 1f), Eps);
        }

        /// <summary>軍の私的所有度＝財務レバレッジ×契約自治（私兵化）。</summary>
        [Test]
        public void ArmyOwnership_レバレッジと自治で私兵化()
        {
            // レバレッジ0.5×自治0.6＝0.3
            Assert.AreEqual(0.3f, KriegsherrRules.ArmyOwnership(0.5f, 0.6f), Eps);
        }

        /// <summary>政治力＝私的所有度×軍規模（私兵を背景に要求を突きつける）。</summary>
        [Test]
        public void PoliticalLeverage_所有と規模の積()
        {
            // 所有0.6×規模0.5＝0.3
            Assert.AreEqual(0.3f, KriegsherrRules.PoliticalLeverage(0.6f, 0.5f), Eps);
            // 大軍でも皇帝の軍（所有0）なら要求にならない
            Assert.AreEqual(0f, KriegsherrRules.PoliticalLeverage(0f, 1f), Eps);
        }

        /// <summary>国家への脅威＝政治力×主権の弱さ（軍が国家を超える）。</summary>
        [Test]
        public void StateThreat_政治力と主権の弱さ()
        {
            // 政治力0.6×主権の弱さ0.5＝0.3
            Assert.AreEqual(0.3f, KriegsherrRules.StateThreat(0.6f, 0.5f), Eps);
            // 主権が強ければ（弱さ0）脅威なし
            Assert.AreEqual(0f, KriegsherrRules.StateThreat(1f, 0f), Eps);
        }

        /// <summary>債務リスク＝レバレッジ×略奪不足（私兵経済の脆さ）。</summary>
        [Test]
        public void DebtServiceRisk_レバレッジと略奪不足()
        {
            // レバレッジ0.8×略奪不足0.5＝0.4
            Assert.AreEqual(0.4f, KriegsherrRules.DebtServiceRisk(0.8f, 0.5f), Eps);
            // 略奪が満額（不足0）なら返済できる
            Assert.AreEqual(0f, KriegsherrRules.DebtServiceRisk(1f, 0f), Eps);
        }

        /// <summary>将軍への忠誠＝私的所有度×給与の信頼性（私兵の論理）。</summary>
        [Test]
        public void LoyaltyToGeneral_所有と給与信頼の積()
        {
            // 所有0.6×給与信頼0.5＝0.3
            Assert.AreEqual(0.3f, KriegsherrRules.LoyaltyToGeneral(0.6f, 0.5f), Eps);
        }

        /// <summary>解任の困難さ＝政治力そのまま（暗殺するしかない）。</summary>
        [Test]
        public void DismissalDifficulty_政治力に等しい()
        {
            Assert.AreEqual(0.7f, KriegsherrRules.DismissalDifficulty(0.7f), Eps);
            // クランプ確認
            Assert.AreEqual(1f, KriegsherrRules.DismissalDifficulty(1.5f), Eps);
        }

        /// <summary>強大化家臣（over-mighty subject）判定＝脅威が既定閾値0.5以上。</summary>
        [Test]
        public void IsOverMightySubject_既定閾値で判定()
        {
            var prm = KriegsherrParams.Default;
            Assert.AreEqual(0.5f, prm.overMightyThreshold, Eps);
            Assert.IsTrue(KriegsherrRules.IsOverMightySubject(0.5f));   // 閾値ちょうどで成立
            Assert.IsTrue(KriegsherrRules.IsOverMightySubject(0.7f));
            Assert.IsFalse(KriegsherrRules.IsOverMightySubject(0.4f));
        }
    }
}
