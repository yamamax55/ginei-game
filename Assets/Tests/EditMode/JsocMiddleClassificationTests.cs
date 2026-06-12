using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 職業の中分類（日本標準職業分類 JSOC 中分類73群を参考・#110）：台帳の整合（73件・連番・各件が有効な大分類）／
    /// 大分類別の件数／コード照会／POP・人物の代表中分類が大分類と一致。<see cref="JsocMiddleClassification"/>。
    /// </summary>
    public class JsocMiddleClassificationTests
    {
        [Test]
        public void HasSeventyThreeMiddleGroups()
        {
            Assert.AreEqual(73, JsocMiddleClassification.Count);
            Assert.AreEqual(73, JsocMiddleClassification.All.Count);
        }

        [Test]
        public void CodesAreSequentialAndUnique_1to73()
        {
            var seen = new HashSet<int>();
            for (int code = 1; code <= 73; code++)
            {
                var g = JsocMiddleClassification.ByCode(code);
                Assert.IsNotNull(g, $"コード{code}が無い");
                Assert.AreEqual(code, g.code);
                Assert.IsFalse(string.IsNullOrEmpty(g.name), $"コード{code}の名称が空");
                Assert.IsTrue(seen.Add(code), $"コード{code}が重複");
            }
            // 範囲外
            Assert.IsNull(JsocMiddleClassification.ByCode(0));
            Assert.IsNull(JsocMiddleClassification.ByCode(74));
        }

        [Test]
        public void CountsPerMajor_SumTo73()
        {
            // JSOC 大分類別の中分類件数（A4/B20/C7/D3/E8/F3/G3/H11/I5/J5/K4）
            Assert.AreEqual(4,  JsocMiddleClassification.CountInMajor(OccupationCategory.管理));
            Assert.AreEqual(20, JsocMiddleClassification.CountInMajor(OccupationCategory.専門技術));
            Assert.AreEqual(7,  JsocMiddleClassification.CountInMajor(OccupationCategory.事務));
            Assert.AreEqual(3,  JsocMiddleClassification.CountInMajor(OccupationCategory.販売));
            Assert.AreEqual(8,  JsocMiddleClassification.CountInMajor(OccupationCategory.サービス));
            Assert.AreEqual(3,  JsocMiddleClassification.CountInMajor(OccupationCategory.保安));
            Assert.AreEqual(3,  JsocMiddleClassification.CountInMajor(OccupationCategory.農林漁業));
            Assert.AreEqual(11, JsocMiddleClassification.CountInMajor(OccupationCategory.生産工程));
            Assert.AreEqual(5,  JsocMiddleClassification.CountInMajor(OccupationCategory.輸送機械運転));
            Assert.AreEqual(5,  JsocMiddleClassification.CountInMajor(OccupationCategory.建設採掘));
            Assert.AreEqual(4,  JsocMiddleClassification.CountInMajor(OccupationCategory.運搬清掃包装));

            int sum = 0;
            foreach (OccupationCategory c in System.Enum.GetValues(typeof(OccupationCategory)))
                sum += JsocMiddleClassification.CountInMajor(c);
            Assert.AreEqual(73, sum); // 無職は0
            Assert.AreEqual(0, JsocMiddleClassification.CountInMajor(OccupationCategory.無職));
        }

        [Test]
        public void InMajor_ConsistentWithParent()
        {
            // すべての中分類について、InMajor バケットと自身の major が整合
            foreach (OccupationCategory c in System.Enum.GetValues(typeof(OccupationCategory)))
                foreach (var g in JsocMiddleClassification.InMajor(c))
                    Assert.AreEqual(c, g.major);
        }

        [Test]
        public void CodeLookups()
        {
            Assert.AreEqual(OccupationCategory.管理,     JsocMiddleClassification.MajorOf(1));   // 管理的公務員
            Assert.AreEqual(OccupationCategory.保安,     JsocMiddleClassification.MajorOf(43));  // 自衛官
            Assert.AreEqual(OccupationCategory.農林漁業, JsocMiddleClassification.MajorOf(46));  // 農業従事者
            Assert.AreEqual(OccupationCategory.生産工程, JsocMiddleClassification.MajorOf(53));  // 製品製造・加工処理
            Assert.AreEqual(OccupationCategory.建設採掘, JsocMiddleClassification.MajorOf(69));  // 採掘従事者
            Assert.AreEqual("自衛官", JsocMiddleClassification.Name(43));
            Assert.AreEqual("01", JsocMiddleClassification.FormatCode(1));
            Assert.AreEqual("73", JsocMiddleClassification.FormatCode(73));
            Assert.AreEqual("—", JsocMiddleClassification.FormatCode(0)); // 範囲外
            Assert.AreEqual(OccupationCategory.無職, JsocMiddleClassification.MajorOf(99));
        }

        // --- 大分類レイヤーとの一貫性（中分類の親＝6種/人物の大分類写像と一致） ---
        [Test]
        public void RepresentativeMiddle_Occupation_MatchesMajor()
        {
            foreach (Occupation o in System.Enum.GetValues(typeof(Occupation)))
            {
                int code = JsocMiddleClassification.RepresentativeMiddle(o);
                if (o == Occupation.無職) { Assert.AreEqual(0, code); continue; }
                Assert.AreEqual(OccupationClassificationRules.MajorGroupOf(o), JsocMiddleClassification.MajorOf(code),
                    $"{o} の代表中分類{code}の親大分類が大分類写像と不一致");
            }
            // 代表中分類の具体値
            Assert.AreEqual(46, JsocMiddleClassification.RepresentativeMiddle(Occupation.農民));
            Assert.AreEqual(53, JsocMiddleClassification.RepresentativeMiddle(Occupation.工員));
            Assert.AreEqual(69, JsocMiddleClassification.RepresentativeMiddle(Occupation.鉱員));
            Assert.AreEqual(43, JsocMiddleClassification.RepresentativeMiddle(Occupation.軍属));
        }

        // ※ネームド人物の中分類は POP 分類に押し込まず別管理（PersonVocationRules）＝人物オーバーロードは持たない。
    }
}
