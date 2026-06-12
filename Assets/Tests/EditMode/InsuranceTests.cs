using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 保険・ロイズ（#1982 INS・<see cref="InsuranceRules"/>）を固定する：大数の法則(INS-1)、引受損益(INS-2)、ソルベンシー(INS-3)、
    /// 再保険(INS-4)、ロイズの共同引受と海上保険(INS-5)、フロート運用(INS-6)。
    /// </summary>
    public class InsuranceTests
    {
        // ===== INS-1 保険の基礎 =====
        [Test]
        public void ExpectedLoss_FairPremium_Pool()
        {
            Assert.AreEqual(20f, InsuranceRules.ExpectedLoss(0.02f, 1000f), 1e-3f);    // 確率×損害
            Assert.AreEqual(26f, InsuranceRules.FairPremium(0.02f, 1000f, 0.3f), 1e-3f); // 20×1.3
            var pool = new List<InsurancePolicy>
            {
                new InsurancePolicy(0.02f, 1000f, 26f),
                new InsurancePolicy(0.05f, 2000f, 130f),
            };
            Assert.AreEqual(120f, InsuranceRules.PoolExpectedLoss(pool), 1e-3f); // 20+100
            Assert.AreEqual(156f, InsuranceRules.PoolPremium(pool), 1e-3f);      // 26+130
            Assert.AreEqual(0.8f, InsuranceRules.LossRatio(80f, 100f), 1e-4f);
        }

        // ===== INS-2 引受損益 =====
        [Test]
        public void Underwriting_ResultAndCombinedRatio()
        {
            Assert.AreEqual(10f, InsuranceRules.UnderwritingResult(100f, 70f, 20f), 1e-3f);
            Assert.AreEqual(0.9f, InsuranceRules.CombinedRatio(70f, 20f, 100f), 1e-4f);
            Assert.IsTrue(InsuranceRules.IsUnderwritingProfit(0.9f));
            Assert.IsFalse(InsuranceRules.IsUnderwritingProfit(1.1f)); // (80+30)/100=1.1＝引受赤字
            // 保険会社データからの引受損益
            var ins = new Insurer("保険会社", 100f, premiumsWritten: 100f, claimsPaid: 70f, expenses: 20f);
            Assert.AreEqual(10f, InsuranceRules.UnderwritingResult(ins), 1e-3f);
        }

        // ===== INS-3 ソルベンシー =====
        [Test]
        public void Reserve_Solvency()
        {
            Assert.AreEqual(110f, InsuranceRules.RequiredReserve(100f, 0.1f), 1e-3f); // 期待保険金×1.1
            Assert.AreEqual(50f, InsuranceRules.RequiredCapital(100f, 70f, 0.5f), 1e-3f); // max(100,70)×0.5
            Assert.AreEqual(1.6f, InsuranceRules.SolvencyRatio(80f, 50f), 1e-3f);
            Assert.IsTrue(InsuranceRules.IsSolvent(80f, 50f, InsuranceRules.MinSolvencyRatio));
            Assert.IsFalse(InsuranceRules.IsSolvent(40f, 50f, InsuranceRules.MinSolvencyRatio)); // 0.8<1
        }

        // ===== INS-4 再保険 =====
        [Test]
        public void Reinsurance_CedeAndExcessOfLoss()
        {
            Assert.AreEqual(400f, InsuranceRules.CededLoss(1000f, 0.4f), 1e-3f);   // 比例で渡す
            Assert.AreEqual(600f, InsuranceRules.RetainedLoss(1000f, 0.4f), 1e-3f); // 自分に残る
            // 超過損害額：保有300超を限度500まで再保険が負担
            Assert.AreEqual(500f, InsuranceRules.ExcessOfLoss(1000f, 300f, 500f), 1e-3f); // 700だが限度500
            Assert.AreEqual(100f, InsuranceRules.ExcessOfLoss(400f, 300f, 500f), 1e-3f);  // 100
            Assert.AreEqual(0f, InsuranceRules.ExcessOfLoss(200f, 300f, 500f), 1e-3f);    // 保有内＝再保険なし
            Assert.AreEqual(52f, InsuranceRules.ReinsurancePremium(40f, 0.3f), 1e-3f);    // 出再期待損失×1.3
        }

        // ===== INS-5 ロイズ＝保険市場 =====
        [Test]
        public void Lloyds_SyndicatePlacementAndMarine()
        {
            var s1 = new LloydsSyndicate("S1", capacity: 300f, lineShare: 0.2f);
            var s2 = new LloydsSyndicate("S2", capacity: 100f, lineShare: 0.5f);
            // ライン＝min(能力, リスク×割合)
            Assert.AreEqual(200f, InsuranceRules.SyndicateLine(s1, 1000f), 1e-3f); // min(300,200)
            Assert.AreEqual(100f, InsuranceRules.SyndicateLine(s2, 1000f), 1e-3f); // min(100,500)
            // 共同引受＝合計300（リスク1000には届かず未充足）
            Assert.AreEqual(300f, InsuranceRules.PlaceRisk(new List<LloydsSyndicate> { s1, s2 }, 1000f), 1e-3f);
            Assert.IsFalse(InsuranceRules.IsFullyPlaced(new List<LloydsSyndicate> { s1, s2 }, 1000f));
            // 引受能力が十分なら満たされる
            var big1 = new LloydsSyndicate("B1", 1000f, 0.6f);
            var big2 = new LloydsSyndicate("B2", 1000f, 0.5f);
            Assert.IsTrue(InsuranceRules.IsFullyPlaced(new List<LloydsSyndicate> { big1, big2 }, 1000f));
            // 海上保険料＝船団価値×襲撃確率×(1+付加)（通商破壊 #94/#95 から）
            Assert.AreEqual(650f, InsuranceRules.MarinePremium(10000f, 0.05f, 0.3f), 1e-2f); // 500×1.3
        }

        // ===== INS-6 フロート運用 =====
        [Test]
        public void Float_InvestmentAndTotalProfit()
        {
            Assert.AreEqual(600f, InsuranceRules.Float(1000f, 400f), 1e-3f);
            Assert.AreEqual(30f, InsuranceRules.InvestmentIncome(600f, 0.05f), 1e-3f);
            Assert.AreEqual(40f, InsuranceRules.TotalProfit(10f, 30f), 1e-3f);   // 引受10＋投資30
            Assert.AreEqual(10f, InsuranceRules.TotalProfit(-20f, 30f), 1e-3f);  // 引受赤字でも投資で黒字（バフェット型）
            var ins = new Insurer("保険会社", 100f, premiumsWritten: 1000f, claimsPaid: 400f);
            Assert.AreEqual(600f, InsuranceRules.Float(ins), 1e-3f);
        }
    }
}
