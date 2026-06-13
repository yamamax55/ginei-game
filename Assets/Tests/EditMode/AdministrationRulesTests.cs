using NUnit.Framework;
using Ginei;
using AdP = Ginei.AdministrationRules.AdminParams;

namespace Ginei.Tests
{
    /// <summary>
    /// 宰相の行政が内政（安定度）へ及ぼす寄与（名実の乖離＝朝廷の権威で減衰）を固定する。
    /// </summary>
    public class AdministrationRulesTests
    {
        [Test]
        public void StabilityContribution_ScalesWithCompetence_AndAuthority()
        {
            var p = AdP.Default; // 上限+12
            Assert.AreEqual(12f, AdministrationRules.StabilityContribution(1f, 1f, p), 1e-4f);
            Assert.AreEqual(6f, AdministrationRules.StabilityContribution(0.5f, 1f, p), 1e-4f);
            // 名実の乖離：朝廷の権威0なら有能でも寄与0（官職は名誉職）
            Assert.AreEqual(0f, AdministrationRules.StabilityContribution(1f, 0f, p), 1e-4f);
            // 権威半分で半減
            Assert.AreEqual(6f, AdministrationRules.StabilityContribution(1f, 0.5f, p), 1e-4f);
            // competence はクランプ
            Assert.AreEqual(12f, AdministrationRules.StabilityContribution(2f, 1f, p), 1e-4f);
        }

        [Test]
        public void StabilityContribution_Person_UsesCivilAptitude_AndMerit()
        {
            var p = AdP.Default;
            // 文才80（運営/情報80）・考課なし・権威1 → 0.8×12 = 9.6
            var able = new Person(1, "宰相", Faction.帝国, PersonRole.文民) { operation = 80, intelligence = 80 };
            Assert.AreEqual(9.6f, AdministrationRules.StabilityContribution(able, 1f, p), 1e-3f);

            // 考第が低い宰相は実効が落ちる（同じ文才でも下下評定で減る）
            var poorMerit = new Person(2, "宰相", Faction.帝国, PersonRole.文民)
            { operation = 80, intelligence = 80, merit = new OfficialMerit(2) };
            for (int i = 0; i < 3; i++) MeritEvaluationRules.Record(poorMerit.merit, MeritRating.下下);
            Assert.Less(AdministrationRules.StabilityContribution(poorMerit, 1f, p),
                        AdministrationRules.StabilityContribution(able, 1f, p));

            // 空席（null）は寄与0
            Assert.AreEqual(0f, AdministrationRules.StabilityContribution(null, 1f, p), 1e-4f);
        }

        [Test]
        public void GovernanceRules_AdminBonus_RaisesEquilibrium_DefaultUnchanged()
        {
            float baseTarget = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.民生);
            float boosted = GovernanceRules.EquilibriumStability(1f, 0f, true, false, GovernancePolicy.民生, adminBonus: 10f);
            Assert.AreEqual(baseTarget + 10f, boosted, 1e-3f);
            // 既定オーバーロード（adminBonus 省略）は従来と同値
            float legacy = GovernanceRules.EquilibriumStability(1f, 0f, true, false);
            Assert.AreEqual(baseTarget, legacy, 1e-3f);
        }
    }
}
