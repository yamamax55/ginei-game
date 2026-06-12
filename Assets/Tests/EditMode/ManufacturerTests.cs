using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// メーカー（製造業・#2016・<see cref="ManufacturerRules"/>）を固定する：原材料と製造(MFG-1)、歩留まり/品質(MFG-2)、
    /// 研究開発→製品力(MFG-3)、ブランド→価格プレミアム(MFG-4)、経験曲線・利潤(MFG-5)。
    /// </summary>
    public class ManufacturerTests
    {
        // ===== MFG-1 原材料と製造 =====
        [Test]
        public void Production_MaterialBottleneck()
        {
            Assert.AreEqual(500f, ManufacturerRules.ProducibleUnits(1000f, 2f), 1e-3f);
            // 原材料で500しか作れない＝目標600でも500（材料ボトルネック）
            Assert.AreEqual(500f, ManufacturerRules.ManufacturedOutput(600f, 1000f, 2f), 1e-3f);
            Assert.AreEqual(400f, ManufacturerRules.ManufacturedOutput(400f, 1000f, 2f), 1e-3f);
            Assert.AreEqual(1000f, ManufacturerRules.MaterialCost(500f, 2f), 1e-3f);
        }

        // ===== MFG-2 歩留まり・品質 =====
        [Test]
        public void Yield_GoodAndDefect()
        {
            Assert.AreEqual(450f, ManufacturerRules.GoodUnits(500f, 0.9f), 1e-3f);
            Assert.AreEqual(50f, ManufacturerRules.DefectUnits(500f, 0.9f), 1e-3f);
            Assert.AreEqual(100f, ManufacturerRules.DefectLossCost(50f, 2f), 1e-3f);
        }

        // ===== MFG-3 研究開発→製品力 =====
        [Test]
        public void Rd_ProductivityAndYieldImprovement()
        {
            Assert.AreEqual(1.1f, ManufacturerRules.RdProductivityFactor(5f, 0.02f), 1e-4f);
            // R&D投資50で歩留まり 0.9→0.95
            Assert.AreEqual(0.95f, ManufacturerRules.ImproveYield(0.9f, 50f, 0.001f, 0.99f), 1e-4f);
            // 投資過大でも上限でクランプ
            Assert.AreEqual(0.99f, ManufacturerRules.ImproveYield(0.9f, 200f, 0.001f, 0.99f), 1e-4f);
        }

        // ===== MFG-4 ブランド→価格プレミアム =====
        [Test]
        public void Brand_PricePremium()
        {
            Assert.AreEqual(0.3f, ManufacturerRules.BrandPremium(0.6f, 0.5f), 1e-4f);
            Assert.AreEqual(130f, ManufacturerRules.BrandedPrice(100f, 0.6f, 0.5f), 1e-3f); // 100×1.3
        }

        // ===== MFG-5 経験曲線・利潤 =====
        [Test]
        public void LearningCurve_AndProfit()
        {
            // 累積400/基準100＝2倍増→100×0.9^2 = 81（作るほど安くなる）
            Assert.AreEqual(81f, ManufacturerRules.LearningCurveUnitCost(100f, 400f, 100f, 0.9f), 1e-2f);
            Assert.AreEqual(100f, ManufacturerRules.LearningCurveUnitCost(100f, 100f, 100f, 0.9f), 1e-3f); // 基準＝学習なし
            Assert.AreEqual(100f, ManufacturerRules.LearningCurveUnitCost(100f, 50f, 100f, 0.9f), 1e-3f);  // 基準未満は負の学習なし
            Assert.AreEqual(49f, ManufacturerRules.UnitProfit(130f, 81f), 1e-3f);
            // 良品450×(価格130−単価81)=22050
            Assert.AreEqual(22050f, ManufacturerRules.ManufacturingProfit(450f, 130f, 81f), 1e-1f);
        }
    }
}
