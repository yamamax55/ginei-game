using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 星系名→恒星モデルの割り当て（<see cref="StarModelRules"/>）を固定する。
    /// モデルは名前ごとに1つずつ用意されるので、①51名すべてが別々の番号に解決すること
    /// ②番号とファイル名（Star_00〜）の対応が崩れないこと ③未知の名前でも毎回同じ恒星になること
    /// （起動やロードのたびに星の見た目が変わると「同じ星系だ」と認識できなくなる）を検証する。
    /// </summary>
    public class StarModelRulesTests
    {
        [Test]
        public void AllPooledNames_MapToTheirOwnIndex()
        {
            string[] names = MountainSystemNames.Names;
            for (int i = 0; i < names.Length; i++)
                Assert.AreEqual(i, StarModelRules.IndexForName(names[i]),
                    $"「{names[i]}」が並び順どおりの番号に解決しない（FBXの連番とずれる）");
        }

        [Test]
        public void AllPooledNames_AreCoveredAndDistinct()
        {
            string[] names = MountainSystemNames.Names;
            var seen = new HashSet<int>();
            for (int i = 0; i < names.Length; i++)
            {
                int idx = StarModelRules.IndexForName(names[i]);
                Assert.IsTrue(idx >= 0 && idx < StarModelRules.ModelCount, $"「{names[i]}」の番号が範囲外");
                Assert.IsTrue(seen.Add(idx), $"「{names[i]}」の番号 {idx} が他と重複している");
            }
            // 全51名が漏れなく別々の番号を占める＝用意する FBX の枚数と一致する。
            Assert.AreEqual(names.Length, seen.Count, "名前とモデルが1対1になっていない");
            Assert.AreEqual(StarModelRules.ModelCount, seen.Count);
        }

        [Test]
        public void ModelCount_MatchesTheNamePool()
        {
            Assert.AreEqual(MountainSystemNames.Count, StarModelRules.ModelCount);
            Assert.AreEqual(51, StarModelRules.ModelCount, "名前プールの件数が変わった＝FBXの枚数も合わせる必要がある");
        }

        [Test]
        public void ResourcePath_IsZeroPaddedTwoDigits()
        {
            Assert.AreEqual("Models/Stars/Star_00", StarModelRules.ResourcePathFor(0));
            Assert.AreEqual("Models/Stars/Star_09", StarModelRules.ResourcePathFor(9));
            Assert.AreEqual("Models/Stars/Star_50", StarModelRules.ResourcePathFor(50));
        }

        [Test]
        public void ResourcePath_ForEveryPooledName_IsInRange()
        {
            string[] names = MountainSystemNames.Names;
            for (int i = 0; i < names.Length; i++)
            {
                string path = StarModelRules.ResourcePathForName(names[i]);
                Assert.IsTrue(path.StartsWith(StarModelRules.ResourceFolder), $"「{names[i]}」のパスが置き場と違う");
                Assert.AreEqual(StarModelRules.ResourceFolder + i.ToString("00"), path);
            }
        }

        [Test]
        public void UnknownName_IsDeterministicAndInRange()
        {
            // プールに無い名前でも「毎回同じ」＝同じ星系はいつ見ても同じ恒星に見える。
            const string unknown = "架空のどこかの峰";
            int first = StarModelRules.IndexForName(unknown);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(first, StarModelRules.IndexForName(unknown), "未知名の割り当てが呼び出しごとに変わる");
            Assert.IsTrue(first >= 0 && first < StarModelRules.ModelCount, "未知名の番号が範囲外＝モデルが読めない");
        }

        [Test]
        public void UnknownNames_SpreadAcrossTheModels()
        {
            // 未知名がすべて同じ1体に寄ると、見た目で星系を区別できなくなる。ばらけることを確認する。
            var seen = new HashSet<int>();
            for (int i = 0; i < 60; i++) seen.Add(StarModelRules.IndexForName("未知星系" + i));
            Assert.Greater(seen.Count, 10, "未知名の割り当てが偏りすぎている");
        }

        [Test]
        public void NullOrEmptyName_ResolvesToTheFirstModel()
        {
            Assert.AreEqual(0, StarModelRules.IndexForName(null));
            Assert.AreEqual(0, StarModelRules.IndexForName(""));
            Assert.AreEqual("Models/Stars/Star_00", StarModelRules.ResourcePathForName(null));
        }

        [Test]
        public void ResourcePath_WrapsOutOfRangeIndex()
        {
            int n = StarModelRules.ModelCount;
            Assert.AreEqual(StarModelRules.ResourcePathFor(0), StarModelRules.ResourcePathFor(n));
            Assert.AreEqual(StarModelRules.ResourcePathFor(n - 1), StarModelRules.ResourcePathFor(-1));
        }

        [Test]
        public void StableIndex_IsSafeForDegenerateInput()
        {
            Assert.AreEqual(0, StarModelRules.StableIndex("なにか", 0));
            Assert.AreEqual(0, StarModelRules.StableIndex(null, 10));
            Assert.AreEqual(0, StarModelRules.StableIndex("", 10));
            int v = StarModelRules.StableIndex("エベレスト", 10);
            Assert.IsTrue(v >= 0 && v < 10);
        }
    }
}
