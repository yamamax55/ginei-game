using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 工作機械メーカー（マザーマシン・#2023・<see cref="MachineToolRules"/>）を固定する：マザーマシン(MTL-1)、受注産業(MTL-2)、
    /// 設備投資循環(MTL-3)、精度と数値制御(MTL-4)、戦略物資・輸出規制(MTL-5)。
    /// </summary>
    public class MachineToolTests
    {
        // ===== MTL-1 マザーマシン =====
        [Test]
        public void MotherMachine_QualityCeilingAndIndustrialBase()
        {
            // 工作機械精度0.9が下流製造の品質上限＝基準100×0.9=90
            Assert.AreEqual(90f, MachineToolRules.ManufacturingQualityCeiling(0.9f, 100f), 1e-3f);
            Assert.AreEqual(0.8f, MachineToolRules.IndustrialBaseFactor(80f, 100f), 1e-3f);   // 国産率8割
            Assert.AreEqual(1.0f, MachineToolRules.IndustrialBaseFactor(120f, 100f), 1e-3f);  // 自給（クランプ）
        }

        // ===== MTL-2 受注産業と受注残 =====
        [Test]
        public void OrderBusiness_BacklogAndBookToBill()
        {
            Assert.AreEqual(600f, MachineToolRules.BacklogAfterOrders(500f, 300f, 200f), 1e-3f);
            Assert.AreEqual(1.5f, MachineToolRules.BookToBillRatio(300f, 200f), 1e-3f); // 1超＝受注拡大の先行指標
            Assert.AreEqual(3f, MachineToolRules.DeliveryLeadTime(600f, 200f), 1e-3f);
        }

        // ===== MTL-3 設備投資循環 =====
        [Test]
        public void CapexCycle_MostVolatile()
        {
            // 設備投資+10%×増幅5 = 需要+50%（建機の加速度3より大きく振れる）
            Assert.AreEqual(150f, MachineToolRules.ToolDemand(0.1f, 100f, 5f), 1e-3f);
            Assert.AreEqual(50f, MachineToolRules.ToolDemand(-0.1f, 100f, 5f), 1e-3f);  // 不況も大きく
        }

        // ===== MTL-4 精度と数値制御 =====
        [Test]
        public void Precision_RdAndCeiling()
        {
            Assert.AreEqual(0.7f, MachineToolRules.PrecisionLevel(0.5f, 4f, 0.1f, 0.99f), 1e-3f); // 0.5×1.4
            Assert.AreEqual(0.99f, MachineToolRules.PrecisionLevel(0.5f, 20f, 0.1f, 0.99f), 1e-3f); // 物理上限
        }

        // ===== MTL-5 戦略物資・輸出規制 =====
        [Test]
        public void StrategicGoods_ExportControlAndWeaponsEnablement()
        {
            Assert.IsTrue(MachineToolRules.IsStrategicGoods(0.95f, 0.9f));   // 高精度＝戦略物資
            Assert.IsFalse(MachineToolRules.IsStrategicGoods(0.8f, 0.9f));
            // 規制下の高精度機は敵対先へ輸出不可（COCOM/東芝機械事件型）
            Assert.IsFalse(MachineToolRules.CanExport(0.95f, 0.9f, true, true));
            Assert.IsTrue(MachineToolRules.CanExport(0.95f, 0.9f, false, true));  // 非敵対なら可
            Assert.IsTrue(MachineToolRules.CanExport(0.8f, 0.9f, true, true));    // 低精度＝規制対象外
            // 高精度機が高度兵器の製造を解禁する dual-use
            Assert.IsTrue(MachineToolRules.WeaponsEnablement(0.95f, 0.9f));
            Assert.IsFalse(MachineToolRules.WeaponsEnablement(0.8f, 0.9f));       // 精度不足で作れない
        }
    }
}
