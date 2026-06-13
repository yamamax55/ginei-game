using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>POP労働・技能の暦境界オーケストレータ（#2026/#2034 配線・<see cref="PopLaborTickRules"/>）：年次の技能形成（教育→OJT収束・教育格差で上限律速）。</summary>
    public class PopLaborTickTests
    {
        [Test]
        public void TickYear_FormsSkillTowardEducationCeiling()
        {
            var p = new Province(1, "", 100f) { workforce = OccupationRules.Default(SystemType.工業) };
            Assert.IsNull(p.skills);

            // 高教育（普及0.9×質0.8→ベースライン0.72・高等は全POP職の前提を満たす）。学習率0.5。
            PopLaborTickRules.TickYear(p, 0.9f, 0.8f, EducationLevel.高等, 0.5f);
            Assert.IsNotNull(p.skills);
            Assert.AreEqual(0.36f, p.skills.Level(Occupation.農民), 1e-4f); // Advance(0,0.72,0.5)
            Assert.AreEqual(0f, p.skills.Level(Occupation.無職), 1e-4f);    // 無職は形成されない

            // 2年目＝上限0.72へさらに収束（経験曲線）
            PopLaborTickRules.TickYear(p, 0.9f, 0.8f, EducationLevel.高等, 0.5f);
            Assert.AreEqual(0.54f, p.skills.Level(Occupation.工員), 1e-4f); // 0.36+(0.72-0.36)*0.5
            Assert.AreEqual(0.54f, PopLaborTickRules.OverallSkill(p), 1e-3f);
        }

        [Test]
        public void TickYear_LowEducationCapsHarderSkills()
        {
            var p = new Province(2, "", 100f) { workforce = OccupationRules.Default(SystemType.居住) };
            // 低教育（初等まで・質0.5・普及1.0→ベースライン0.5）・学習率1.0で一気に上限へ。
            PopLaborTickRules.TickYear(p, 1.0f, 0.5f, EducationLevel.初等, 1.0f);
            // 官吏（事務 難易度0.4・前提=中等）は初等では頭打ち＝0.5×(1-0.4)=0.3
            Assert.AreEqual(0.30f, p.skills.Level(Occupation.官吏), 1e-4f);
            // 軍属（保安 難易度0.5・前提=中等）はさらに低く 0.5×(1-0.5)=0.25
            Assert.AreEqual(0.25f, p.skills.Level(Occupation.軍属), 1e-4f);

            // 翌年も上限に張り付く（教育を上げない限り超えない＝教育格差が技能格差に）
            PopLaborTickRules.TickYear(p, 1.0f, 0.5f, EducationLevel.初等, 1.0f);
            Assert.AreEqual(0.30f, p.skills.Level(Occupation.官吏), 1e-4f);
        }
    }
}
