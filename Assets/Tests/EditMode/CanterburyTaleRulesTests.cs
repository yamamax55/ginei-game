using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>社交場の語り（カンタベリー物語）：得意素養→ジャンル写像・決定論・寸言の整形・宿主の口上。</summary>
    public class CanterburyTaleRulesTests
    {
        [Test]
        public void GenreFor_MapsDominantAspect_Deterministic()
        {
            // 武勇＝騎士/粉屋、知略＝学僧/免罪符、統率＝騎士/郷士、政務＝商人/バースの女房。seed で択一。
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.騎士の物語, CanterburyTaleRules.GenreFor(TalentAspect.武勇, 0));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.粉屋の物語, CanterburyTaleRules.GenreFor(TalentAspect.武勇, 1));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.学僧の物語, CanterburyTaleRules.GenreFor(TalentAspect.知略, 0));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.免罪符売りの物語, CanterburyTaleRules.GenreFor(TalentAspect.知略, 1));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.騎士の物語, CanterburyTaleRules.GenreFor(TalentAspect.統率, 0));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.郷士の物語, CanterburyTaleRules.GenreFor(TalentAspect.統率, 1));
            Assert.AreEqual(CanterburyTaleRules.TaleGenre.商人の物語, CanterburyTaleRules.GenreFor(TalentAspect.政務, 0));
        }

        [Test]
        public void GenreFor_SameSeed_AlwaysSameGenre()
        {
            for (int seed = -50; seed <= 50; seed++)
                Assert.AreEqual(CanterburyTaleRules.GenreFor(TalentAspect.武勇, seed),
                                CanterburyTaleRules.GenreFor(TalentAspect.武勇, seed));
        }

        [Test]
        public void Tale_IncludesGenreName_AndIsStable()
        {
            string t = CanterburyTaleRules.Tale(CanterburyTaleRules.TaleGenre.免罪符売りの物語, 7);
            StringAssert.Contains("『免罪符売りの物語』を語る——", t);
            // 決定論：同じ seed なら同じ語り。
            Assert.AreEqual(t, CanterburyTaleRules.Tale(CanterburyTaleRules.TaleGenre.免罪符売りの物語, 7));
        }

        [Test]
        public void Tale_FromAspect_PicksFittingGenre()
        {
            // 政務×seed0 → 商人の物語。
            StringAssert.Contains("『商人の物語』", CanterburyTaleRules.Tale(TalentAspect.政務, 0));
        }

        [Test]
        public void AllGenres_HaveNonEmptyNameAndFragment()
        {
            foreach (CanterburyTaleRules.TaleGenre g in System.Enum.GetValues(typeof(CanterburyTaleRules.TaleGenre)))
            {
                Assert.IsFalse(string.IsNullOrEmpty(CanterburyTaleRules.GenreName(g)));
                // どの seed でも寸言つきの語りが出る（インデックス範囲安全）。
                for (int seed = 0; seed < 8; seed++)
                    Assert.IsTrue(CanterburyTaleRules.Tale(g, seed).Length > CanterburyTaleRules.GenreName(g).Length + 6);
            }
        }

        [Test]
        public void HostRemark_NonEmpty_Deterministic()
        {
            Assert.IsFalse(string.IsNullOrEmpty(CanterburyTaleRules.HostRemark(0)));
            Assert.AreEqual(CanterburyTaleRules.HostRemark(3), CanterburyTaleRules.HostRemark(3));
            // 負 seed でも安全（範囲外参照しない）。
            Assert.IsFalse(string.IsNullOrEmpty(CanterburyTaleRules.HostRemark(-99)));
        }
    }
}
