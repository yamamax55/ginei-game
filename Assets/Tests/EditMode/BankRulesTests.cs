using NUnit.Framework;
using Ginei;
using BP = Ginei.BankRules.BankParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 銀行・信用（CAP-2 #186）を固定する：信用創造（マネー乗数）・取り付けリスク（信認/準備率）・利鞘・債務超過判定。
    /// 各 public メソッドの代表＋境界＋クランプを決定論で担保。すべて純ロジック。
    /// </summary>
    public class BankRulesTests
    {
        // ===== CreditCreation =====

        [Test]
        public void CreditCreation_MoneyMultiplier()
        {
            var p = BP.Default;
            // 準備率0.1 → 1/0.1-1 = 9倍。預金100 → 900。
            Assert.AreEqual(900f, BankRules.CreditCreation(100f, 0.1f, p), 1e-3f);
        }

        [Test]
        public void CreditCreation_LowerReserveCreatesMore()
        {
            var p = BP.Default;
            float thin = BankRules.CreditCreation(100f, 0.05f, p);  // 薄い準備＝多く創造
            float thick = BankRules.CreditCreation(100f, 0.5f, p);  // 厚い準備＝少なく
            Assert.Greater(thin, thick);
        }

        [Test]
        public void CreditCreation_ClampsReserveAndNegativeDeposits()
        {
            var p = BP.Default; // minReserveRatio 0.01
            // 準備率0は下限0.01へクランプ＝1/0.01-1=99倍（ゼロ除算しない）。
            Assert.AreEqual(99f, BankRules.CreditCreation(1f, 0f, p), 1e-3f);
            // 負の預金は0へクランプ。
            Assert.AreEqual(0f, BankRules.CreditCreation(-100f, 0.1f, p), 1e-4f);
            // 準備率1（全額準備）＝信用創造0。
            Assert.AreEqual(0f, BankRules.CreditCreation(100f, 1f, p), 1e-4f);
        }

        // ===== BankRunRisk =====

        [Test]
        public void BankRunRisk_HighConfidenceIsSafe_LowConfidenceIsMax()
        {
            var p = BP.Default; // run 0.2, safe 0.8
            // 高信認（safe以上）＋潤沢準備＝リスク0。
            Assert.AreEqual(0f, BankRules.BankRunRisk(1f, 0.9f, p), 1e-4f);
            // 低信認（run以下）＝信認リスク1（準備上乗せ後もClamp01で1）。
            Assert.AreEqual(1f, BankRules.BankRunRisk(0.1f, 0.1f, p), 1e-4f);
        }

        [Test]
        public void BankRunRisk_ThinnerReserveRaisesRisk()
        {
            var p = BP.Default;
            // 同じ中間信認でも準備が薄いほどリスクが高い。
            float thin = BankRules.BankRunRisk(0.05f, 0.5f, p);
            float thick = BankRules.BankRunRisk(0.9f, 0.5f, p);
            Assert.Greater(thin, thick);
            // 中間信認0.5 → confRisk=(0.8-0.5)/(0.8-0.2)=0.5。厚い準備(1.0)で上乗せ≈0 → 0.5付近。
            Assert.AreEqual(0.5f, thick, 0.06f);
        }

        [Test]
        public void BankRunRisk_ClampsConfidenceAndStaysInRange()
        {
            var p = BP.Default;
            // 信認>1 はClamp01で1扱い＝安全。
            Assert.AreEqual(0f, BankRules.BankRunRisk(0.5f, 2f, p), 1e-4f);
            // 信認<0 はClamp01で0扱い＝最大。結果は0..1に収まる。
            float r = BankRules.BankRunRisk(0f, -1f, p);
            Assert.AreEqual(1f, r, 1e-4f);
            Assert.LessOrEqual(r, 1f);
            Assert.GreaterOrEqual(r, 0f);
        }

        // ===== InterestSpread =====

        [Test]
        public void InterestSpread_PositiveAndNegative()
        {
            // 貸出5%・預金2% → 利鞘3%。
            Assert.AreEqual(0.03f, BankRules.InterestSpread(0.05f, 0.02f), 1e-4f);
            // 逆鞘（貸出＜預金）は負。
            Assert.AreEqual(-0.01f, BankRules.InterestSpread(0.01f, 0.02f), 1e-4f);
        }

        // ===== IsInsolvent =====

        [Test]
        public void IsInsolvent_LoansBelowDeposits()
        {
            // 貸出80 < 預金100 ＝債務超過。
            Assert.IsTrue(BankRules.IsInsolvent(new Bank(100f, 80f)));
            // 貸出120 ≥ 預金100 ＝健全。
            Assert.IsFalse(BankRules.IsInsolvent(new Bank(100f, 120f)));
            // 同額は債務超過でない（境界）。
            Assert.IsFalse(BankRules.IsInsolvent(new Bank(100f, 100f)));
            // null は安全に false。
            Assert.IsFalse(BankRules.IsInsolvent(null));
        }
    }
}
