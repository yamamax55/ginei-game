using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 国家予算の基盤（<see cref="BudgetRules"/>/<see cref="NationalBudget"/>）を固定する：分野配分の読み書きと非負クランプ、
    /// 歳出総額/シェア、歳入との均衡（黒字/赤字/均衡・赤字率）、予算編成（比例縮尺・歳入への切り詰め・重み配分）、
    /// 出資度（実効値・上限/不足ペナルティ）と分野効果（倍率/加点の対称性）、FiscalState.baseExpenditure への接続。すべて純ロジック。
    /// </summary>
    public class BudgetRulesTests
    {
        private static NationalBudget Sample()
            => new NationalBudget(military: 40f, shipbuilding: 20f, administration: 15f,
                                  welfare: 15f, research: 6f, diplomacy: 4f); // 合計100

        // ===== 配分の読み書き =====

        [Test]
        public void GetSet_RoundTrips_AndClampsNegativeToZero()
        {
            var b = new NationalBudget();
            BudgetRules.Set(b, BudgetCategory.研究, 12f);
            Assert.AreEqual(12f, BudgetRules.Get(b, BudgetCategory.研究), 1e-4f);
            Assert.AreEqual(12f, b.research, 1e-4f);

            BudgetRules.Set(b, BudgetCategory.研究, -5f); // 負は0へ
            Assert.AreEqual(0f, BudgetRules.Get(b, BudgetCategory.研究), 1e-4f);
        }

        [Test]
        public void Add_AccumulatesAndFloorsAtZero()
        {
            var b = new NationalBudget();
            BudgetRules.Add(b, BudgetCategory.軍事, 30f);
            BudgetRules.Add(b, BudgetCategory.軍事, 10f);
            Assert.AreEqual(40f, BudgetRules.Get(b, BudgetCategory.軍事), 1e-4f);
            BudgetRules.Add(b, BudgetCategory.軍事, -100f); // 0で止まる
            Assert.AreEqual(0f, BudgetRules.Get(b, BudgetCategory.軍事), 1e-4f);
        }

        [Test]
        public void Ctor_ClampsNegativeInputs()
        {
            var b = new NationalBudget(-1f, -2f, -3f, -4f, -5f, -6f);
            Assert.AreEqual(0f, BudgetRules.Total(b), 1e-4f);
        }

        // ===== 総額・シェア =====

        [Test]
        public void Total_SumsAllCategories()
        {
            Assert.AreEqual(100f, BudgetRules.Total(Sample()), 1e-4f);
        }

        [Test]
        public void Share_IsCategoryOverTotal_ZeroWhenEmpty()
        {
            var b = Sample();
            Assert.AreEqual(0.40f, BudgetRules.Share(b, BudgetCategory.軍事), 1e-4f);
            Assert.AreEqual(0.04f, BudgetRules.Share(b, BudgetCategory.外交), 1e-4f);
            // 全シェアの合計は1
            float sum = 0f;
            for (int i = 0; i < BudgetRules.CategoryCount; i++) sum += BudgetRules.Share(b, (BudgetCategory)i);
            Assert.AreEqual(1f, sum, 1e-4f);
            // 空予算はシェア0（ゼロ割なし）
            Assert.AreEqual(0f, BudgetRules.Share(new NationalBudget(), BudgetCategory.軍事), 1e-5f);
        }

        // ===== 歳入との均衡 =====

        [Test]
        public void Balance_AndDeficitSurplusBalanced()
        {
            var b = Sample(); // 歳出100
            Assert.AreEqual(20f, BudgetRules.Balance(b, 120f), 1e-4f);
            Assert.IsTrue(BudgetRules.IsSurplus(b, 120f));
            Assert.IsTrue(BudgetRules.IsDeficit(b, 80f));
            Assert.IsTrue(BudgetRules.IsBalanced(b, 100f));
            Assert.IsFalse(BudgetRules.IsBalanced(b, 100.5f));
        }

        [Test]
        public void DeficitRatio_ShortfallOverTotal_ClampedAndZeroOnSurplus()
        {
            var b = Sample(); // 歳出100
            Assert.AreEqual(0.25f, BudgetRules.DeficitRatio(b, 75f), 1e-4f); // 不足25/100
            Assert.AreEqual(0f, BudgetRules.DeficitRatio(b, 120f), 1e-4f);   // 黒字＝0
            Assert.AreEqual(1f, BudgetRules.DeficitRatio(b, 0f), 1e-4f);     // 歳入0＝全部不足
            Assert.AreEqual(0f, BudgetRules.DeficitRatio(new NationalBudget(), 0f), 1e-4f); // 歳出0＝0
        }

        // ===== 予算編成 =====

        [Test]
        public void ScaleToTotal_PreservesShares()
        {
            var b = Sample(); // 100
            float milShare = BudgetRules.Share(b, BudgetCategory.軍事);
            BudgetRules.ScaleToTotal(b, 50f); // 半減
            Assert.AreEqual(50f, BudgetRules.Total(b), 1e-3f);
            Assert.AreEqual(milShare, BudgetRules.Share(b, BudgetCategory.軍事), 1e-4f); // シェア保存
            Assert.AreEqual(20f, b.military, 1e-3f); // 40→20
        }

        [Test]
        public void ScaleToTotal_EmptyBudget_NoChange()
        {
            var b = new NationalBudget();
            BudgetRules.ScaleToTotal(b, 100f); // 総額0＝比例不能＝無変化
            Assert.AreEqual(0f, BudgetRules.Total(b), 1e-5f);
        }

        [Test]
        public void CapToRevenue_CutsOnlyWhenOverRevenue()
        {
            var b = Sample(); // 100
            Assert.IsFalse(BudgetRules.CapToRevenue(b, 120f)); // 歳入>歳出＝切らない
            Assert.AreEqual(100f, BudgetRules.Total(b), 1e-4f);

            Assert.IsTrue(BudgetRules.CapToRevenue(b, 60f)); // 緊縮
            Assert.AreEqual(60f, BudgetRules.Total(b), 1e-3f);
            Assert.AreEqual(0.40f, BudgetRules.Share(b, BudgetCategory.軍事), 1e-4f); // シェア保存
        }

        [Test]
        public void AllocateByWeights_ProportionalToWeights()
        {
            var b = new NationalBudget();
            // 軍事:建艦:内政:社会保障:研究:外交 = 3:1:1:1:0:0 （合計6）
            BudgetRules.AllocateByWeights(b, 120f, new[] { 3f, 1f, 1f, 1f, 0f, 0f });
            Assert.AreEqual(120f, BudgetRules.Total(b), 1e-3f);
            Assert.AreEqual(60f, b.military, 1e-3f);   // 3/6×120
            Assert.AreEqual(20f, b.shipbuilding, 1e-3f); // 1/6×120
            Assert.AreEqual(0f, b.research, 1e-4f);
        }

        [Test]
        public void AllocateByWeights_ZeroWeights_DistributeEqually()
        {
            var b = new NationalBudget();
            BudgetRules.AllocateByWeights(b, 60f, new[] { 0f, 0f, 0f, 0f, 0f, 0f });
            Assert.AreEqual(60f, BudgetRules.Total(b), 1e-3f);
            Assert.AreEqual(10f, b.military, 1e-3f); // 均等＝60/6
            Assert.AreEqual(10f, b.diplomacy, 1e-3f);
        }

        [Test]
        public void AllocateByWeights_NullWeights_EqualSplit_ShortArrayPadsZero()
        {
            var b = new NationalBudget();
            BudgetRules.AllocateByWeights(b, 60f, null); // null＝均等
            Assert.AreEqual(10f, b.military, 1e-3f);

            var b2 = new NationalBudget();
            BudgetRules.AllocateByWeights(b2, 100f, new[] { 1f, 1f }); // 短い＝残りは重み0
            Assert.AreEqual(50f, b2.military, 1e-3f);
            Assert.AreEqual(50f, b2.shipbuilding, 1e-3f);
            Assert.AreEqual(0f, b2.administration, 1e-4f);
        }

        // ===== 出資度（実効値） =====

        [Test]
        public void FundingFactor_RatioClampedToMax_NeedZeroIsFull()
        {
            Assert.AreEqual(1f, BudgetRules.FundingFactor(50f, 50f), 1e-4f);   // 満額
            Assert.AreEqual(0.5f, BudgetRules.FundingFactor(25f, 50f), 1e-4f); // 不足
            Assert.AreEqual(BudgetRules.MaxFundingFactor, BudgetRules.FundingFactor(1000f, 50f), 1e-4f); // 過剰は頭打ち
            Assert.AreEqual(1f, BudgetRules.FundingFactor(0f, 0f), 1e-4f);     // need0＝過不足なし
            Assert.AreEqual(0f, BudgetRules.FundingFactor(-5f, 50f), 1e-4f);   // 負配分＝0
        }

        [Test]
        public void ShortfallPenalty_ComplementOfFunding()
        {
            Assert.AreEqual(0.5f, BudgetRules.ShortfallPenalty(25f, 50f), 1e-4f); // 半額＝50%不足
            Assert.AreEqual(0f, BudgetRules.ShortfallPenalty(60f, 50f), 1e-4f);   // 満額以上＝0
            Assert.AreEqual(1f, BudgetRules.ShortfallPenalty(0f, 50f), 1e-4f);    // 無投資＝100%不足
        }

        [Test]
        public void OutputFactors_TrackCategoryFunding()
        {
            var b = new NationalBudget();
            BudgetRules.Set(b, BudgetCategory.建艦, 30f);
            BudgetRules.Set(b, BudgetCategory.研究, 10f);
            BudgetRules.Set(b, BudgetCategory.軍事, 40f);
            Assert.AreEqual(1.5f, BudgetRules.ShipbuildingFactor(b, 20f), 1e-4f); // 30/20
            Assert.AreEqual(0.5f, BudgetRules.ResearchOutputFactor(b, 20f), 1e-4f); // 10/20
            Assert.AreEqual(1f, BudgetRules.MilitaryReadinessFactor(b, 40f), 1e-4f); // 満額
        }

        [Test]
        public void Bonuses_AreZeroAtFull_SymmetricAroundIt()
        {
            var b = new NationalBudget();
            // 満額（need=配分）→ 加点0
            BudgetRules.Set(b, BudgetCategory.内政, 10f);
            BudgetRules.Set(b, BudgetCategory.社会保障, 10f);
            BudgetRules.Set(b, BudgetCategory.外交, 10f);
            Assert.AreEqual(0f, BudgetRules.AdministrationStabilityBonus(b, 10f), 1e-4f);
            Assert.AreEqual(0f, BudgetRules.WelfareHopeBonus(b, 10f), 1e-4f);
            Assert.AreEqual(0f, BudgetRules.DiplomacyOpinionBonus(b, 10f), 1e-4f);

            // 過剰（2倍上限）→ +スケール、不足（無投資）→ −スケール
            Assert.AreEqual(BudgetRules.AdminStabilityScale, BudgetRules.AdministrationStabilityBonus(b, 5f), 1e-4f); // factor2→+10
            var empty = new NationalBudget();
            Assert.AreEqual(-BudgetRules.AdminStabilityScale, BudgetRules.AdministrationStabilityBonus(empty, 10f), 1e-4f); // factor0→-10
            Assert.AreEqual(-BudgetRules.WelfareHopeScale, BudgetRules.WelfareHopeBonus(empty, 10f), 1e-4f);
            Assert.AreEqual(-BudgetRules.DiplomacyOpinionScale, BudgetRules.DiplomacyOpinionBonus(empty, 10f), 1e-4f);
        }

        // ===== FiscalState 接続 =====

        [Test]
        public void ApplyToFiscalState_SetsBaseExpenditureToTotal()
        {
            var b = Sample(); // 100
            var fs = new FiscalState(revenue: 150f, baseExpenditure: 0f);
            BudgetRules.ApplyToFiscalState(b, fs);
            Assert.AreEqual(100f, fs.baseExpenditure, 1e-4f);
            // 予算が PB へ合流（歳入150−歳出100＝50）
            Assert.AreEqual(50f, FiscalRules.PrimaryBalance(fs), 1e-4f);
        }

        // ===== null/異常入力の安全性 =====

        [Test]
        public void NullInputs_AreSafe()
        {
            Assert.AreEqual(0f, BudgetRules.Total(null), 1e-5f);
            Assert.AreEqual(0f, BudgetRules.Get(null, BudgetCategory.軍事), 1e-5f);
            Assert.AreEqual(0f, BudgetRules.Share(null, BudgetCategory.軍事), 1e-5f);
            Assert.AreEqual(0f, BudgetRules.DeficitRatio(null, 10f), 1e-5f);
            Assert.IsFalse(BudgetRules.CapToRevenue(null, 10f));
            Assert.DoesNotThrow(() => BudgetRules.Set(null, BudgetCategory.軍事, 5f));
            Assert.DoesNotThrow(() => BudgetRules.ScaleToTotal(null, 5f));
            Assert.DoesNotThrow(() => BudgetRules.AllocateByWeights(null, 5f, new[] { 1f }));
            Assert.DoesNotThrow(() => BudgetRules.ApplyToFiscalState(null, null));
            Assert.DoesNotThrow(() => BudgetRules.ApplyToFiscalState(Sample(), null));
        }
    }
}
