using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 法令カタログ（#法令タブ・<see cref="LawCatalog"/>）を固定する：各種別に条文があること、
    /// 種別フィルタが効くこと、政体フィルタ（共通＋固有）が効くこと。
    /// </summary>
    public class LawCatalogTests
    {
        [Test]
        public void EveryCategory_HasArticles()
        {
            foreach (LawCategory cat in System.Enum.GetValues(typeof(LawCategory)))
            {
                var demo = LawCatalog.For(cat, "民主");
                var auto = LawCatalog.For(cat, "専制");
                Assert.Greater(demo.Count, 0, $"民主の {cat} に条文が無い");
                Assert.Greater(auto.Count, 0, $"専制の {cat} に条文が無い");
            }
        }

        [Test]
        public void For_ReturnsOnlyRequestedCategory()
        {
            foreach (var a in LawCatalog.For(LawCategory.憲法, "民主"))
                Assert.AreEqual(LawCategory.憲法, a.category);
        }

        [Test]
        public void For_IncludesCommonAndMatchingIdeology_ExcludesOther()
        {
            var demo = LawCatalog.For(LawCategory.憲法, "民主");
            bool hasCommon = false, hasDemocratic = false, hasAutocratic = false;
            foreach (var a in demo)
            {
                if (a.IsCommon) hasCommon = true;
                else if (a.ideology == "民主") hasDemocratic = true;
                else if (a.ideology == "専制") hasAutocratic = true;
            }
            Assert.IsTrue(hasCommon, "共通の憲法条文が含まれない");
            Assert.IsTrue(hasDemocratic, "民主固有の憲法条文が含まれない");
            Assert.IsFalse(hasAutocratic, "専制固有の条文が民主の結果に混ざっている");
        }

        [Test]
        public void EmptyIdeology_ReturnsCommonOnly()
        {
            foreach (var a in LawCatalog.For(LawCategory.法律, ""))
                Assert.IsTrue(a.IsCommon, "空思想で固有法が混ざった");
        }
    }
}
