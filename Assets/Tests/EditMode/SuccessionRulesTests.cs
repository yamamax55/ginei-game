using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// カリスマの日常化＝英雄死後の組織存続（#812/#814/#816 SHINGEN・SPINE-1 #795）を固定する：
    /// 制度化した組織は超えて続き（幕藩）、属人組織は英雄と共に滅ぶ（信玄の轍）。後継者の正統性×カリスマ・
    /// 急な中央集権化（リファクタリング）・カリスマ再現の呪縛（バグ）。
    /// </summary>
    public class SuccessionRulesTests
    {
        private static readonly SuccessionParams P = SuccessionParams.Default; // fragment 0.4 / refactor 0.5

        [Test]
        public void Personalized_Org_Fragments_OnSuccession()
        {
            // 信玄の轍：結束1だが制度化0.1（属人）。後継者は凡庸（正統性0.3・カリスマ0.5）。
            var org = new Organization(1, Faction.帝国, cohesion: 1f, institutionalization: 0.1f, leaderCharisma: 1f);
            var r = SuccessionRules.ResolveSuccession(org, successorLegitimacy: 0.3f, successorCharisma: 0.5f, P);

            Assert.IsTrue(r.fragmented);          // 制度が支えず崩壊
            Assert.IsFalse(r.survived);
            Assert.Less(r.newCohesion, 0.4f);     // 0.235 程度
            Assert.IsTrue(org.fragmented);
        }

        [Test]
        public void Institutionalized_Org_Survives_EvenMediocreHeir()
        {
            // 幕藩：制度化0.9。後継者が凡庸（0.4/0.4）でも存続。
            var org = new Organization(1, Faction.帝国, cohesion: 1f, institutionalization: 0.9f, leaderCharisma: 1f);
            var r = SuccessionRules.ResolveSuccession(org, 0.4f, 0.4f, P);

            Assert.IsTrue(r.survived);
            Assert.IsFalse(r.fragmented);
            Assert.Greater(r.newCohesion, 0.9f);  // 0.916 程度
        }

        [Test]
        public void GreatHeir_CarriesEvenPersonalizedOrg()
        {
            // 属人組織でも、後継者が優秀（正統性1・カリスマ1）なら個人カリスマ分を引き継げる。
            var org = new Organization(1, Faction.帝国, cohesion: 1f, institutionalization: 0.1f, leaderCharisma: 1f);
            var r = SuccessionRules.ResolveSuccession(org, 1f, 1f, P);

            Assert.IsTrue(r.survived);
            Assert.AreEqual(1f, r.newCohesion, 1e-4f); // transfer=1 → cohesion 維持
        }

        [Test]
        public void InvestInstitution_RaisesAndClamps()
        {
            var org = new Organization(1, Faction.帝国, institutionalization: 0.3f);
            SuccessionRules.InvestInstitution(org, 0.4f);
            Assert.AreEqual(0.7f, org.institutionalization, 1e-4f);
            SuccessionRules.InvestInstitution(org, 1f);
            Assert.AreEqual(1f, org.institutionalization, 1e-4f); // 上限
        }

        [Test]
        public void Refactor_Abrupt_UnderPressure_DropsCohesion_AndCanFragment()
        {
            // 中央集権化のリファクタリング失敗（bug3）：急＋外圧で結束が崩れる。
            var org = new Organization(1, Faction.帝国, cohesion: 0.7f);
            float loss = SuccessionRules.Refactor(org, abruptness: 1f, externalPressure: 1f, P);
            Assert.AreEqual(0.5f, loss, 1e-4f);
            Assert.AreEqual(0.2f, org.cohesion, 1e-4f);
            Assert.IsTrue(org.fragmented); // 0.2 < 0.4
        }

        [Test]
        public void Refactor_Gradual_LowPressure_IsSafe()
        {
            var org = new Organization(1, Faction.帝国, cohesion: 0.9f);
            SuccessionRules.Refactor(org, abruptness: 0.2f, externalPressure: 0.2f, P); // loss=0.02
            Assert.AreEqual(0.88f, org.cohesion, 1e-4f);
            Assert.IsFalse(org.fragmented);
        }

        [Test]
        public void MythPressure_HighWhenLowLegitimacy()
        {
            Assert.AreEqual(0.8f, SuccessionRules.MythPressure(0.2f), 1e-4f); // 正統性低→無謀な賭けの圧力高
            Assert.AreEqual(0.0f, SuccessionRules.MythPressure(1.0f), 1e-4f); // 正統性十分→圧力なし
        }
    }
}
