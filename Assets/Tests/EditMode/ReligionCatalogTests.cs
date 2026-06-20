using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>
    /// 宗教カタログ（#172-175・カタログ駆動）の純データを固定。名前引き・フォールバック/null安全・
    /// 全件列挙・特性の範囲(0..1)・名前付き伝統の差別化（仏教は最も非暴力・カトリックは最も布教熱）を担保。
    /// </summary>
    public class ReligionCatalogTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void For_既知の宗教を引ける()
        {
            ReligionDef d = ReligionCatalog.For("仏教");
            Assert.AreEqual("仏教", d.name);
            Assert.AreEqual(ReligionFamily.ダルマ系, d.family);
        }

        [Test]
        public void For_未登録名はNoneフォールバック()
        {
            ReligionDef d = ReligionCatalog.For("ゾロアスター教");
            Assert.AreEqual("", d.name);
            Assert.AreEqual(0f, d.proselytism, Tol);
        }

        [Test]
        public void For_NullとEmptyはNone()
        {
            Assert.AreEqual("", ReligionCatalog.For(null).name);
            Assert.AreEqual("", ReligionCatalog.For("").name);
        }

        [Test]
        public void ForReligion_Nullは安全にNone()
        {
            Assert.AreEqual("", ReligionCatalog.ForReligion(null).name);
            var r = new Religion("イスラム教", 0.7f);
            Assert.AreEqual("イスラム教", ReligionCatalog.ForReligion(r).name);
        }

        [Test]
        public void IsKnown_実在のみTrue()
        {
            Assert.IsTrue(ReligionCatalog.IsKnown("キリスト教"));
            Assert.IsFalse(ReligionCatalog.IsKnown("無し"));
            Assert.IsFalse(ReligionCatalog.IsKnown(""));
            Assert.IsFalse(ReligionCatalog.IsKnown(null));
        }

        [Test]
        public void All_主要伝統がそろっている()
        {
            string[] expected = { "ユダヤ教", "キリスト教", "カトリック", "プロテスタント", "ロシア正教", "イスラム教", "仏教", "ヒンドゥー教" };
            foreach (var name in expected)
                Assert.IsTrue(ReligionCatalog.IsKnown(name), $"{name} がカタログに無い");
            Assert.AreEqual(expected.Length, ReligionCatalog.Count);
        }

        [Test]
        public void All_全特性が0to1の範囲()
        {
            foreach (var d in ReligionCatalog.All)
            {
                Assert.GreaterOrEqual(d.proselytism, 0f); Assert.LessOrEqual(d.proselytism, 1f);
                Assert.GreaterOrEqual(d.militancy, 0f); Assert.LessOrEqual(d.militancy, 1f);
                Assert.GreaterOrEqual(d.socialCohesion, 0f); Assert.LessOrEqual(d.socialCohesion, 1f);
                Assert.GreaterOrEqual(d.tolerance, 0f); Assert.LessOrEqual(d.tolerance, 1f);
            }
        }

        [Test]
        public void 仏教は最も非暴力で最も寛容()
        {
            ReligionDef buddhism = ReligionCatalog.For("仏教");
            foreach (var d in ReligionCatalog.All)
            {
                Assert.LessOrEqual(buddhism.militancy, d.militancy + Tol, $"{d.name} より戦闘的");
                Assert.GreaterOrEqual(buddhism.tolerance, d.tolerance - Tol, $"{d.name} より不寛容");
            }
        }

        [Test]
        public void カトリックは最も布教熱が高い()
        {
            ReligionDef catholic = ReligionCatalog.For("カトリック");
            foreach (var d in ReligionCatalog.All)
                Assert.GreaterOrEqual(catholic.proselytism, d.proselytism - Tol, $"{d.name} の方が布教熱が高い");
        }

        [Test]
        public void コンストラクタは特性をClamp()
        {
            var d = new ReligionDef("x", 2f, -1f, 5f, -0.5f, "y", ReligionFamily.その他);
            Assert.AreEqual(1f, d.proselytism, Tol);
            Assert.AreEqual(0f, d.militancy, Tol);
            Assert.AreEqual(1f, d.socialCohesion, Tol);
            Assert.AreEqual(0f, d.tolerance, Tol);
        }
    }
}
