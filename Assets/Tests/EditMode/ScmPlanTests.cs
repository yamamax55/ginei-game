using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// SCM計画レイヤー（#2105）：MRP展開(SCM-1)/正味化(SCM-2)/充足(SCM-3)/配分(SCM-4)/配送(SCM-5)/計画(SCM-6)。
    /// </summary>
    public class ScmPlanTests
    {
        int fiber, cloth, clothing;

        [SetUp]
        public void Reset()
        {
            CommodityCatalog.Clear();
            RecipeBook.Clear();
            // 繊維→布→衣類 のチェーンを用意（布←繊維×2、衣類←布×2）。
            fiber = CommodityCatalog.Register("繊維", CommodityCategory.原材料).id;
            cloth = CommodityCatalog.Register("布", CommodityCategory.中間財).id;
            clothing = CommodityCatalog.Register("衣類", CommodityCategory.消費財).id;
            RecipeBook.Register(new Recipe(cloth).AddInput(fiber, 2f));
            RecipeBook.Register(new Recipe(clothing).AddInput(cloth, 2f));
        }

        // --- SCM-1 MRP展開 ---
        [Test]
        public void Explosion_RecursiveRequirements()
        {
            var req = RequirementsExplosionRules.Explode(clothing, 20f);
            Assert.AreEqual(20f, req[clothing], 1e-3f);
            Assert.AreEqual(40f, req[cloth], 1e-3f);   // 衣類20×布2
            Assert.AreEqual(80f, req[fiber], 1e-3f);   // 布40×繊維2

            // 歩留まり：食品←穀物×1 yield0.8、需要80 → 穀物100
            int grain = CommodityCatalog.Register("穀物", CommodityCategory.原材料).id;
            int food = CommodityCatalog.Register("食品", CommodityCategory.消費財).id;
            RecipeBook.Register(new Recipe(food, 0.8f).AddInput(grain, 1f));
            var req2 = RequirementsExplosionRules.Explode(food, 80f);
            Assert.AreEqual(80f, req2[food], 1e-3f);
            Assert.AreEqual(100f, req2[grain], 1e-3f); // 80/0.8×1
        }

        // --- SCM-2 正味化 ---
        [Test]
        public void NetRequirements_SubtractOnHand()
        {
            Assert.AreEqual(40f, NetRequirementsRules.Net(80f, 40f), 1e-3f);
            Assert.AreEqual(0f, NetRequirementsRules.Net(40f, 80f), 1e-3f);

            var gross = new Dictionary<int, float> { { fiber, 80f }, { cloth, 40f }, { clothing, 20f } };
            var onHand = new CommodityStock();
            onHand.Add(fiber, 40f);
            var net = NetRequirementsRules.NetRequirements(gross, onHand);
            Assert.AreEqual(40f, net[fiber], 1e-3f);   // 80−40
            Assert.AreEqual(40f, net[cloth], 1e-3f);   // 手持ち0
            Assert.AreEqual(20f, net[clothing], 1e-3f);
        }

        // --- SCM-3 充足 ---
        [Test]
        public void Coverage_AndShortfall()
        {
            Assert.AreEqual(0.5f, MrpCoverageRules.Coverage(40f, 80f), 1e-4f);
            Assert.AreEqual(1f, MrpCoverageRules.Coverage(100f, 80f), 1e-4f);
            Assert.AreEqual(1f, MrpCoverageRules.Coverage(50f, 0f), 1e-4f);
            Assert.AreEqual(40f, MrpCoverageRules.Shortfall(40f, 80f), 1e-3f);
        }

        // --- SCM-4 配分 ---
        [Test]
        public void Allocation_Proportional()
        {
            var a = SupplyAllocationRules.AllocateProportional(60f, new[] { 40f, 40f });
            Assert.AreEqual(30f, a[0], 1e-3f); // 60×0.5
            Assert.AreEqual(30f, a[1], 1e-3f);
            var b = SupplyAllocationRules.AllocateProportional(100f, new[] { 40f, 40f });
            Assert.AreEqual(40f, b[0], 1e-3f); // 需要上限でクランプ
            Assert.AreEqual(0.75f, SupplyAllocationRules.FillRate(30f, 40f), 1e-4f);
        }

        // --- SCM-5 配送 ---
        [Test]
        public void Distribution_DeliverAndBlock()
        {
            Assert.AreEqual(30f, DistributionPlanRules.NetPosition(100f, 70f), 1e-3f);
            Assert.AreEqual(-30f, DistributionPlanRules.NetPosition(70f, 100f), 1e-3f);
            Assert.AreEqual(30f, DistributionPlanRules.Deliver(50f, 30f, 100f, false), 1e-3f); // 不足律速
            Assert.AreEqual(20f, DistributionPlanRules.Deliver(50f, 30f, 20f, false), 1e-3f);  // 容量律速
            Assert.AreEqual(0f, DistributionPlanRules.Deliver(50f, 30f, 100f, true), 1e-3f);   // 通商破壊で遮断
        }

        // --- SCM-6 計画（MRP＋サービスレベル＋ボトルネック） ---
        [Test]
        public void Plan_ServiceLevelAndBottleneck()
        {
            var demands = new Dictionary<int, float> { { clothing, 20f } };
            var onHand = new CommodityStock();
            onHand.Add(fiber, 40f); // 繊維所要80に対し手持ち40＝充足0.5
            var plan = ScmTickRules.Plan(demands, onHand);

            Assert.AreEqual(80f, plan.grossReq[fiber], 1e-3f);
            Assert.AreEqual(40f, plan.netReq[fiber], 1e-3f);     // 80−40
            Assert.AreEqual(0.5f, plan.serviceLevel, 1e-4f);     // 原材料 繊維が律速
            Assert.AreEqual(fiber, plan.criticalCommodity);      // ボトルネック＝繊維

            // 原材料が潤沢ならサービスレベル1
            var onHand2 = new CommodityStock();
            onHand2.Add(fiber, 200f);
            var plan2 = ScmTickRules.Plan(demands, onHand2);
            Assert.AreEqual(1f, plan2.serviceLevel, 1e-4f);
        }
    }
}
