using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// BOM生産システム（#2098）：カタログ(BOM-1)/レシピ(BOM-2)/在庫(BOM-3)/生産(BOM-4)/消費財需要(BOM-5)/Tick連鎖(BOM-6)。
    /// </summary>
    public class BomProductionTests
    {
        [SetUp]
        public void Reset()
        {
            CommodityCatalog.Clear();
            RecipeBook.Clear();
        }

        // --- BOM-1 カタログ ---
        [Test]
        public void Catalog_RegisterLookupDedup()
        {
            var wood = CommodityCatalog.Register("木材", CommodityCategory.原材料);
            var mat = CommodityCatalog.Register("建材", CommodityCategory.中間財);
            Assert.Greater(wood.id, 0);
            Assert.AreNotEqual(wood.id, mat.id);
            Assert.AreSame(wood, CommodityCatalog.Get(wood.id));
            Assert.AreSame(wood, CommodityCatalog.ByName("木材"));
            Assert.AreSame(wood, CommodityCatalog.Register("木材", CommodityCategory.原材料)); // 冪等
            Assert.AreEqual(1, CommodityCatalog.ByCategory(CommodityCategory.中間財).Count);
        }

        // --- BOM-2/3 レシピ・在庫 ---
        [Test]
        public void RecipeAndStock()
        {
            int wood = CommodityCatalog.Register("木材", CommodityCategory.原材料).id;
            int mat = CommodityCatalog.Register("建材", CommodityCategory.中間財).id;
            var r = RecipeBook.Register(new Recipe(mat).AddInput(wood, 2f));
            Assert.AreSame(r, RecipeBook.ForOutput(mat));
            Assert.AreEqual(1, r.inputs.Count);

            var cs = new CommodityStock();
            cs.Add(wood, 100f);
            Assert.AreEqual(100f, cs.Get(wood), 1e-3f);
            cs.Add(wood, -200f); // 非負
            Assert.AreEqual(0f, cs.Get(wood), 1e-3f);
        }

        // --- BOM-4 生産（レオンチェフ・ボトルネック） ---
        [Test]
        public void Production_MaxOutputProduceBottleneck()
        {
            int wood = CommodityCatalog.Register("木材", CommodityCategory.原材料).id;
            int mat = CommodityCatalog.Register("建材", CommodityCategory.中間財).id;
            var r = new Recipe(mat).AddInput(wood, 2f); // 建材←木材×2、歩留まり1
            var cs = new CommodityStock();
            cs.Add(wood, 100f);

            Assert.AreEqual(50f, BomProductionRules.MaxOutput(r, cs), 1e-3f); // 100/2
            float made = BomProductionRules.Produce(r, cs, 30f);
            Assert.AreEqual(30f, made, 1e-3f);
            Assert.AreEqual(40f, cs.Get(wood), 1e-3f);   // 100−30×2
            Assert.AreEqual(30f, cs.Get(mat), 1e-3f);

            // ボトルネック（木材不足で目標未達）
            var cs2 = new CommodityStock();
            cs2.Add(wood, 100f);
            int binding = BomProductionRules.Bottleneck(r, cs2, 60f, out bool constrained);
            Assert.IsTrue(constrained);
            Assert.AreEqual(wood, binding);
            BomProductionRules.Bottleneck(r, cs2, 40f, out bool ok);
            Assert.IsFalse(ok);

            // 歩留まり0.9：50×0.9＝45
            var ry = new Recipe(mat, 0.9f).AddInput(wood, 2f);
            Assert.AreEqual(45f, BomProductionRules.MaxOutput(ry, cs2), 1e-3f);
        }

        // --- BOM-5 消費財需要 ---
        [Test]
        public void Consumer_DemandFulfillmentConsume()
        {
            int food = CommodityCatalog.Register("食品", CommodityCategory.消費財).id;
            Assert.AreEqual(100f, ConsumerDemandRules.Demand(100f, 1.0f), 1e-3f);
            Assert.AreEqual(0.8f, ConsumerDemandRules.Fulfillment(80f, 100f), 1e-4f);
            Assert.AreEqual(0.8f, ConsumerDemandRules.LivingStandardFactor(0.5f, 0.6f), 1e-4f); // Lerp(0.6,1,0.5)
            Assert.AreEqual(-2f, ConsumerDemandRules.SupportDelta(0.8f, 10f), 1e-4f);

            var cs = new CommodityStock();
            cs.Add(food, 80f);
            float shortage = ConsumerDemandRules.Consume(cs, food, 100f);
            Assert.AreEqual(20f, shortage, 1e-3f); // 80しか食べられず20不足
            Assert.AreEqual(0f, cs.Get(food), 1e-3f);
        }

        // --- BOM-6 連鎖（繊維→布→衣類） ---
        [Test]
        public void Tick_ChainFiberToClothing()
        {
            int fiber = CommodityCatalog.Register("繊維", CommodityCategory.原材料).id;
            int cloth = CommodityCatalog.Register("布", CommodityCategory.中間財).id;
            int clothing = CommodityCatalog.Register("衣類", CommodityCategory.消費財).id;
            var clothR = new Recipe(cloth).AddInput(fiber, 2f);       // 布←繊維×2
            var clothingR = new Recipe(clothing).AddInput(cloth, 2f); // 衣類←布×2

            var cs = new CommodityStock();
            cs.Add(fiber, 80f);
            var ordered = new List<Recipe> { clothR, clothingR }; // 上流→下流
            BomTickRules.RunChain(cs, ordered, _ => 1000f);          // 目標は潤沢＝投入律速

            Assert.AreEqual(0f, cs.Get(fiber), 1e-3f);  // 繊維80→布40 で使い切り
            Assert.AreEqual(0f, cs.Get(cloth), 1e-3f);  // 布40→衣類20 で使い切り
            Assert.AreEqual(20f, cs.Get(clothing), 1e-3f); // 繊維80→…→衣類20
        }
    }
}
