using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 信販会社（#1996 SHIN・<see cref="CreditFinanceRules"/>）を固定する：割賦・立替(SHIN-1)、カード加盟店手数料(SHIN-2)、
    /// リボルビング(SHIN-3)、与信審査と貸倒れ(SHIN-4)、信用保証(SHIN-5)、債権証券化(SHIN-6)。
    /// </summary>
    public class CreditFinanceTests
    {
        // ===== SHIN-1 割賦販売・立替払い =====
        [Test]
        public void Installment_AndMerchantAdvance()
        {
            // 1000×(1+0.2)/10 = 120
            Assert.AreEqual(120f, CreditFinanceRules.InstallmentPayment(1000f, 0.2f, 10), 1e-3f);
            Assert.AreEqual(0f, CreditFinanceRules.InstallmentPayment(1000f, 0.2f, 0), 1e-3f);
            Assert.AreEqual(200f, CreditFinanceRules.ConsumerFeeIncome(1000f, 0.2f), 1e-3f);
            // 加盟店へは手数料を引いて立替・差分が信販の取り分
            Assert.AreEqual(970f, CreditFinanceRules.MerchantAdvance(1000f, 0.03f), 1e-3f);
            Assert.AreEqual(30f, CreditFinanceRules.MerchantFeeIncome(1000f, 0.03f), 1e-3f);
        }

        // ===== SHIN-2 クレジットカード =====
        [Test]
        public void Card_FeeAndAvailableCredit()
        {
            Assert.AreEqual(300f, CreditFinanceRules.CardTransactionFee(10000f, 0.03f), 1e-3f);
            Assert.AreEqual(700f, CreditFinanceRules.AvailableCredit(1000f, 300f), 1e-3f);
            Assert.AreEqual(0f, CreditFinanceRules.AvailableCredit(1000f, 1500f), 1e-3f); // 限度超は0
        }

        // ===== SHIN-3 リボルビング =====
        [Test]
        public void Revolving_InterestAndDebtTrap()
        {
            Assert.AreEqual(15f, CreditFinanceRules.RevolvingInterest(1000f, 0.015f), 1e-3f);
            Assert.AreEqual(50f, CreditFinanceRules.MinimumPayment(1000f, 0.05f), 1e-3f);
            // 残高1000＋利息15−支払50 = 965（少しずつ減る）
            Assert.AreEqual(965f, CreditFinanceRules.BalanceAfterPayment(1000f, 50f, 15f), 1e-3f);
            // 支払が利息以下＝残高が減らない罠
            Assert.IsTrue(CreditFinanceRules.IsDebtTrap(10f, 15f));
            Assert.IsFalse(CreditFinanceRules.IsDebtTrap(50f, 15f));
        }

        // ===== SHIN-4 与信審査と貸倒れ =====
        [Test]
        public void Underwriting_LimitAndChargeOff()
        {
            Assert.AreEqual(1500f, CreditFinanceRules.CreditLimit(5000f, 0.3f), 1e-3f);
            Assert.IsTrue(CreditFinanceRules.CanApprove(1000f, 5000f, 0.3f));   // 1000<=1500
            Assert.IsFalse(CreditFinanceRules.CanApprove(2000f, 5000f, 0.3f));  // 2000>1500
            Assert.AreEqual(50f, CreditFinanceRules.DelinquencyLoss(1000f, 0.05f), 1e-3f);
            Assert.AreEqual(700f, CreditFinanceRules.ChargeOff(1000f, 0.3f), 1e-3f); // 回収3割→7割償却
        }

        // ===== SHIN-5 信用保証 =====
        [Test]
        public void Guarantee_FeeAndSubrogation()
        {
            Assert.AreEqual(200f, CreditFinanceRules.GuaranteeFee(10000f, 0.02f), 1e-3f);
            // 借り手焦げ付き→銀行へ1万肩代わり→6千回収→純損失4千
            Assert.AreEqual(4000f, CreditFinanceRules.Subrogation(10000f, 6000f), 1e-3f);
            Assert.AreEqual(10000f, CreditFinanceRules.GuaranteeExposure(10000f), 1e-3f);
        }

        // ===== SHIN-6 債権の証券化 =====
        [Test]
        public void Securitization_AndSpread()
        {
            Assert.AreEqual(9000f, CreditFinanceRules.SecuritizeReceivables(10000f, 0.9f), 1e-3f); // 掛け目9割
            Assert.AreEqual(450f, CreditFinanceRules.FundingCost(9000f, 0.05f), 1e-3f);
            Assert.AreEqual(0.10f, CreditFinanceRules.NetSpread(0.15f, 0.05f), 1e-4f); // 債権利回り−調達金利
        }
    }
}
