using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 捕虜化・解放・処断（LIFE-4 #154）を固定する：包囲で捕縛確率が上がること、捕縛→拘留、解放/処断/登用の遷移
    /// （処断は死亡#152へ合流）、政体ごとの AI 既定処遇、処断の支持代償。
    /// </summary>
    public class CaptivityRulesTests
    {
        private static Person Mk() => new Person(1, "提督", Faction.同盟, PersonRole.軍人);

        [Test]
        public void CaptureChance_HigherWhenEncircled()
        {
            float free = CaptivityRules.CaptureChance(encircled: false, commandFactor: 0.5f, moraleFactor: 0.5f);
            float trapped = CaptivityRules.CaptureChance(encircled: true, commandFactor: 0.5f, moraleFactor: 0.5f);
            Assert.Greater(trapped, free);
        }

        [Test]
        public void Capture_PutsPersonInCustody_SeatVacated()
        {
            var p = Mk();
            Assert.IsTrue(p.IsAvailable);
            Assert.IsTrue(CaptivityRules.Capture(p, Faction.帝国, 800));
            Assert.AreEqual(CaptiveStatus.捕虜, p.captiveStatus);
            Assert.AreEqual(Faction.帝国, p.heldBy);
            Assert.IsFalse(p.IsAvailable); // 席は空く
            Assert.IsFalse(p.IsDeceased);  // が本人は生存
        }

        [Test]
        public void Release_ReturnsToFreedom()
        {
            var p = Mk();
            CaptivityRules.Capture(p, Faction.帝国, 800);
            Assert.IsTrue(CaptivityRules.Release(p));
            Assert.AreEqual(CaptiveStatus.自由, p.captiveStatus);
            Assert.IsTrue(p.IsAvailable); // 元勢力へ復帰＝再任用可
        }

        [Test]
        public void Execute_MergesIntoDeath()
        {
            var p = Mk();
            CaptivityRules.Capture(p, Faction.帝国, 800);
            Assert.IsTrue(CaptivityRules.Execute(p, 801));
            Assert.AreEqual(CaptiveStatus.処断済, p.captiveStatus);
            Assert.IsTrue(p.IsDeceased);          // 死亡#152 へ合流
            Assert.AreEqual(801, p.deathYear);
        }

        [Test]
        public void Recruit_DefectsToCaptorFaction()
        {
            var p = Mk(); // 同盟
            CaptivityRules.Capture(p, Faction.帝国, 800);
            Assert.IsTrue(CaptivityRules.Recruit(p, Faction.帝国));
            Assert.AreEqual(Faction.帝国, p.faction); // 寝返り
            Assert.IsTrue(p.IsAvailable);
        }

        [Test]
        public void Disposition_OnlyValidFromCaptiveState()
        {
            var p = Mk(); // 自由のまま
            Assert.IsFalse(CaptivityRules.Release(p));
            Assert.IsFalse(CaptivityRules.Execute(p, 801));
            Assert.IsFalse(CaptivityRules.Recruit(p, Faction.帝国));
        }

        [Test]
        public void DefaultDisposition_VariesByRegime()
        {
            Assert.AreEqual(CaptiveDisposition.解放, CaptivityRules.DefaultDisposition(CivilianControlType.君主統帥));
            Assert.AreEqual(CaptiveDisposition.処断, CaptivityRules.DefaultDisposition(CivilianControlType.党軍));
            Assert.AreEqual(CaptiveDisposition.処断, CaptivityRules.DefaultDisposition(CivilianControlType.未分化));
        }

        [Test]
        public void ExecutionPenalty_ScalesWithFame()
        {
            Assert.Greater(CaptivityRules.ExecutionSupportPenalty(1f), CaptivityRules.ExecutionSupportPenalty(0.2f));
            Assert.AreEqual(0.3f, CaptivityRules.ExecutionSupportPenalty(1f), 1e-4f);
        }

        [Test]
        public void RecruitChance_RareAndCloserIdeologyHelps()
        {
            float close = CaptivityRules.RecruitChance(ideologyDistance: 0.1f, treatment: 1f);
            float far = CaptivityRules.RecruitChance(ideologyDistance: 0.9f, treatment: 1f);
            Assert.Greater(close, far);
            Assert.LessOrEqual(close, 0.4f); // 離反は稀
        }
    }
}
