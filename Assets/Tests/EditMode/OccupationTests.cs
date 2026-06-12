using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// POP の職業（就労・#110 職業版・<see cref="OccupationRules"/>）を固定する：類型別の既定構成（合計1・基幹職が最多）、
    /// 基幹職マッピング、就労実数＝生産年齢×シェア（コホート連動）、就業率/徴募源/失業圧/適所度。
    /// </summary>
    public class OccupationTests
    {
        [Test]
        public void Default_SumsToOne_PrimaryIsLargest()
        {
            foreach (SystemType t in new[] { SystemType.工業, SystemType.農業, SystemType.鉱業, SystemType.居住 })
            {
                Workforce w = OccupationRules.Default(t);
                Assert.AreEqual(1f, w.Total, 1e-4f, $"{t} の職業シェア合計は1");
            }
            // 生産類型は基幹職が最多
            Assert.AreEqual(Occupation.工員, OccupationRules.PrimaryOccupation(SystemType.工業));
            Assert.AreEqual(Occupation.農民, OccupationRules.PrimaryOccupation(SystemType.農業));
            Assert.AreEqual(Occupation.鉱員, OccupationRules.PrimaryOccupation(SystemType.鉱業));
            Assert.AreEqual(Occupation.官吏, OccupationRules.PrimaryOccupation(SystemType.居住));
            Workforce ind = OccupationRules.Default(SystemType.工業);
            Assert.Greater(ind.Share(Occupation.工員), ind.Share(Occupation.農民)); // 工業は工員が農民より多い
        }

        [Test]
        public void Workers_IsWorkingAgeTimesShare_UsesCohort()
        {
            // コホートあり＝生産年齢(working)×シェア
            var p = new Province(1, "民主", 100f) { systemType = SystemType.工業 };
            p.demographics = new Population(20f, 60f, 20f); // working=60
            p.workforce = OccupationRules.Default(SystemType.工業);
            float expectedFactory = 60f * p.workforce.Share(Occupation.工員);
            Assert.AreEqual(expectedFactory, OccupationRules.Workers(p, Occupation.工員), 1e-3f);
        }

        [Test]
        public void Workers_NoCohort_UsesPopulationWorkingShare()
        {
            // コホート未設定＝population×既定生産年齢比×シェア（後方互換の見積り）
            var p = new Province(2, "専制", 200f) { systemType = SystemType.農業 };
            float workingAge = 200f * PopulationDynamicsRules.DefaultWorkingShare;
            Workforce def = OccupationRules.Default(SystemType.農業);
            Assert.AreEqual(workingAge * def.Share(Occupation.農民),
                            OccupationRules.Workers(p, Occupation.農民), 1e-2f);
        }

        [Test]
        public void Employment_Recruit_Unemployment_Alignment()
        {
            var p = new Province(3, "民主", 100f) { systemType = SystemType.工業 };
            p.demographics = new Population(20f, 60f, 20f);
            p.workforce = OccupationRules.Default(SystemType.工業);

            // 就業率＝1−無職
            Assert.AreEqual(1f - p.workforce.Share(Occupation.無職), OccupationRules.EmploymentRate(p), 1e-4f);
            // 徴募源＝軍属の実数
            Assert.AreEqual(60f * p.workforce.Share(Occupation.軍属), OccupationRules.RecruitablePool(p), 1e-3f);
            // 失業圧＝無職シェア
            Assert.AreEqual(p.workforce.Share(Occupation.無職), OccupationRules.UnemploymentPressure(p), 1e-4f);
            // 適所度＝基幹職（工業→工員）のシェア
            Assert.AreEqual(p.workforce.Share(Occupation.工員), OccupationRules.AlignmentFactor(p), 1e-4f);
        }

        [Test]
        public void Workforce_NullDefaultsToTypeEstimate()
        {
            // workforce 未設定でも類型既定で見積れる（後方互換）
            var p = new Province(4, "民主", 100f) { systemType = SystemType.鉱業 };
            Assert.IsNull(p.workforce);
            Assert.Greater(OccupationRules.Workers(p, Occupation.鉱員), 0f);
            Assert.AreEqual(OccupationRules.Default(SystemType.鉱業).Share(Occupation.鉱員),
                            OccupationRules.AlignmentFactor(p), 1e-4f);
        }
    }
}
