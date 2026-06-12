using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 代表生産チェーン 森林→木材→建材→住宅（#2091）：在庫(VCHAIN-1)/林業(2)/製材(3)/建設(4)/居住需要(5)/Tick(6)。
    /// </summary>
    public class SupplyChainTests
    {
        // --- VCHAIN-1 在庫 ---
        [Test]
        public void Stock_GetAdd()
        {
            var cs = new ChainStock(500f);
            cs.Add(SupplyChainGood.木材, 80f);
            cs.Add(SupplyChainGood.建材, 36f);
            cs.Add(SupplyChainGood.住宅, 7f);
            Assert.AreEqual(80f, cs.Get(SupplyChainGood.木材), 1e-3f);
            Assert.AreEqual(36f, cs.Get(SupplyChainGood.建材), 1e-3f);
            Assert.AreEqual(7f, cs.Get(SupplyChainGood.住宅), 1e-3f);
            cs.Add(SupplyChainGood.木材, -200f); // 非負クランプ
            Assert.AreEqual(0f, cs.Get(SupplyChainGood.木材), 1e-3f);
        }

        // --- VCHAIN-2 林業（森林→木材） ---
        [Test]
        public void Forestry_RegrowHarvestOverharvest()
        {
            Assert.AreEqual(600f, ForestryRules.Regrow(500f, 1000f, 0.4f), 1e-3f);  // 500+0.4×500×0.5
            Assert.AreEqual(1000f, ForestryRules.Regrow(1000f, 1000f, 0.4f), 1e-3f); // 満杯は増えない
            Assert.AreEqual(60f, ForestryRules.SustainableYield(600f, 0.1f), 1e-3f);
            Assert.AreEqual(80f, ForestryRules.Harvest(600f, 80f), 1e-3f);
            Assert.AreEqual(600f, ForestryRules.Harvest(600f, 700f), 1e-3f); // 森林が上限
            Assert.IsTrue(ForestryRules.IsOverharvest(80f, 600f, 0.1f));  // 80>60＝枯れる
            Assert.IsFalse(ForestryRules.IsOverharvest(50f, 600f, 0.1f));
        }

        // --- VCHAIN-3 製材（木材→建材・ManufacturerRules委譲） ---
        [Test]
        public void Sawmill_WoodToMaterials()
        {
            Assert.AreEqual(100f, SawmillRules.GrossMaterials(100f, 300f, 2f), 1e-3f); // 目標律速
            Assert.AreEqual(50f, SawmillRules.GrossMaterials(100f, 100f, 2f), 1e-3f);  // 木材律速（100/2）
            Assert.AreEqual(90f, SawmillRules.MaterialOutput(100f, 0.9f), 1e-3f);       // 歩留まり
            Assert.AreEqual(200f, SawmillRules.WoodConsumed(100f, 2f), 1e-3f);
        }

        // --- VCHAIN-4 建設（建材→住宅・ConstructionRules委譲） ---
        [Test]
        public void Construction_MaterialsToHouses()
        {
            Assert.AreEqual(16f, ConstructionChainRules.BuildableHouses(80f, 5f), 1e-3f);
            Assert.AreEqual(16f, ConstructionChainRules.HousesBuilt(20f, 80f, 5f), 1e-3f); // 建材律速
            Assert.AreEqual(10f, ConstructionChainRules.HousesBuilt(10f, 80f, 5f), 1e-3f); // 能力律速
            Assert.AreEqual(80f, ConstructionChainRules.MaterialsConsumed(16f, 5f), 1e-3f);
            Assert.AreEqual(1600f, ConstructionChainRules.HousingValue(16f, 100f), 1e-3f);
            Assert.AreEqual(640f, ConstructionChainRules.HousingProfit(16f, 100f, 60f), 1e-3f); // 1600-960
        }

        // --- VCHAIN-5 居住需要・充足・劣化 ---
        [Test]
        public void Housing_DemandOccupancyDepreciate()
        {
            Assert.AreEqual(30f, HousingDemandRules.HousingDemand(100f, 0.3f), 1e-3f);
            Assert.AreEqual(1f, HousingDemandRules.Occupancy(30f, 30f), 1e-4f);
            Assert.AreEqual(0.5f, HousingDemandRules.Occupancy(15f, 30f), 1e-4f);
            Assert.AreEqual(1f, HousingDemandRules.Occupancy(45f, 30f), 1e-4f); // 余剰はclamp
            Assert.AreEqual(15f, HousingDemandRules.Shortage(15f, 30f), 1e-3f);
            Assert.AreEqual(98f, HousingDemandRules.Depreciate(100f, 0.02f), 1e-3f);
            Assert.AreEqual(-5f, HousingDemandRules.ShortageSupportDelta(0.5f, 10f), 1e-4f);
            Assert.AreEqual(0.85f, HousingDemandRules.LivingStandardFactor(0.5f, 0.7f), 1e-4f); // Lerp(0.7,1,0.5)
        }

        // --- VCHAIN-6 Tick（1パス：森林→木材→建材→住宅→充足） ---
        [Test]
        public void Tick_FullChainPass()
        {
            var p = new SupplyChainParams
            {
                forestCapacity = 1000f, regenRate = 0.4f, harvestRate = 0.1f, harvestPace = 80f,
                woodPerMaterial = 2f, sawmillPace = 100f, sawmillYield = 0.9f,
                materialPerHouse = 5f, buildPace = 20f, perCapitaHousing = 0.3f, housingDepreciation = 0f,
            };
            var cs = new ChainStock(500f);
            var r = SupplyChainTickRules.TickYear(cs, 100f, p);

            Assert.AreEqual(80f, r.woodHarvested, 1e-3f);       // 伐採（過伐採）
            Assert.IsTrue(r.overharvest);
            Assert.AreEqual(36f, r.materialsProduced, 1e-3f);   // 製材（木材40→建材36）
            Assert.AreEqual(7.2f, r.housesBuilt, 1e-3f);        // 建設（建材36→住宅7.2）
            Assert.AreEqual(30f, r.housingDemand, 1e-3f);
            Assert.AreEqual(0.24f, r.occupancy, 1e-3f);         // 7.2/30＝住宅不足
            // 在庫：森林は伐採で減り、木材/建材は使い切り、住宅が積まれた
            Assert.AreEqual(520f, cs.forest, 1e-3f);            // 600−80
            Assert.AreEqual(0f, cs.Get(SupplyChainGood.木材), 1e-3f);
            Assert.AreEqual(0f, cs.Get(SupplyChainGood.建材), 1e-3f);
            Assert.AreEqual(7.2f, cs.Get(SupplyChainGood.住宅), 1e-3f);

            Assert.DoesNotThrow(() => SupplyChainTickRules.TickYear(null, 100f, p)); // null安全
        }
    }
}
