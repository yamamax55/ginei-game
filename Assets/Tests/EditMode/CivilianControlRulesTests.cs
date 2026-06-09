using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍政関係＝文民統制（GOV-4 #145）を固定する：政体型ごとの兼任可否・軍人事の所在・役職既定資格、
    /// および統制が弱く支持が低く敗戦するとクーデターが発火すること（強い統制は抑え込む）。
    /// </summary>
    public class CivilianControlRulesTests
    {
        [Test]
        public void MilitaryMayHoldPoliticalOffice_OnlyWarlordAndUndivided()
        {
            Assert.IsTrue(CivilianControlRules.MilitaryMayHoldPoliticalOffice(CivilianControlType.軍部優位));
            Assert.IsTrue(CivilianControlRules.MilitaryMayHoldPoliticalOffice(CivilianControlType.未分化));
            Assert.IsFalse(CivilianControlRules.MilitaryMayHoldPoliticalOffice(CivilianControlType.文民統制));
            Assert.IsFalse(CivilianControlRules.MilitaryMayHoldPoliticalOffice(CivilianControlType.君主統帥));
            Assert.IsFalse(CivilianControlRules.MilitaryMayHoldPoliticalOffice(CivilianControlType.党軍));
        }

        [Test]
        public void CiviliansAppointMilitary_FalseUnderWarlordAndUndivided()
        {
            Assert.IsTrue(CivilianControlRules.CiviliansAppointMilitary(CivilianControlType.文民統制));
            Assert.IsTrue(CivilianControlRules.CiviliansAppointMilitary(CivilianControlType.党軍));
            Assert.IsFalse(CivilianControlRules.CiviliansAppointMilitary(CivilianControlType.軍部優位));
            Assert.IsFalse(CivilianControlRules.CiviliansAppointMilitary(CivilianControlType.未分化));
        }

        [Test]
        public void DefaultMilitaryOnly_TrueForMilitaryDomain_ExceptUndivided()
        {
            Assert.IsTrue(CivilianControlRules.DefaultMilitaryOnly(OfficeDomain.軍事, CivilianControlType.文民統制));
            Assert.IsFalse(CivilianControlRules.DefaultMilitaryOnly(OfficeDomain.内政, CivilianControlType.文民統制));
            Assert.IsFalse(CivilianControlRules.DefaultMilitaryOnly(OfficeDomain.軍事, CivilianControlType.未分化));
        }

        [Test]
        public void DefaultCivilianOnly_TrueForPoliticalDomains_UnderCivilianControl()
        {
            Assert.IsTrue(CivilianControlRules.DefaultCivilianOnly(OfficeDomain.内政, CivilianControlType.文民統制));
            Assert.IsTrue(CivilianControlRules.DefaultCivilianOnly(OfficeDomain.財政, CivilianControlType.文民統制));
            Assert.IsFalse(CivilianControlRules.DefaultCivilianOnly(OfficeDomain.軍事, CivilianControlType.文民統制));
            // 軍部優位では軍人が政治職も占めるので文民専用にしない
            Assert.IsFalse(CivilianControlRules.DefaultCivilianOnly(OfficeDomain.内政, CivilianControlType.軍部優位));
        }

        [Test]
        public void CoupRisk_HighWhenWeakControl_LowSupport_Defeat()
        {
            // 軍部優位・統制弱・支持低・敗戦 → 高リスク
            float risk = CivilianControlRules.CoupRisk(CivilianControlType.軍部優位, 0.2f, 0.3f, true);
            Assert.Greater(risk, 0.6f);
            Assert.IsTrue(CivilianControlRules.WouldCoup(CivilianControlType.軍部優位, 0.2f, 0.3f, true));
        }

        [Test]
        public void CoupRisk_LowUnderStrongCivilianControl()
        {
            // 文民統制・統制強・高支持・平時 → 低リスク
            float risk = CivilianControlRules.CoupRisk(CivilianControlType.文民統制, 0.9f, 0.8f, false);
            Assert.Less(risk, 0.2f);
            Assert.IsFalse(CivilianControlRules.WouldCoup(CivilianControlType.文民統制, 0.9f, 0.8f, false));
        }

        [Test]
        public void StrongControl_SuppressesCoupRisk_Monotonic()
        {
            // 同条件でも統制が強いほどリスクは下がる
            float weak = CivilianControlRules.CoupRisk(CivilianControlType.君主統帥, 0.2f, 0.5f, false);
            float strong = CivilianControlRules.CoupRisk(CivilianControlType.君主統帥, 0.9f, 0.5f, false);
            Assert.Greater(weak, strong);
        }
    }
}
