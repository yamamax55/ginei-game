using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦場回収を固定する：残骸プール＝双方喪失×残骸化率、回収権＝支配度比例、修理/解体の排他配分、
    /// 戦場保持の利得差。境界を担保。
    /// </summary>
    public class SalvageRulesTests
    {
        private static readonly SalvageParams P = SalvageParams.Default;
        // 残骸化0.6/戦力回収0.2/資源回収0.5

        [Test]
        public void WreckPool_FromBothSidesLosses()
        {
            // 双方500喪失＝1000×0.6=600 の残骸
            Assert.AreEqual(600f, SalvageRules.WreckPool(500f, 500f, P), 1e-4f);
            Assert.AreEqual(0f, SalvageRules.WreckPool(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void ClaimableWrecks_ByBattlefieldControl()
        {
            Assert.AreEqual(600f, SalvageRules.ClaimableWrecks(600f, 1f), 1e-4f);  // 勝者＝全取り
            Assert.AreEqual(0f, SalvageRules.ClaimableWrecks(600f, 0f), 1e-5f);    // 敗走側＝全損
            Assert.AreEqual(300f, SalvageRules.ClaimableWrecks(600f, 0.5f), 1e-4f);
        }

        [Test]
        public void Recovery_ExclusiveAllocation()
        {
            // 全部修理へ：600×1×0.2=120 戦力、資源0
            Assert.AreEqual(120f, SalvageRules.RecoveredStrength(600f, 1f, P), 1e-4f);
            Assert.AreEqual(0f, SalvageRules.RecoveredResources(600f, 1f, P), 1e-5f);
            // 全部解体へ：戦力0、600×1×0.5=300 資源
            Assert.AreEqual(0f, SalvageRules.RecoveredStrength(600f, 0f, P), 1e-5f);
            Assert.AreEqual(300f, SalvageRules.RecoveredResources(600f, 0f, P), 1e-4f);
            // 半々＝両方半分
            Assert.AreEqual(60f, SalvageRules.RecoveredStrength(600f, 0.5f, P), 1e-4f);
            Assert.AreEqual(150f, SalvageRules.RecoveredResources(600f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ControlPremium_ValueOfHoldingTheField()
        {
            // 残骸600・全修理：保持120 − 放棄0 = 120
            Assert.AreEqual(120f, SalvageRules.ControlPremium(600f, 1f, P), 1e-4f);
            // 残骸なし＝差なし
            Assert.AreEqual(0f, SalvageRules.ControlPremium(0f, 1f, P), 1e-5f);
        }
    }
}
