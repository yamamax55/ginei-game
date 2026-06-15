using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 惑星の土地＝ネームド財産の所有（#2019/#2070・<see cref="PlanetLandRules"/>）を固定する：
    /// 私有/国家持分・評価額の地価同期・国家の残余持分確保（完全所有）・共産の国有化・冪等。
    /// </summary>
    public class PlanetLandRulesTests
    {
        [SetUp]
        public void Clear() => PropertyDeedRegistry.Clear();

        private static PropertyDeed PersonDeed(int systemId, int personId, float share)
        {
            var d = new PropertyDeed(0, systemId, share, 1000f) { ownerKind = AssetOwnerKind.人物, ownerPersonId = personId, rentRate = 0.04f };
            return PropertyDeedRegistry.Register(d);
        }

        [Test]
        public void Shares_PrivateAndState()
        {
            PersonDeed(5, 1, 0.2f);
            PersonDeed(5, 2, 0.3f);
            Assert.AreEqual(0.5f, PlanetLandRules.PrivateShare(5), 1e-4f);
            Assert.AreEqual(0.5f, PlanetLandRules.StateShare(5), 1e-4f); // 国家は残り
            // 別惑星は独立
            Assert.AreEqual(0f, PlanetLandRules.PrivateShare(9), 1e-4f);
            Assert.AreEqual(1f, PlanetLandRules.StateShare(9), 1e-4f);
        }

        [Test]
        public void DeedMarketValue_ShareOfPlanet()
        {
            var d = PersonDeed(5, 1, 0.2f); // baseValue 1000 × 0.2
            Assert.AreEqual(200f, PlanetLandRules.DeedMarketValue(d), 1e-3f);
        }

        [Test]
        public void SyncValuations_TracksPlanetLandValue()
        {
            PersonDeed(5, 1, 0.2f);
            PersonDeed(5, 2, 0.3f);
            PlanetLandRules.SyncValuations(5, 2500f);
            foreach (var d in PropertyDeedRegistry.DeedsOnSystem(5))
                Assert.AreEqual(2500f, d.baseValue, 1e-3f);
        }

        [Test]
        public void EnsureStateDeed_StateOwnsResidual_FullyOwned()
        {
            PersonDeed(5, 1, 0.2f);
            PersonDeed(5, 2, 0.3f);
            PropertyDeed state = PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, canPrivatize: true);
            Assert.IsTrue(state.IsFactionOwned);
            Assert.AreEqual(Faction.帝国, state.ownerFaction);
            Assert.AreEqual(0.5f, state.share, 1e-4f);     // 1 − 私有0.5
            Assert.AreEqual(1000f, state.baseValue, 1e-3f);
            Assert.IsTrue(PlanetLandRules.IsFullyOwned(5)); // 国家＋個人＝1
        }

        [Test]
        public void EnsureStateDeed_Idempotent()
        {
            PersonDeed(5, 1, 0.4f);
            PropertyDeed s1 = PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, true);
            PropertyDeed s2 = PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1200f, true);
            Assert.AreSame(s1, s2);                          // 同じ国家権利証（重複生成しない）
            Assert.AreEqual(0.6f, s2.share, 1e-4f);
            Assert.AreEqual(1200f, s2.baseValue, 1e-3f);     // 地価に同期
            // 国家権利証は1枚だけ（持分>0）
            int stateDeeds = 0;
            foreach (var d in PropertyDeedRegistry.DeedsOnSystem(5))
                if (d.IsFactionOwned && d.share > 0f) stateDeeds++;
            Assert.AreEqual(1, stateDeeds);
        }

        [Test]
        public void EnsureStateDeed_EmptyPlanet_StateOwnsAll()
        {
            PropertyDeed state = PlanetLandRules.EnsureStateDeed(7, Faction.同盟, 500f, true);
            Assert.AreEqual(1f, state.share, 1e-4f); // 私有なし＝国家が全所有
            Assert.IsTrue(PlanetLandRules.IsFullyOwned(7));
        }

        [Test]
        public void EnsureStateDeed_Communism_Nationalizes()
        {
            PersonDeed(5, 1, 0.2f);
            PersonDeed(5, 2, 0.3f);
            PlanetLandRules.EnsureStateDeed(5, Faction.帝国, 1000f, canPrivatize: false);
            // 私有持分は没収＝国有化
            Assert.AreEqual(0f, PlanetLandRules.PrivateShare(5), 1e-4f);
            Assert.AreEqual(1f, PlanetLandRules.StateShare(5), 1e-4f);
            Assert.IsTrue(PlanetLandRules.IsFullyOwned(5));
            // 旧・人物権利証は国家所有へ
            foreach (var d in PropertyDeedRegistry.DeedsOnSystem(5))
                Assert.IsTrue(d.IsFactionOwned);
        }

        [Test]
        public void NationalizePrivateDeeds_ConvertsToState()
        {
            PersonDeed(5, 1, 0.2f);
            PlanetLandRules.NationalizePrivateDeeds(5, Faction.同盟);
            foreach (var d in PropertyDeedRegistry.DeedsOnSystem(5))
            {
                Assert.IsTrue(d.IsFactionOwned);
                Assert.AreEqual(Faction.同盟, d.ownerFaction);
                Assert.AreEqual(0f, d.share, 1e-4f);
            }
        }
    }
}
