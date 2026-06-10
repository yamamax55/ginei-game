using NUnit.Framework;
using Ginei;
using FP = Ginei.FiscalRules.FiscalParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 財政・経済（#163 EPIC・#161/#162）を固定する：PB・金利のリスクプレミアム・国債の増減・債務スパイラル・財政健全度/為替、
    /// 税収と社会保障費（オーナス連動）・再分配の政治帰結。すべて純ロジック。
    /// </summary>
    public class FiscalRulesTests
    {
        private const float Economy = 1000f;

        // ===== #161 財政 =====

        [Test]
        public void PrimaryBalance_RevenueMinusExpenditure()
        {
            var s = new FiscalState(150f, 100f);
            Assert.AreEqual(50f, FiscalRules.PrimaryBalance(s), 1e-4f);
        }

        [Test]
        public void InterestRate_RisesWithDebtRatio()
        {
            var p = FP.Default; // safe 0.6, slope 0.1
            var low = new FiscalState(0, 0, debt: 300f);  // ratio 0.3 < 0.6 → 基準のみ
            var high = new FiscalState(0, 0, debt: 1600f); // ratio 1.6 → +（1.6-0.6)*0.1=0.1
            Assert.AreEqual(0.02f, FiscalRules.InterestRate(low, Economy, p), 1e-4f);
            Assert.AreEqual(0.12f, FiscalRules.InterestRate(high, Economy, p), 1e-4f);
        }

        [Test]
        public void Tick_DeficitGrowsDebt_SurplusShrinks()
        {
            var p = FP.Default;
            var deficit = new FiscalState(100f, 120f); // PB -20、債務0→利払い0
            FiscalRules.Tick(deficit, Economy, 1f, p);
            Assert.AreEqual(20f, deficit.debt, 1e-3f); // 赤字を国債で埋める

            var surplus = new FiscalState(150f, 100f, debt: 200f); // PB 50、利払い=200*0.02=4 → 収支+46
            FiscalRules.Tick(surplus, Economy, 1f, p);
            Assert.AreEqual(154f, surplus.debt, 1e-3f); // 黒字で減債
        }

        [Test]
        public void IsDebtSpiral_WhenInterestExceedsPrimarySurplus()
        {
            var p = FP.Default;
            var spiral = new FiscalState(100f, 90f, debt: 1600f); // PB10、利払い=1600*0.12=192 → PB<利払い・高債務
            Assert.IsTrue(FiscalRules.IsDebtSpiral(spiral, Economy, p));

            var healthy = new FiscalState(150f, 100f, debt: 200f); // PB50、利払い4、低債務
            Assert.IsFalse(FiscalRules.IsDebtSpiral(healthy, Economy, p));
        }

        [Test]
        public void DebtSpiral_CompoundsOverTicks()
        {
            var p = FP.Default;
            var s = new FiscalState(100f, 90f, debt: 1600f); // 小黒字PBだが高債務＝利払いが上回る
            float before = s.debt;
            for (int i = 0; i < 5; i++) FiscalRules.Tick(s, Economy, 1f, p);
            Assert.Greater(s.debt, before); // 複利で膨らむ
        }

        [Test]
        public void FiscalHealthFactor_LerpsSafeToCrisis()
        {
            var p = FP.Default; // safe0.6 crisis2.0
            Assert.AreEqual(1f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 300f), Economy, p), 1e-4f);  // 0.3
            Assert.AreEqual(0f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 2000f), Economy, p), 1e-4f); // 2.0
            Assert.AreEqual(0.5f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 1300f), Economy, p), 1e-4f); // 1.3
        }

        [Test]
        public void ExchangeRate_DepreciatesWithDebt()
        {
            var p = FP.Default;
            float strong = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 300f), Economy, p);
            float weak = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 2000f), Economy, p);
            Assert.Greater(strong, weak);
            Assert.AreEqual(0.5f, weak, 1e-4f); // 危機で通貨半値
        }

        // ===== #162 税・社会保障 =====

        [Test]
        public void TaxRevenue_BaseTimesRate()
        {
            Assert.AreEqual(30f, FiscalRules.TaxRevenue(taxBase: 100f, taxRate: 0.3f), 1e-4f);
        }

        [Test]
        public void WelfareCost_RisesWithDependents_OnusLink()
        {
            var p = FP.Default;
            float few = FiscalRules.WelfareCost(dependents: 100f, welfareLevel: 0.5f, p);
            float many = FiscalRules.WelfareCost(dependents: 300f, welfareLevel: 0.5f, p); // 高齢化＝扶養増
            Assert.AreEqual(50f, few, 1e-4f);
            Assert.Greater(many, few); // 人口オーナスで社会保障費が増える
        }

        [Test]
        public void Redistribution_TaxPenaltyAndWelfareHope_Monotonic()
        {
            Assert.Greater(FiscalRules.TaxBurdenPenalty(0.8f), FiscalRules.TaxBurdenPenalty(0.2f));
            Assert.Greater(FiscalRules.WelfareHopeBonus(0.8f), FiscalRules.WelfareHopeBonus(0.2f));
        }

        [Test]
        public void RevenueAndExpenditure_Assemble()
        {
            var p = FP.Default;
            float rev = FiscalRules.Revenue(taxBase: 100f, taxRate: 0.3f, tradeIncome: 20f); // 30+20
            float exp = FiscalRules.Expenditure(military: 30f, admin: 10f, dependents: 50f, welfareLevel: 0.4f, p); // 30+10+20
            Assert.AreEqual(50f, rev, 1e-4f);
            Assert.AreEqual(60f, exp, 1e-4f);
        }

        // ===== 敵対的エッジケース（境界・クランプ・null・異常入力） =====

        // --- null 安全性 ---

        [Test]
        public void NullState_AllAccessorsReturnDefaults()
        {
            var p = FP.Default;
            // 仕様：s==null は安全に既定値を返す（例外を投げない）。
            Assert.AreEqual(0f, FiscalRules.DebtRatio(null, Economy), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.PrimaryBalance(null), 1e-5f);
            Assert.AreEqual(p.baseInterestRate, FiscalRules.InterestRate(null, Economy, p), 1e-5f); // debt0扱い→基準のみ
            Assert.AreEqual(0f, FiscalRules.InterestPayment(null, Economy, p), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.OverallBalance(null, Economy, p), 1e-5f);
            Assert.IsFalse(FiscalRules.IsDebtSpiral(null, Economy, p));
            // null は debt0 → ratio0 ≤ safe → 健全度1・為替1。
            Assert.AreEqual(1f, FiscalRules.FiscalHealthFactor(null, Economy, p), 1e-5f);
            Assert.AreEqual(1f, FiscalRules.ExchangeRateFactor(null, Economy, p), 1e-5f);
        }

        [Test]
        public void Tick_NullState_DoesNotThrow()
        {
            // 仕様：null は何もしない（落ちない）。
            Assert.DoesNotThrow(() => FiscalRules.Tick(null, Economy, 1f, FP.Default));
        }

        // --- economy のゼロ除算ガード（Mathf.Max(1f, economy)）---

        [Test]
        public void DebtRatio_ZeroEconomy_GuardedByMinOne()
        {
            // economy=0 でも分母は 1 にクランプ＝ratio = debt/1 = debt。ゼロ除算しない。
            var s = new FiscalState(0, 0, debt: 500f);
            Assert.AreEqual(500f, FiscalRules.DebtRatio(s, 0f), 1e-4f);
        }

        [Test]
        public void DebtRatio_NegativeEconomy_GuardedByMinOne()
        {
            // economy<0 も Mathf.Max(1f, economy)=1 で分母1。ratio=debt。
            var s = new FiscalState(0, 0, debt: 250f);
            Assert.AreEqual(250f, FiscalRules.DebtRatio(s, -1000f), 1e-4f);
        }

        [Test]
        public void DebtRatio_EconomyBetweenZeroAndOne_StillClampedToOne()
        {
            // economy=0.5 < 1 → 分母は1（0.5ではない）。仕様：最小経済規模1で割る。
            var s = new FiscalState(0, 0, debt: 10f);
            Assert.AreEqual(10f, FiscalRules.DebtRatio(s, 0.5f), 1e-4f);
        }

        // --- InterestRate のリスクプレミアム境界（Mathf.Max(0, ratio-safe)）---

        [Test]
        public void InterestRate_AtSafeRatio_NoPremium()
        {
            var p = FP.Default; // safe0.6, base0.02
            // debt600/economy1000 = ratio0.6 ちょうど → 超過0 → 基準のみ。
            var s = new FiscalState(0, 0, debt: 600f);
            Assert.AreEqual(0.02f, FiscalRules.InterestRate(s, Economy, p), 1e-5f);
        }

        [Test]
        public void InterestRate_BelowSafe_NeverNegativePremium()
        {
            var p = FP.Default;
            // ratio0.1 << safe0.6 → Max(0,負)=0 → プレミアム加算されず基準のまま（負の金利にならない）。
            var s = new FiscalState(0, 0, debt: 100f);
            Assert.AreEqual(0.02f, FiscalRules.InterestRate(s, Economy, p), 1e-5f);
        }

        // --- Tick の dt<=0 と債務クランプ ---

        [Test]
        public void Tick_NonPositiveDt_NoChange()
        {
            var p = FP.Default;
            var s0 = new FiscalState(100f, 120f, debt: 50f); // 赤字
            FiscalRules.Tick(s0, Economy, 0f, p);
            Assert.AreEqual(50f, s0.debt, 1e-4f); // dt=0 → 不変

            var sNeg = new FiscalState(100f, 120f, debt: 50f);
            FiscalRules.Tick(sNeg, Economy, -1f, p);
            Assert.AreEqual(50f, sNeg.debt, 1e-4f); // dt<0 → 不変
        }

        [Test]
        public void Tick_HugeSurplus_DebtClampedAtZero()
        {
            var p = FP.Default;
            // PB=1000、debt100、利払い=100*0.02=2 → 収支+998。debt-998 は負だが Max(0,..)=0 でクランプ。
            var s = new FiscalState(1000f, 0f, debt: 100f);
            FiscalRules.Tick(s, Economy, 1f, p);
            Assert.AreEqual(0f, s.debt, 1e-4f); // 債務は0未満にならない（過剰返済は0で止まる）
        }

        [Test]
        public void Tick_PartialDt_ScalesBalance()
        {
            var p = FP.Default;
            // PB=-20、debt0→利払い0→収支-20。dt=0.5 → debt増 = 0 - (-20*0.5) = 10。
            var s = new FiscalState(100f, 120f, debt: 0f);
            FiscalRules.Tick(s, Economy, 0.5f, p);
            Assert.AreEqual(10f, s.debt, 1e-4f);
        }

        // --- IsDebtSpiral の二重境界（strict > と strict <）---

        [Test]
        public void IsDebtSpiral_AtSafeRatio_NotSpiral()
        {
            var p = FP.Default;
            // ratio=0.6 ちょうど（>safe が false）。PBを小さくしても高債務条件を満たさない。
            // debt600、利払い=600*0.02=12、PB=5<12 だが ratio>safe が偽 → スパイラルでない。
            var s = new FiscalState(100f, 95f, debt: 600f);
            Assert.IsFalse(FiscalRules.IsDebtSpiral(s, Economy, p));
        }

        [Test]
        public void IsDebtSpiral_PbEqualsInterest_NotSpiral()
        {
            var p = FP.Default;
            // ratio1.6>safe を満たすが、PB==利払い（strict < が false）→ スパイラルでない。
            // debt1600 → 金利=0.02+(1.6-0.6)*0.1=0.12 → 利払い=192。PB=192 ちょうど。
            var s = new FiscalState(292f, 100f, debt: 1600f); // PB=192
            Assert.AreEqual(192f, FiscalRules.InterestPayment(s, Economy, p), 1e-3f);
            Assert.AreEqual(192f, FiscalRules.PrimaryBalance(s), 1e-3f);
            Assert.IsFalse(FiscalRules.IsDebtSpiral(s, Economy, p)); // 等しいだけでは膨らまない
        }

        // --- FiscalHealthFactor の両端クランプとちょうど境界 ---

        [Test]
        public void FiscalHealthFactor_AtExactSafeAndCrisis_Endpoints()
        {
            var p = FP.Default; // safe0.6 crisis2.0
            // ちょうど safe(ratio0.6) → 1.0、ちょうど crisis(ratio2.0) → 0.0。
            Assert.AreEqual(1f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 600f), Economy, p), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 2000f), Economy, p), 1e-5f);
        }

        [Test]
        public void FiscalHealthFactor_BeyondCrisis_StaysZero()
        {
            var p = FP.Default;
            // crisis を超えても 0 未満にならない（ratio>=crisis で 0 クランプ）。
            Assert.AreEqual(0f, FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 5000f), Economy, p), 1e-5f);
        }

        [Test]
        public void ExchangeRateFactor_AlwaysInHalfToOne()
        {
            var p = FP.Default;
            // 健全度0..1 → 為替 0.5..1.0 の範囲に常に収まる。
            float best = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 0f), Economy, p);
            float worst = FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 9999f), Economy, p);
            Assert.AreEqual(1f, best, 1e-5f);
            Assert.AreEqual(0.5f, worst, 1e-5f);
            // 中間 ratio1.3 → 健全度0.5 → 為替0.75。
            Assert.AreEqual(0.75f, FiscalRules.ExchangeRateFactor(new FiscalState(0, 0, 1300f), Economy, p), 1e-5f);
        }

        // --- TaxRevenue / WelfareCost のクランプ ---

        [Test]
        public void TaxRevenue_NegativeBase_ClampedToZero()
        {
            // 課税ベース負 → Max(0,base)=0 → 税収0（負の税収にならない）。
            Assert.AreEqual(0f, FiscalRules.TaxRevenue(taxBase: -100f, taxRate: 0.5f), 1e-5f);
        }

        [Test]
        public void TaxRevenue_RateClamped01_BothEnds()
        {
            // taxRate>1 → Clamp01=1 → ベースそのまま。taxRate<0 → Clamp01=0 → 税収0。
            Assert.AreEqual(100f, FiscalRules.TaxRevenue(taxBase: 100f, taxRate: 5f), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.TaxRevenue(taxBase: 100f, taxRate: -2f), 1e-5f);
        }

        [Test]
        public void WelfareCost_NegativeDependentsAndLevel_ClampedToZero()
        {
            var p = FP.Default; // welfarePerDependent=1
            Assert.AreEqual(0f, FiscalRules.WelfareCost(dependents: -50f, welfareLevel: 0.5f, p), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.WelfareCost(dependents: 50f, welfareLevel: -0.3f, p), 1e-5f);
            // welfareLevel>1 → Clamp01=1 → 扶養×1×係数。
            Assert.AreEqual(50f, FiscalRules.WelfareCost(dependents: 50f, welfareLevel: 3f, p), 1e-5f);
        }

        [Test]
        public void Revenue_NegativeTradeIncome_ClampedToZero()
        {
            // 交易収入負 → Max(0,..)=0。税収30 のみ。
            Assert.AreEqual(30f, FiscalRules.Revenue(taxBase: 100f, taxRate: 0.3f, tradeIncome: -999f), 1e-5f);
        }

        [Test]
        public void Expenditure_NegativeComponents_ClampedToZero()
        {
            var p = FP.Default;
            // 軍事・内政が負でも 0 クランプ。社会保障のみ＝50*0.4*1=20。
            float exp = FiscalRules.Expenditure(military: -100f, admin: -50f, dependents: 50f, welfareLevel: 0.4f, p);
            Assert.AreEqual(20f, exp, 1e-5f);
        }

        // --- 政治帰結係数の両端クランプ ---

        [Test]
        public void TaxBurdenPenalty_ClampedRange()
        {
            // taxRate>1 → 上限 TaxDiscontentMax、taxRate<0 → 0。
            Assert.AreEqual(FiscalRules.TaxDiscontentMax, FiscalRules.TaxBurdenPenalty(2f), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.TaxBurdenPenalty(-1f), 1e-5f);
        }

        [Test]
        public void WelfareHopeBonus_ClampedRange()
        {
            Assert.AreEqual(FiscalRules.WelfareHopeMax, FiscalRules.WelfareHopeBonus(2f), 1e-5f);
            Assert.AreEqual(0f, FiscalRules.WelfareHopeBonus(-1f), 1e-5f);
        }

        // --- FiscalParams コンストラクタのクランプ ---

        [Test]
        public void FiscalParams_NegativeInputs_ClampedToZero()
        {
            // 負の基準金利・傾き・safe・扶養コストは 0 へクランプ。
            var p = new FP(baseInterestRate: -1f, riskPremiumSlope: -2f, safeDebtRatio: -0.5f, crisisDebtRatio: -3f, welfarePerDependent: -1f);
            Assert.AreEqual(0f, p.baseInterestRate, 1e-5f);
            Assert.AreEqual(0f, p.riskPremiumSlope, 1e-5f);
            Assert.AreEqual(0f, p.safeDebtRatio, 1e-5f);
            Assert.AreEqual(0f, p.welfarePerDependent, 1e-5f);
            // crisis は safe+0.01 を下回れない → クランプ後 safe=0 なので 0.01 へ押し上げが仕様。
            // ★疑似バグ：コンストラクタは crisis 計算に「クランプ前の生の safeDebtRatio(-0.5)」を使うため
            //   crisis=Max(-0.5+0.01,-3)=-0.49 となり、クランプ後 safe(0) を下回る＝不変条件 crisis>=safe+0.01 違反。
            //   FiscalHealthFactor の分母(crisis-safe)=-0.49<0 で健全度が反転しうる。仕様値0.01で assert（落ちる）。
            Assert.AreEqual(0.01f, p.crisisDebtRatio, 1e-5f);
        }

        [Test]
        public void FiscalParams_CrisisBelowSafe_ForcedAboveSafe()
        {
            // crisis<safe を渡すと safe+0.01 へ補正＝健全度の分母が常に正（ゼロ除算回避）。
            var p = new FP(0.02f, 0.1f, safeDebtRatio: 1.0f, crisisDebtRatio: 0.5f, welfarePerDependent: 1f);
            Assert.AreEqual(1.0f, p.safeDebtRatio, 1e-5f);
            Assert.AreEqual(1.01f, p.crisisDebtRatio, 1e-5f);
            // 分母 crisis-safe=0.01>0 → 健全度が NaN/Inf にならない。ratio1.005 は中間。
            float h = FiscalRules.FiscalHealthFactor(new FiscalState(0, 0, 1005f), Economy, p);
            Assert.AreEqual(0.5f, h, 1e-4f);
        }

        // --- FiscalState コンストラクタの負債クランプ ---

        [Test]
        public void FiscalState_NegativeDebt_ClampedToZero()
        {
            var s = new FiscalState(0, 0, debt: -500f);
            Assert.AreEqual(0f, s.debt, 1e-5f);
            Assert.AreEqual(0f, FiscalRules.DebtRatio(s, Economy), 1e-5f);
        }

        // --- OverallBalance の符号規約（PB−利払い）---

        [Test]
        public void OverallBalance_SubtractsInterestFromPb()
        {
            var p = FP.Default;
            // PB=50、debt200→利払い=200*0.02=4 → 収支=46。
            var s = new FiscalState(150f, 100f, debt: 200f);
            Assert.AreEqual(46f, FiscalRules.OverallBalance(s, Economy, p), 1e-4f);
        }
    }
}
