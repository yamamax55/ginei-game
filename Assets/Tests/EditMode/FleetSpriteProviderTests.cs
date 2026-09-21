using Ginei.Data;
using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class FleetSpriteProviderTests
    {
#if UNITY_5_3_OR_NEWER
        private Texture2D imperialTexture;
        private Texture2D allianceTexture;
#endif
        private Sprite imperial;
        private Sprite alliance;

        [SetUp]
        public void SetUp()
        {
            FleetSpriteProvider.Clear();
#if UNITY_5_3_OR_NEWER
            imperialTexture = new Texture2D(2, 2);
            allianceTexture = new Texture2D(2, 2);
            imperial = Sprite.Create(imperialTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
            alliance = Sprite.Create(allianceTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f);
#else
            imperial = new Sprite();
            alliance = new Sprite();
#endif
            FleetSpriteProvider.Register(Faction.帝国, imperial);
            FleetSpriteProvider.Register(Faction.同盟, alliance);
        }

        [TearDown]
        public void TearDown()
        {
            FleetSpriteProvider.Clear();
            Object.DestroyImmediate(imperial);
            Object.DestroyImmediate(alliance);
#if UNITY_5_3_OR_NEWER
            Object.DestroyImmediate(imperialTexture);
            Object.DestroyImmediate(allianceTexture);
#endif
        }

        [Test]
        public void RegisteredFactionsResolveByEnumAndName()
        {
            Assert.AreSame(imperial, FleetSpriteProvider.SpriteForFaction(Faction.帝国));
            Assert.AreSame(alliance, FleetSpriteProvider.SpriteForFaction(Faction.同盟));
            Assert.AreSame(imperial, FleetSpriteProvider.SpriteForFactionName("帝国"));
            Assert.AreSame(alliance, FleetSpriteProvider.SpriteForFactionName("同盟"));
        }

        [Test]
        public void AdditionalFactionNameCanBeRegisteredAndUnknownIsNull()
        {
            FleetSpriteProvider.Register("辺境連合", alliance);

            Assert.AreSame(alliance, FleetSpriteProvider.SpriteForFactionName("辺境連合"));
            Assert.IsNull(FleetSpriteProvider.SpriteForFactionName("未登録勢力"));
            Assert.IsNull(FleetSpriteProvider.SpriteForFactionName(null));
        }

        [Test]
        public void NullRegistrationRemovesMappingWithoutResourceReload()
        {
            FleetSpriteProvider.Register(Faction.帝国, null);

            Assert.IsNull(FleetSpriteProvider.SpriteForFaction(Faction.帝国));
            Assert.IsNull(FleetSpriteProvider.SpriteForFactionName("帝国"));
            Assert.AreSame(alliance, FleetSpriteProvider.SpriteForFaction(Faction.同盟));
        }
    }
}
