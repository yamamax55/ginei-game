using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 地方自治体（<see cref="LocalGovernmentRules"/>・中央集権↔地方分権）を固定する：自治度の安定ボーナス（能力依存）、
    /// 税収の中央/地方配分、分離独立リスク（高自治×低統合）、政体の既定自治度、中央集権度。
    /// </summary>
    public class LocalGovernmentTests
    {
        [Test]
        public void Centralization_IsInverseOfAutonomy()
        {
            Assert.AreEqual(0.7f, LocalGovernmentRules.Centralization(0.3f), 1e-4f);
            Assert.AreEqual(1f, LocalGovernmentRules.Centralization(0f), 1e-4f);
            Assert.AreEqual(0f, LocalGovernmentRules.Centralization(1f), 1e-4f);
        }

        [Test]
        public void LocalStabilityBonus_DependsOnCompetenceAndAutonomy()
        {
            // 有能な自治ほど安定ボーナスが高い／無能なら効かない（能力律速）
            Assert.AreEqual(LocalGovernmentRules.MaxLocalStabilityBonus, LocalGovernmentRules.LocalStabilityBonus(1f, 1f), 1e-3f);
            Assert.AreEqual(0f, LocalGovernmentRules.LocalStabilityBonus(1f, 0f), 1e-4f); // 自治あっても無能なら0
            Assert.AreEqual(0f, LocalGovernmentRules.LocalStabilityBonus(0f, 1f), 1e-4f); // 中央直轄なら地方ボーナスなし
            Assert.Greater(LocalGovernmentRules.LocalStabilityBonus(0.8f, 0.7f),
                           LocalGovernmentRules.LocalStabilityBonus(0.4f, 0.7f)); // 自治が高いほど高い
        }

        [Test]
        public void RevenueShare_CentralVsLocal()
        {
            // 自治0＝全額中央、自治1＝最低割合だけ中央（残りは地方留保）
            Assert.AreEqual(1f, LocalGovernmentRules.CentralRevenueShare(0f), 1e-4f);
            Assert.AreEqual(LocalGovernmentRules.MinCentralShare, LocalGovernmentRules.CentralRevenueShare(1f), 1e-4f);
            Assert.AreEqual(0.7f, LocalGovernmentRules.CentralRevenueShare(0.5f), 1e-4f); // Lerp(1,0.4,0.5)
            // 中央＋地方＝1
            Assert.AreEqual(1f, LocalGovernmentRules.CentralRevenueShare(0.5f) + LocalGovernmentRules.LocalRevenueShare(0.5f), 1e-4f);
            // 自治が高いほど中央取り分は減る
            Assert.Less(LocalGovernmentRules.CentralRevenueShare(0.8f), LocalGovernmentRules.CentralRevenueShare(0.2f));
        }

        [Test]
        public void SeparatismRisk_HighAutonomyLowIntegration()
        {
            Assert.AreEqual(0.72f, LocalGovernmentRules.SeparatismRisk(0.9f, 0.2f), 1e-3f); // 0.9*(1-0.2)
            Assert.AreEqual(0f, LocalGovernmentRules.SeparatismRisk(0.9f, 1f), 1e-4f);       // 完全統合＝離れない
            Assert.Less(LocalGovernmentRules.SeparatismRisk(0.2f, 0.2f),
                        LocalGovernmentRules.SeparatismRisk(0.9f, 0.2f));                    // 中央直轄は分離しにくい
        }

        [Test]
        public void DefaultAutonomy_ByIdeology()
        {
            Assert.AreEqual(0.2f, LocalGovernmentRules.DefaultAutonomy("専制"), 1e-4f);   // 中央集権
            Assert.AreEqual(0.6f, LocalGovernmentRules.DefaultAutonomy("民主"), 1e-4f);   // 地方分権
            Assert.AreEqual(0.5f, LocalGovernmentRules.DefaultAutonomy(null), 1e-4f);
        }
    }
}
