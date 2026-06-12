using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 証券会社（投資銀行・ブローカー・#1963 SEC・<see cref="SecuritiesFirmRules"/>）を固定する：ブローカー手数料(SEC-1)、
    /// 引受と売れ残り在庫(SEC-2)、自己売買・在庫評価損益(SEC-3)、自己資本規制(SEC-4)、収益と金融機関写像(SEC-5)。
    /// </summary>
    public class SecuritiesFirmTests
    {
        // ===== SEC-1 ブローカー業務 =====
        [Test]
        public void Brokerage_Commission()
        {
            Assert.AreEqual(100f, SecuritiesFirmRules.BrokerageCommission(10000f, 0.01f), 1e-3f);
            Assert.AreEqual(100f, SecuritiesFirmRules.CommissionFromClients(5000f, 2f, 0.01f), 1e-3f); // 預かり×回転×料率
        }

        // ===== SEC-2 引受業務 =====
        [Test]
        public void Underwriting_FeeAndUnplacedInventory()
        {
            Assert.AreEqual(50f, SecuritiesFirmRules.UnderwritingFee(1000f, 0.05f), 1e-3f);
            Assert.AreEqual(100f, SecuritiesFirmRules.UnplacedInventory(1000f, 0.9f), 1e-3f); // 消化率9割＝1割売れ残り
            // 引受実行：引受料を自己資本へ・売れ残りを在庫へ
            var firm = new SecuritiesFirm(); // cap=100, inv=0, 引受料率0.05
            float fee = SecuritiesFirmRules.Underwrite(firm, 1000f, 0.9f);
            Assert.AreEqual(50f, fee, 1e-3f);
            Assert.AreEqual(150f, firm.capital, 1e-3f);
            Assert.AreEqual(100f, firm.inventory, 1e-3f);
        }

        // ===== SEC-3 自己売買・マーケットメイク =====
        [Test]
        public void MarketMaking_AndInventoryShock()
        {
            Assert.AreEqual(50f, SecuritiesFirmRules.MarketMakingRevenue(10000f, 0.005f), 1e-3f);
            Assert.AreEqual(-20f, SecuritiesFirmRules.InventoryPnL(100f, -0.2f), 1e-3f);
            // 在庫100の証券会社が相場2割下落で評価損20＝自己資本が削られる
            var firm = new SecuritiesFirm("x", 150f, 100f);
            float pnl = SecuritiesFirmRules.ApplyInventoryShock(firm, -0.2f);
            Assert.AreEqual(-20f, pnl, 1e-3f);
            Assert.AreEqual(130f, firm.capital, 1e-3f);
        }

        // ===== SEC-4 自己資本規制 =====
        [Test]
        public void NetCapitalRule()
        {
            var ok = new SecuritiesFirm("健全", 10f, 100f);   // 在庫100→所要8、自己資本10
            var weak = new SecuritiesFirm("脆弱", 5f, 100f);  // 自己資本5<所要8
            Assert.AreEqual(100f, SecuritiesFirmRules.RiskExposure(ok), 1e-3f);
            Assert.AreEqual(8f, SecuritiesFirmRules.RequiredNetCapital(ok, SecuritiesFirmRules.MinNetCapitalRatio), 1e-3f);
            Assert.IsTrue(SecuritiesFirmRules.MeetsNetCapital(ok, SecuritiesFirmRules.MinNetCapitalRatio));
            Assert.IsFalse(SecuritiesFirmRules.MeetsNetCapital(weak, SecuritiesFirmRules.MinNetCapitalRatio));
            Assert.IsTrue(SecuritiesFirmRules.IsUndercapitalized(weak, SecuritiesFirmRules.MinNetCapitalRatio));
        }

        // ===== SEC-5 収益と健全性 =====
        [Test]
        public void Revenue_AndFinancialInstitutionBridge()
        {
            var firm = new SecuritiesFirm(); // 料率 0.01/0.05/0.005
            // 委託100 + 引受50 + マーケットメイク50 = 200
            Assert.AreEqual(200f, SecuritiesFirmRules.Revenue(firm, 10000f, 1000f, 10000f), 1e-3f);
            // 金融機関への写像＝危機 #1939 に組み込む
            var ib = new SecuritiesFirm("投資銀行", 100f, 100f);
            var fi = SecuritiesFirmRules.AsFinancialInstitution(ib);
            Assert.AreEqual(100f, fi.capital, 1e-3f);
            Assert.AreEqual(200f, fi.assets, 1e-3f);       // 自己資本+在庫
            Assert.AreEqual(100f, fi.mbsExposure, 1e-3f);  // 在庫＝リスク資産
            Assert.IsNull(SecuritiesFirmRules.AsFinancialInstitution(null));
        }
    }
}
