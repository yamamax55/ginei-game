using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// リース会社（#1989 LEAS・<see cref="LeasingRules"/>）を固定する：リース料(LEAS-1)、ファイナンス/オペレーティング(LEAS-2)、
    /// 残価リスクと中途解約(LEAS-3)、与信とデフォルト(LEAS-4)、セール&リースバック(LEAS-5)、戦艦リース(LEAS-6)。
    /// </summary>
    public class LeasingTests
    {
        // ===== LEAS-1 リースの基礎 =====
        [Test]
        public void PeriodicPayment_AndTotal()
        {
            // (1000−200)/4 + 1000×0.05 = 200+50 = 250
            Assert.AreEqual(250f, LeasingRules.PeriodicPayment(1000f, 200f, 4, 0.05f), 1e-3f);
            // 残価0なら値下がり分が増える：(1000)/4 + 50 = 300
            Assert.AreEqual(300f, LeasingRules.PeriodicPayment(1000f, 0f, 4, 0.05f), 1e-3f);
            Assert.AreEqual(0f, LeasingRules.PeriodicPayment(1000f, 0f, 0, 0.05f), 1e-3f);
            Assert.AreEqual(1200f, LeasingRules.TotalLeaseCost(300f, 4), 1e-3f);
            Assert.AreEqual(200f, LeasingRules.LeaseVsBuyPremium(1200f, 1000f), 1e-3f); // 購入より200割高
        }

        // ===== LEAS-2 ファイナンス vs オペレーティング =====
        [Test]
        public void ClassifyLease_ResidualRiskBearer()
        {
            Assert.AreEqual(0.9f, LeasingRules.PayoutRatio(1000f, 100f), 1e-4f);
            // 残価小＝フルペイアウト＝ファイナンス（借り手が残価リスク）
            Assert.AreEqual(LeaseType.ファイナンス, LeasingRules.ClassifyLease(1000f, 100f, LeasingRules.FinanceLeaseThreshold));
            Assert.IsTrue(LeasingRules.LesseeBearsResidualRisk(LeaseType.ファイナンス));
            // 残価大＝短期返却＝オペレーティング（貸し手が残価リスク）
            Assert.AreEqual(LeaseType.オペレーティング, LeasingRules.ClassifyLease(1000f, 400f, LeasingRules.FinanceLeaseThreshold));
            Assert.IsFalse(LeasingRules.LesseeBearsResidualRisk(LeaseType.オペレーティング));
        }

        // ===== LEAS-3 残価リスクと中途解約 =====
        [Test]
        public void ResidualRisk_AndEarlyTermination()
        {
            Assert.AreEqual(-50f, LeasingRules.ResidualGainLoss(200f, 150f), 1e-3f); // 見込み200・実売150＝損
            Assert.AreEqual(50f, LeasingRules.ResidualGainLoss(200f, 250f), 1e-3f);  // 上回れば益
            Assert.AreEqual(750f, LeasingRules.RemainingPayments(250f, 3), 1e-3f);
            Assert.AreEqual(375f, LeasingRules.EarlyTerminationFee(750f, 0.5f), 1e-3f);
        }

        // ===== LEAS-4 与信とデフォルト =====
        [Test]
        public void Default_RecoveryAndExposure()
        {
            // 債権800・現物回収500＝純損失300（リースの担保性で軽減）
            Assert.AreEqual(300f, LeasingRules.DefaultLoss(800f, 500f), 1e-3f);
            Assert.AreEqual(0.625f, LeasingRules.RecoveryRate(500f, 800f), 1e-3f);
            Assert.AreEqual(600f, LeasingRules.NetExposure(800f, 200f), 1e-3f); // 残価で担保される純与信
        }

        // ===== LEAS-5 セール・アンド・リースバック =====
        [Test]
        public void SaleAndLeaseback()
        {
            Assert.AreEqual(1000f, LeasingRules.LeasebackCashRaised(1000f), 1e-3f);   // 売却で現金化
            Assert.AreEqual(200f, LeasingRules.LeasebackFinancingCost(1000f, 1200f), 1e-3f); // 総リース1200−売却1000
            Assert.IsTrue(LeasingRules.IsLiquidityPositive(1000f, 800f));            // 当面の資金需要を賄える
            Assert.IsFalse(LeasingRules.IsLiquidityPositive(1000f, 1200f));
        }

        // ===== LEAS-6 戦艦リース =====
        [Test]
        public void WarshipLease_StrengthToPool()
        {
            // 軍艦リース料：(4000−1000)/6 + 4000×0.05 = 500+200 = 700
            Assert.AreEqual(700f, LeasingRules.WarshipLeasePayment(4000f, 1000f, 6, 0.05f), 1e-3f);
            // 建造せず戦力をプール（#148）へ一時供給→返却
            FleetPool.Clear();
            Assert.AreEqual(100, LeasingRules.CommissionLeasedStrength(Faction.帝国, 100));
            Assert.AreEqual(100, FleetPool.Get(Faction.帝国));
            Assert.AreEqual(60, LeasingRules.ReturnLeasedStrength(Faction.帝国, 40)); // リース終了で一部返却
            FleetPool.Clear();
            // 買取＝残価／建艦との初期費用差（リースは初期費用が小さい）
            Assert.AreEqual(1000f, LeasingRules.BuyoutPrice(1000f), 1e-3f);
            Assert.AreEqual(3300f, LeasingRules.LeaseVsBuildInitialSaving(4000f, 700f), 1e-3f); // 建造4000 vs 初回700
        }
    }
}
