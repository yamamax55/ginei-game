using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ServiceObligationRules（服役義務の社会契約・STSH-3 #2360）の EditMode テスト。
    /// 徴募政策ごとの「兵力供給↑×合意コスト↓」トレードオフ・単調性・境界値・null 安全を担保。
    /// </summary>
    public class ServiceObligationRulesTests
    {
        private const float Eps = 1e-4f;

        [SetUp]
        public void Setup() => FleetPool.Clear();

        [TearDown]
        public void Teardown() => FleetPool.Clear();

        private static ServiceObligation Make(RecruitmentPolicy policy, float years = 2f, float exempt = 0f)
            => new ServiceObligation(Faction.同盟, policy, years, exempt);

        private static Polity FullPolity()
            => new Polity(1, Faction.同盟, 1000, 100, cooperation: 1f);

        // ─── YieldFactor / ConsentCostFactor 単調性 ──────────────────────

        [Test]
        public void YieldFactor_強制が強いほど供給歩留まりが単調増加()
        {
            Assert.Less(ServiceObligationRules.YieldFactor(RecruitmentPolicy.自発),
                        ServiceObligationRules.YieldFactor(RecruitmentPolicy.選抜徴兵));
            Assert.Less(ServiceObligationRules.YieldFactor(RecruitmentPolicy.選抜徴兵),
                        ServiceObligationRules.YieldFactor(RecruitmentPolicy.全員徴兵));
        }

        [Test]
        public void ConsentCostFactor_強制が強いほど合意コストが単調増加()
        {
            Assert.Less(ServiceObligationRules.ConsentCostFactor(RecruitmentPolicy.自発),
                        ServiceObligationRules.ConsentCostFactor(RecruitmentPolicy.選抜徴兵));
            Assert.Less(ServiceObligationRules.ConsentCostFactor(RecruitmentPolicy.選抜徴兵),
                        ServiceObligationRules.ConsentCostFactor(RecruitmentPolicy.全員徴兵));
        }

        [Test]
        public void LegitimacyFactor_自発服役のみが最も厚い正統性()
        {
            Assert.Greater(ServiceObligationRules.LegitimacyFactor(RecruitmentPolicy.自発),
                           ServiceObligationRules.LegitimacyFactor(RecruitmentPolicy.選抜徴兵));
            Assert.Greater(ServiceObligationRules.LegitimacyFactor(RecruitmentPolicy.選抜徴兵),
                           ServiceObligationRules.LegitimacyFactor(RecruitmentPolicy.全員徴兵));
        }

        // ─── トレードオフの核：供給↑ ↔ 合意コスト↓ ───────────────────────

        [Test]
        public void トレードオフ_全員徴兵は志願制より供給が多く合意コストも高い()
        {
            FleetPool.Set(Faction.同盟, 10000);
            var polity = FullPolity();

            int volSupply = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.自発));
            int totSupply = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.全員徴兵));
            Assert.Greater(totSupply, volSupply);

            float volCost = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.自発), polity);
            float totCost = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵), polity);
            Assert.Greater(totCost, volCost);
        }

        [Test]
        public void 志願制の合意コストはゼロ()
        {
            var polity = FullPolity();
            float cost = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.自発), polity);
            Assert.AreEqual(0f, cost, Eps);
        }

        // ─── RecruitablePool（FleetPool 委譲）───────────────────────────

        [Test]
        public void RecruitablePool_FleetPoolから免除率を引いた母数()
        {
            FleetPool.Set(Faction.同盟, 1000);
            int pool = ServiceObligationRules.RecruitablePool(Make(RecruitmentPolicy.全員徴兵, exempt: 0.2f));
            Assert.AreEqual(800, pool);
        }

        [Test]
        public void RecruitablePool_免除率1で母数ゼロ()
        {
            FleetPool.Set(Faction.同盟, 1000);
            int pool = ServiceObligationRules.RecruitablePool(Make(RecruitmentPolicy.全員徴兵, exempt: 1f));
            Assert.AreEqual(0, pool);
        }

        [Test]
        public void RecruitablePool_プール未設定で0()
        {
            Assert.AreEqual(0, ServiceObligationRules.RecruitablePool(Make(RecruitmentPolicy.全員徴兵)));
        }

        // ─── FleetStrengthSupply 境界値・単調性 ──────────────────────────

        [Test]
        public void FleetStrengthSupply_全員徴兵は母数満額に近い供給()
        {
            FleetPool.Set(Faction.同盟, 1000);
            // 全員徴兵 yield=1.0・服役2年=基準で逓増1.0 → 1000
            int supply = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.全員徴兵));
            Assert.AreEqual(1000, supply);
        }

        [Test]
        public void FleetStrengthSupply_服役年数が長いほど供給が増える()
        {
            FleetPool.Set(Faction.同盟, 1000);
            int shortS = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.選抜徴兵, years: 2f));
            int longS = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.選抜徴兵, years: 8f));
            Assert.Greater(longS, shortS);
        }

        [Test]
        public void FleetStrengthSupply_免除率が高いほど供給が減る()
        {
            FleetPool.Set(Faction.同盟, 1000);
            int low = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.全員徴兵, exempt: 0f));
            int high = ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.全員徴兵, exempt: 0.5f));
            Assert.Greater(low, high);
        }

        [Test]
        public void FleetStrengthSupply_供給は非負()
        {
            FleetPool.Set(Faction.同盟, 0);
            Assert.GreaterOrEqual(ServiceObligationRules.FleetStrengthSupply(Make(RecruitmentPolicy.全員徴兵)), 0);
        }

        // ─── ConsentCost 境界値・単調性 ─────────────────────────────────

        [Test]
        public void ConsentCost_免除率が高いほど合意コストは下がる()
        {
            var polity = FullPolity();
            float low = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵, exempt: 0f), polity);
            float high = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵, exempt: 0.5f), polity);
            Assert.Greater(low, high);
        }

        [Test]
        public void ConsentCost_服役年数が長いほど合意コストが増える()
        {
            var polity = FullPolity();
            float shortC = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵, years: 2f), polity);
            float longC = ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵, years: 8f), polity);
            Assert.Greater(longC, shortC);
        }

        [Test]
        public void ConsentCost_合意コストは非負()
        {
            var polity = FullPolity();
            Assert.GreaterOrEqual(
                ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵), polity), 0f);
        }

        // ─── LegitimacyBonus ───────────────────────────────────────────

        [Test]
        public void LegitimacyBonus_自発服役が最も厚い()
        {
            float vol = ServiceObligationRules.LegitimacyBonus(Make(RecruitmentPolicy.自発));
            float tot = ServiceObligationRules.LegitimacyBonus(Make(RecruitmentPolicy.全員徴兵));
            Assert.Greater(vol, tot);
        }

        [Test]
        public void LegitimacyBonus_免除率が高いと正統性が薄まる()
        {
            float full = ServiceObligationRules.LegitimacyBonus(Make(RecruitmentPolicy.自発, exempt: 0f));
            float exempt = ServiceObligationRules.LegitimacyBonus(Make(RecruitmentPolicy.自発, exempt: 0.5f));
            Assert.Greater(full, exempt);
        }

        [Test]
        public void LegitimacyBonus_全員徴兵は正統性ボーナスゼロ()
        {
            Assert.AreEqual(0f, ServiceObligationRules.LegitimacyBonus(Make(RecruitmentPolicy.全員徴兵)), Eps);
        }

        // ─── null 安全 ─────────────────────────────────────────────────

        [Test]
        public void Null安全_全API()
        {
            Assert.AreEqual(0, ServiceObligationRules.RecruitablePool(null));
            Assert.AreEqual(0, ServiceObligationRules.FleetStrengthSupply(null));
            Assert.AreEqual(1f, ServiceObligationRules.ServiceYearSupplyMultiplier(null), Eps);
            Assert.AreEqual(0f, ServiceObligationRules.LegitimacyBonus(null), Eps);
            Assert.AreEqual(0f, ServiceObligationRules.ConsentCost(null, FullPolity()), Eps);
            Assert.AreEqual(0f, ServiceObligationRules.ConsentCost(Make(RecruitmentPolicy.全員徴兵), null), Eps);
        }

        [Test]
        public void コンストラクタ_負の服役年数と範囲外免除率をクランプ()
        {
            var o = new ServiceObligation(Faction.帝国, RecruitmentPolicy.選抜徴兵, -5f, 2f);
            Assert.AreEqual(0f, o.serviceYearsRequired, Eps);
            Assert.AreEqual(1f, o.exemptionRate, Eps);
        }
    }
}
