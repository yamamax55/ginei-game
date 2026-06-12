using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 銀行の業態分類（#2010 BTYP・<see cref="BankTypeRules"/>）を固定する：業態プロファイル(BTYP-1)、顧客適合(BTYP-2)、
    /// リレバン貸倒れ低減(BTYP-3)、協同組織性(BTYP-4)、地域経済連動(BTYP-5)。既存 Bank/BankRules は不変（後方互換）。
    /// </summary>
    public class BankTypeTests
    {
        // ===== BTYP-1 業態の定義・後方互換 =====
        [Test]
        public void Profiles_AndBackwardCompat()
        {
            Assert.IsTrue(BankTypeRules.IsCooperative(BankType.信用金庫));
            Assert.IsFalse(BankTypeRules.IsCooperative(BankType.都市銀行));
            Assert.IsFalse(BankTypeRules.IsCooperative(BankType.地方銀行));
            Assert.AreEqual(1.0f, BankTypeRules.ProfileFor(BankType.都市銀行).areaReach, 1e-4f);
            Assert.AreEqual(0.1f, BankTypeRules.ProfileFor(BankType.信用金庫).areaReach, 1e-4f);
            // 既存 Bank は業態既定＝都市銀行（従来挙動）
            Assert.AreEqual(BankType.都市銀行, new Bank(1000f, 800f).bankType);
        }

        // ===== BTYP-2 顧客層と貸出 =====
        [Test]
        public void CustomerFit_BySpecialization()
        {
            Assert.AreEqual(10f, BankTypeRules.IdealCustomerScale(BankType.都市銀行), 1e-4f);
            // 都銀は大企業に最適・零細に不向き
            Assert.AreEqual(1.0f, BankTypeRules.CustomerFitFactor(BankType.都市銀行, 10f), 1e-3f);
            Assert.AreEqual(0.05f, BankTypeRules.CustomerFitFactor(BankType.都市銀行, 0.5f), 1e-3f);
            // 信金は零細に最適・大企業を捌けない
            Assert.AreEqual(1.0f, BankTypeRules.CustomerFitFactor(BankType.信用金庫, 0.5f), 1e-3f);
            Assert.AreEqual(0.05f, BankTypeRules.CustomerFitFactor(BankType.信用金庫, 10f), 1e-3f);
        }

        // ===== BTYP-3 リレーションシップバンキング =====
        [Test]
        public void RelationshipBanking_LowersDefaultRisk()
        {
            float r = BankTypeRules.RelationshipMaxReduction; // 0.5
            // 信金は情報優位でデフォルトリスクを最も下げる
            Assert.AreEqual(0.55f, BankTypeRules.RelationshipDefaultFactor(BankType.信用金庫, r), 1e-3f);
            Assert.AreEqual(0.9f, BankTypeRules.RelationshipDefaultFactor(BankType.都市銀行, r), 1e-3f);
            Assert.AreEqual(0.055f, BankTypeRules.EffectiveDefaultRisk(0.1f, BankType.信用金庫, r), 1e-4f);
            Assert.AreEqual(0.09f, BankTypeRules.EffectiveDefaultRisk(0.1f, BankType.都市銀行, r), 1e-4f);
        }

        // ===== BTYP-4 信用金庫の協同組織性 =====
        [Test]
        public void Cooperative_DividendAndAreaRestriction()
        {
            // 協同組織（信金）は会員へ出資配当・株式会社は0
            Assert.AreEqual(300f, BankTypeRules.MemberDividend(1000f, 0.3f, BankTypeRules.IsCooperative(BankType.信用金庫)), 1e-3f);
            Assert.AreEqual(0f, BankTypeRules.MemberDividend(1000f, 0.3f, BankTypeRules.IsCooperative(BankType.都市銀行)), 1e-3f);
            // 信金は営業地域制限あり・区域外貸出不可
            Assert.IsTrue(BankTypeRules.IsAreaRestricted(BankType.信用金庫));
            Assert.IsFalse(BankTypeRules.OutOfAreaLendingAllowed(BankType.信用金庫));
            Assert.IsTrue(BankTypeRules.OutOfAreaLendingAllowed(BankType.都市銀行));
        }

        // ===== BTYP-5 地域経済との連動 =====
        [Test]
        public void RegionalEconomy_ConcentratedBanksSufferMore()
        {
            Assert.AreEqual(1.0f, BankTypeRules.DiversificationFactor(BankType.都市銀行), 1e-4f);
            Assert.AreEqual(0.1f, BankTypeRules.DiversificationFactor(BankType.信用金庫), 1e-4f);
            // 地域不況：都銀は全国分散で不感、地銀・信金は増幅（共倒れ）
            Assert.AreEqual(1.0f, BankTypeRules.RegionalDefaultMultiplier(BankType.都市銀行, 0.5f), 1e-3f);
            Assert.AreEqual(1.3f, BankTypeRules.RegionalDefaultMultiplier(BankType.地方銀行, 0.5f), 1e-3f);
            Assert.AreEqual(1.45f, BankTypeRules.RegionalDefaultMultiplier(BankType.信用金庫, 0.5f), 1e-3f);
        }
    }
}
