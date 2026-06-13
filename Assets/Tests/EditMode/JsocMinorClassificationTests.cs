using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 職業の小分類（日本標準職業分類 JSOC 小分類を参考・curate 済み・#110）：台帳の整合（コード一意・親中分類が有効・親大分類が中分類経由で一致）／
    /// 本作固有（宇宙設定）職業の存在／POPの代表小分類が中分類・大分類と一致。<see cref="JsocMinorClassification"/>。
    /// </summary>
    public class JsocMinorClassificationTests
    {
        [Test]
        public void TableNotEmpty_AndAllConsistent()
        {
            Assert.Greater(JsocMinorClassification.Count, 0);
            Assert.AreEqual(JsocMinorClassification.Count, JsocMinorClassification.All.Count);

            var seen = new HashSet<string>();
            foreach (var g in JsocMinorClassification.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(g.code), "コードが空");
                Assert.IsFalse(string.IsNullOrEmpty(g.name), $"{g.code} の名称が空");
                Assert.IsTrue(seen.Add(g.code), $"コード {g.code} が重複");
                // 親中分類が有効（1〜73）＝中分類台帳に存在する
                Assert.IsNotNull(JsocMiddleClassification.ByCode(g.middleCode), $"{g.code} の親中分類 {g.middleCode} が無効");
                // 親大分類は中分類経由で一意に決まる（小分類→中分類→大分類の階層が通る）
                Assert.AreEqual(JsocMiddleClassification.MajorOf(g.middleCode), JsocMinorClassification.MajorOf(g.code));
            }
        }

        [Test]
        public void ByCode_AndLookups()
        {
            Assert.IsNull(JsocMinorClassification.ByCode("999"));
            Assert.IsNull(JsocMinorClassification.ByCode(""));
            // 宇宙船操縦士＝輸送機械運転（中分類62 船舶・航空機運転）配下・本作固有
            Assert.AreEqual(62, JsocMinorClassification.MiddleOf("622"));
            Assert.AreEqual(OccupationCategory.輸送機械運転, JsocMinorClassification.MajorOf("622"));
            Assert.AreEqual("宇宙船操縦士（航宙士）", JsocMinorClassification.Name("622"));
            Assert.IsTrue(JsocMinorClassification.IsSetting("622"));
            // 採掘従事者＝建設採掘・JSOC由来
            Assert.AreEqual(OccupationCategory.建設採掘, JsocMinorClassification.MajorOf("691"));
            Assert.IsFalse(JsocMinorClassification.IsSetting("691"));
        }

        [Test]
        public void SettingMinors_AreSpaceOccupations()
        {
            var setting = JsocMinorClassification.SettingMinors();
            Assert.Greater(setting.Count, 0);
            Assert.AreEqual(JsocMinorClassification.CountSetting, setting.Count);
            foreach (var g in setting) Assert.IsTrue(g.isSetting);
            // 代表的な宇宙設定職業が含まれる
            var codes = new HashSet<string>();
            foreach (var g in setting) codes.Add(g.code);
            Assert.IsTrue(codes.Contains("622"), "宇宙船操縦士");
            Assert.IsTrue(codes.Contains("092"), "テラフォーミング技師");
            Assert.IsTrue(codes.Contains("692"), "小惑星・宇宙採掘員");
            Assert.IsTrue(codes.Contains("431"), "宇宙艦隊将兵");
            Assert.IsTrue(codes.Contains("602"), "宇宙列車運転士");
            Assert.IsTrue(codes.Contains("462"), "垂直農法従事者");
        }

        [Test]
        public void InMiddle_GroupsUnderParent()
        {
            // 中分類62（船舶・航空機運転）配下に 船舶/宇宙船/ワープ航法 が並ぶ
            var under62 = JsocMinorClassification.InMiddle(62);
            Assert.AreEqual(3, under62.Count);
            foreach (var g in under62) Assert.AreEqual(62, g.middleCode);
        }

        [Test]
        public void RepresentativeMinor_Occupation_MatchesHierarchy()
        {
            foreach (Occupation o in System.Enum.GetValues(typeof(Occupation)))
            {
                string code = JsocMinorClassification.RepresentativeMinor(o);
                if (o == Occupation.無職) { Assert.AreEqual("", code); continue; }
                var g = JsocMinorClassification.ByCode(code);
                Assert.IsNotNull(g, $"{o} の代表小分類 {code} が台帳に無い");
                // 小分類→中分類が、6種の代表中分類と一致
                Assert.AreEqual(JsocMiddleClassification.RepresentativeMiddle(o), g.middleCode,
                    $"{o} の代表小分類の親中分類が代表中分類と不一致");
                // 小分類→大分類が、6種の大分類写像と一致
                Assert.AreEqual(OccupationClassificationRules.MajorGroupOf(o), JsocMinorClassification.MajorOf(code));
            }
            Assert.AreEqual("461", JsocMinorClassification.RepresentativeMinor(Occupation.農民));
            Assert.AreEqual("531", JsocMinorClassification.RepresentativeMinor(Occupation.工員));
            Assert.AreEqual("691", JsocMinorClassification.RepresentativeMinor(Occupation.鉱員));
            Assert.AreEqual("252", JsocMinorClassification.RepresentativeMinor(Occupation.官吏));
            Assert.AreEqual("431", JsocMinorClassification.RepresentativeMinor(Occupation.軍属));
        }
    }
}
