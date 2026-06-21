using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星系名プール（世界の名峰ベース・<see cref="MountainSystemNames"/>）を固定する：プールに重複が無いこと、
    /// 巡回アクセス、日本の名峰を含まないこと、他プール（旗艦＝世界遺産）と被らないこと。
    /// </summary>
    public class MountainSystemNamesTests
    {
        [Test]
        public void Pool_IsNonEmpty_AndUnique()
        {
            Assert.Greater(MountainSystemNames.Count, 0);
            var seen = new HashSet<string>();
            foreach (var n in MountainSystemNames.Names)
            {
                Assert.IsFalse(string.IsNullOrEmpty(n));
                Assert.IsTrue(seen.Add(n), $"重複した星系名: {n}");
            }
        }

        [Test]
        public void At_Wraps()
        {
            int c = MountainSystemNames.Count;
            Assert.AreEqual(MountainSystemNames.At(0), MountainSystemNames.At(c));
            Assert.AreEqual(MountainSystemNames.At(c - 1), MountainSystemNames.At(-1));
        }

        [Test]
        public void Pool_ExcludesJapaneseMountains()
        {
            // 日本の名峰は星系名に使わない（除外の意図を固定）。
            string[] japanese = { "フジ", "富士", "ヤリガタケ", "ホタカ", "タテヤマ", "オンタケ", "アサマ", "ダイセツ" };
            var pool = new HashSet<string>(MountainSystemNames.Names);
            foreach (var j in japanese)
                Assert.IsFalse(pool.Contains(j), $"日本の名峰は除外のはず: {j}");
        }

        [Test]
        public void Pool_DoesNotOverlapHeritageShipNames()
        {
            // 旗艦名（世界遺産プール）と星系名（名峰プール）は混同しないよう被らせない。
            var ships = new HashSet<string>(HeritageShipNames.Names);
            foreach (var n in MountainSystemNames.Names)
                Assert.IsFalse(ships.Contains(n), $"旗艦名プールと重複: {n}");
        }
    }
}
