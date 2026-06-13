using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>特技カタログ：定義の解決・件数・登録/上書き・既定復元。</summary>
    public class TalentCatalogTests
    {
        [SetUp]
        public void Setup() => TalentCatalog.ResetToDefaults();

        [TearDown]
        public void Cleanup() => TalentCatalog.ResetToDefaults();

        [Test]
        public void Defaults_ContainKnownTalents()
        {
            Assert.AreEqual(18, TalentCatalog.Count);
            var oni = TalentCatalog.Get("鬼神");
            Assert.IsNotNull(oni);
            Assert.AreEqual(TalentAspect.武勇, oni.aspect);
            Assert.AreEqual(TalentKind.特性, oni.kind);
            Assert.AreEqual(TalentEffect.攻撃強化, oni.effect);

            Assert.AreEqual(TalentKind.戦法, TalentCatalog.Get("火計").kind);
            Assert.AreEqual(TalentAspect.政務, TalentCatalog.Get("富国").aspect);
            Assert.IsNull(TalentCatalog.Get("存在しない特技"));
            Assert.IsNull(TalentCatalog.Get((Talent)null));
        }

        [Test]
        public void All_IsStablySortedById()
        {
            var all = TalentCatalog.All;
            Assert.AreEqual(18, all.Count);
            for (int i = 1; i < all.Count; i++)
                Assert.LessOrEqual(string.CompareOrdinal(all[i - 1].id, all[i].id), 0);
        }

        [Test]
        public void Register_AddsOrOverrides_ResetRestores()
        {
            TalentCatalog.Register(new TalentDef("固有特技", "暗黒物質砲", TalentAspect.知略, TalentKind.戦法, TalentEffect.範囲攻撃戦法, 0.6f));
            Assert.AreEqual(19, TalentCatalog.Count);
            Assert.AreEqual("暗黒物質砲", TalentCatalog.Get("固有特技").talentName);

            // 上書き
            TalentCatalog.Register(new TalentDef("鬼神", "改・鬼神", TalentAspect.武勇, TalentKind.特性, TalentEffect.攻撃強化, 0.20f));
            Assert.AreEqual("改・鬼神", TalentCatalog.Get("鬼神").talentName);

            TalentCatalog.ResetToDefaults();
            Assert.AreEqual(18, TalentCatalog.Count);
            Assert.AreEqual("鬼神", TalentCatalog.Get("鬼神").talentName);
        }
    }
}
