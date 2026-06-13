using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>ADM-4 提督相性：性向の近さ・敵勢力・野心家どうしの反目→参謀補完/軍団結束/寝返り。</summary>
    public class AffinityRulesTests
    {
        private static AdmiralData A(int humility, int ambition, Faction f = Faction.帝国)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.humility = humility; a.ambition = ambition; a.faction = f;
            a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [Test]
        public void Affinity_FromTraitsFactionAndRivalry()
        {
            Assert.AreEqual(0.5f, AffinityRules.Affinity(null, A(50, 50)), 1e-4f); // 不明=中立
            Assert.AreEqual(1.0f, AffinityRules.Affinity(A(50, 50), A(50, 50)), 1e-4f); // 同性向
            Assert.AreEqual(0.9f, AffinityRules.Affinity(A(50, 50), A(70, 50)), 1e-4f); // 謙虚さ差20
            Assert.AreEqual(0.8f, AffinityRules.Affinity(A(50, 80), A(50, 80)), 1e-4f); // 野心家どうし反目-0.2
            Assert.AreEqual(0.7f, AffinityRules.Affinity(A(50, 50), A(50, 50, Faction.同盟)), 1e-4f); // 敵対-0.3
        }

        [Test]
        public void DerivedFactors()
        {
            Assert.AreEqual(1.5f, AffinityRules.StaffSynergyFactor(1.0f), 1e-4f);
            Assert.AreEqual(1.0f, AffinityRules.StaffSynergyFactor(0.5f), 1e-4f);
            Assert.AreEqual(0.5f, AffinityRules.StaffSynergyFactor(0f), 1e-4f);

            Assert.AreEqual(1.15f, AffinityRules.CorpsCohesionFactor(1.0f), 1e-4f);
            Assert.AreEqual(0.85f, AffinityRules.CorpsCohesionFactor(0f), 1e-4f);

            Assert.AreEqual(-0.25f, AffinityRules.DefectionModifier(1.0f), 1e-4f); // 高相性=離反抑制
            Assert.AreEqual(0.25f, AffinityRules.DefectionModifier(0f), 1e-4f);    // 低相性=離反増
            Assert.AreEqual(0f, AffinityRules.DefectionModifier(0.5f), 1e-4f);
        }
    }
}
