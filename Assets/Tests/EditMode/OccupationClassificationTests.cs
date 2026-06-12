using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 職業の標準分類（日本標準職業分類 JSOC 大分類を参考・#110）：POP6種→大分類の写像／人物→大分類／
    /// 惑星類型別の既定構成／JSOC記号／徴募源（保安）／少数6種からの写像。<see cref="OccupationClassificationRules"/>。
    /// </summary>
    public class OccupationClassificationTests
    {
        // --- POP の少数6種 → JSOC 大分類 ---
        [Test]
        public void SimpleOccupation_MapsToJsocMajorGroup()
        {
            Assert.AreEqual(OccupationCategory.農林漁業, OccupationClassificationRules.MajorGroupOf(Occupation.農民));
            Assert.AreEqual(OccupationCategory.生産工程, OccupationClassificationRules.MajorGroupOf(Occupation.工員));
            Assert.AreEqual(OccupationCategory.建設採掘, OccupationClassificationRules.MajorGroupOf(Occupation.鉱員)); // 採掘従事者
            Assert.AreEqual(OccupationCategory.事務,     OccupationClassificationRules.MajorGroupOf(Occupation.官吏));
            Assert.AreEqual(OccupationCategory.保安,     OccupationClassificationRules.MajorGroupOf(Occupation.軍属)); // 徴募源
            Assert.AreEqual(OccupationCategory.無職,     OccupationClassificationRules.MajorGroupOf(Occupation.無職));
        }

        // --- 人物 → JSOC 大分類 ---
        [Test]
        public void Person_Politician_IsManagerial()
        {
            var p = new Person(1, "議員", Faction.同盟, PersonRole.文民) { isPolitician = true };
            Assert.AreEqual(OccupationCategory.管理, OccupationClassificationRules.MajorGroupOf(p));
        }

        [Test]
        public void Person_Military_IsSecurity()
        {
            var p = new Person(2, "提督", Faction.帝国, PersonRole.軍人) { leadership = 80 };
            Assert.AreEqual(OccupationCategory.保安, OccupationClassificationRules.MajorGroupOf(p)); // 自衛官＝階級に依らず保安
        }

        [Test]
        public void Person_CivilTechnocrat_IsProfessional()
        {
            // 技術才（研究/技術/計画/生産）が文才（運営/情報）以上＝専門技術
            var p = new Person(3, "技術士官", Faction.同盟, PersonRole.文民)
            { research = 80, engineering = 80, planning = 60, production = 60, operation = 20, intelligence = 20 };
            Assert.AreEqual(OccupationCategory.専門技術, OccupationClassificationRules.MajorGroupOf(p));
        }

        [Test]
        public void Person_CivilAdministrator_IsClerical()
        {
            // 文才が技術才より高い文民＝事務
            var p = new Person(4, "行政官", Faction.同盟, PersonRole.文民)
            { operation = 70, intelligence = 70, research = 10, engineering = 10, planning = 10, production = 10 };
            Assert.AreEqual(OccupationCategory.事務, OccupationClassificationRules.MajorGroupOf(p));
        }

        // --- 惑星類型別の既定構成（合計1・基幹群） ---
        [Test]
        public void Default_SumsToOne_AndPrimaryGroup()
        {
            foreach (SystemType t in System.Enum.GetValues(typeof(SystemType)))
            {
                var prof = OccupationClassificationRules.Default(t);
                Assert.AreEqual(1f, prof.Total, 1e-4f, $"{t} の構成合計が1でない");
                // 基幹大分類が最多群（無職を除く）であること
                Assert.AreEqual(OccupationClassificationRules.PrimaryGroup(t), prof.Dominant(), $"{t} の最多群が基幹群でない");
            }
        }

        [Test]
        public void Default_Residential_TertiaryStandsOut()
        {
            var res = OccupationClassificationRules.Default(SystemType.居住);
            // 居住は事務が最多＝三次産業が広がる
            Assert.AreEqual(OccupationCategory.事務, res.Dominant());
            Assert.Greater(res.Share(OccupationCategory.販売), res.Share(OccupationCategory.生産工程)); // 販売 > 生産工程
        }

        // --- JSOC 記号・名称 ---
        [Test]
        public void JsocCodes()
        {
            Assert.AreEqual("A", OccupationClassificationRules.JsocCode(OccupationCategory.管理));
            Assert.AreEqual("F", OccupationClassificationRules.JsocCode(OccupationCategory.保安));
            Assert.AreEqual("K", OccupationClassificationRules.JsocCode(OccupationCategory.運搬清掃包装));
            Assert.AreEqual("—", OccupationClassificationRules.JsocCode(OccupationCategory.無職)); // 職業外
            Assert.AreEqual("保安職業従事者", OccupationClassificationRules.GroupName(OccupationCategory.保安));
        }

        // --- 徴募源・分類ヘルパ ---
        [Test]
        public void SecurityAndHelpers()
        {
            Assert.IsTrue(OccupationClassificationRules.IsSecurityForce(OccupationCategory.保安));
            Assert.IsFalse(OccupationClassificationRules.IsSecurityForce(OccupationCategory.事務));
            Assert.IsTrue(OccupationClassificationRules.IsProfessional(OccupationCategory.専門技術));
            Assert.IsTrue(OccupationClassificationRules.IsManagerial(OccupationCategory.管理));
        }

        // --- 少数6種 Workforce → JSOC 構成への写像 ---
        [Test]
        public void Classify_FromSimpleWorkforce()
        {
            var w = OccupationRules.Default(SystemType.工業); // 工員主の6種構成
            var prof = OccupationClassificationRules.Classify(w);
            // 合計は保存される
            Assert.AreEqual(w.Total, prof.Total, 1e-4f);
            // 工員→生産工程 にシェアが移っている
            Assert.AreEqual(w.Share(Occupation.工員), prof.Share(OccupationCategory.生産工程), 1e-4f);
            // 軍属→保安＝徴募源シェア
            Assert.AreEqual(w.Share(Occupation.軍属), OccupationClassificationRules.RecruitableShare(prof), 1e-4f);
            // 工業は生産工程が最多群
            Assert.AreEqual(OccupationCategory.生産工程, prof.Dominant());
        }
    }
}
