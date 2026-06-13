using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 目安箱の「信認」（MEYASU-2 #1298）を固定する：信認は人物でなく箱（国王/政治家/地方）ごと、
    /// 地方は regionKey ごとに独立、実効傾聴＝箱の信認×国家傾聴度、閾値割れで壁紙化、不使用で中庸へ収束。
    /// </summary>
    public class CredibilityRulesTests
    {
        private static readonly CredibilityParams P = CredibilityParams.Default; // default 0.5 / wallpaper 0.15 / neutral 0.5 / decay 0.05

        [Test]
        public void UntouchedBox_ReturnsDefault()
        {
            var c = new BoxCredibility(Faction.帝国);
            Assert.AreEqual(0.5f, CredibilityRules.Of(c, BoxKind.国王, P), 1e-4f);
            Assert.AreEqual(0.5f, CredibilityRules.Of(c, BoxKind.政治家, P), 1e-4f);
            // null 安全（既定値を返す）
            Assert.AreEqual(0.5f, CredibilityRules.Of(null, BoxKind.国王, P), 1e-4f);
        }

        [Test]
        public void Adjust_RaisesLowers_AndClamps()
        {
            var c = new BoxCredibility(Faction.帝国);
            CredibilityRules.Adjust(c, BoxKind.政治家, 0.3f, P);
            Assert.AreEqual(0.8f, CredibilityRules.Of(c, BoxKind.政治家, P), 1e-4f); // 0.5+0.3

            CredibilityRules.Adjust(c, BoxKind.政治家, 0.5f, P);
            Assert.AreEqual(1f, CredibilityRules.Of(c, BoxKind.政治家, P), 1e-4f);   // 上限クランプ

            CredibilityRules.Adjust(c, BoxKind.政治家, -2f, P);
            Assert.AreEqual(0f, CredibilityRules.Of(c, BoxKind.政治家, P), 1e-4f);   // 下限クランプ
        }

        [Test]
        public void RegionalBoxes_AreIndependentPerRegion()
        {
            var c = new BoxCredibility(Faction.同盟);
            CredibilityRules.Adjust(c, BoxKind.地方, -0.4f, P, "ハイネセン");
            Assert.AreEqual(0.1f, CredibilityRules.Of(c, BoxKind.地方, P, "ハイネセン"), 1e-4f);
            // 別の地方は未接触＝既定値（疎・独立）
            Assert.AreEqual(0.5f, CredibilityRules.Of(c, BoxKind.地方, P, "オーディン"), 1e-4f);
            // 中央箱とも独立
            Assert.AreEqual(0.5f, CredibilityRules.Of(c, BoxKind.国王, P), 1e-4f);
        }

        [Test]
        public void Heed_IsBoxValueTimesGlobalDeference()
        {
            var c = new BoxCredibility(Faction.帝国);
            CredibilityRules.Adjust(c, BoxKind.国王, 0.3f, P);   // 0.8
            c.globalDeference = 0.5f;
            Assert.AreEqual(0.4f, CredibilityRules.Heed(c, BoxKind.国王, P), 1e-4f); // 0.8*0.5
        }

        [Test]
        public void Wallpapered_WhenEffectiveHeedBelowThreshold()
        {
            var c = new BoxCredibility(Faction.帝国);
            // 健全な箱（既定0.5・傾聴1.0）は壁紙化しない
            Assert.IsFalse(CredibilityRules.IsWallpapered(c, BoxKind.政治家, P));

            CredibilityRules.Adjust(c, BoxKind.国王, -0.4f, P); // 0.1 < 0.15 → 壁紙化
            Assert.IsTrue(CredibilityRules.IsWallpapered(c, BoxKind.国王, P));
        }

        [Test]
        public void GlobalDeferenceCollapse_WallpapersEvenHealthyBox()
        {
            var c = new BoxCredibility(Faction.帝国); // 箱は既定0.5（健全）
            CredibilityRules.AdjustGlobal(c, -2f);     // 国家傾聴度が失墜＝0へクランプ
            Assert.AreEqual(0f, c.globalDeference, 1e-4f);
            Assert.AreEqual(0f, CredibilityRules.Heed(c, BoxKind.政治家, P), 1e-4f);
            Assert.IsTrue(CredibilityRules.IsWallpapered(c, BoxKind.政治家, P)); // 全体失墜は健全な箱も読まれなくする
        }

        [Test]
        public void Decay_DriftsTowardNeutral_BothDirections()
        {
            var c = new BoxCredibility(Faction.帝国);
            CredibilityRules.Adjust(c, BoxKind.政治家, 0.4f, P);            // 0.9（中庸より上）
            CredibilityRules.Adjust(c, BoxKind.地方, -0.4f, P, "イゼルローン"); // 0.1（中庸より下）

            CredibilityRules.Decay(c, 2f, P); // maxDelta = 0.05*2 = 0.1
            Assert.AreEqual(0.8f, CredibilityRules.Of(c, BoxKind.政治家, P), 1e-4f);            // 0.9→0.8
            Assert.AreEqual(0.2f, CredibilityRules.Of(c, BoxKind.地方, P, "イゼルローン"), 1e-4f); // 0.1→0.2
        }
    }
}
