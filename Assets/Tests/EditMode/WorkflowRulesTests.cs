using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議ワークフロー（WF基盤＋MEYASU-1 #1297）を固定する：箱は建白/注入を越階で受理、決裁待ちのみ承認/却下、
    /// 承認のみ執行（官僚忠実度で骨抜き＝実効適用量を返す）、起案→伝播→決裁→執行の通しが回る。
    /// </summary>
    public class WorkflowRulesTests
    {
        private static Petition NewPet(PetitionOrigin origin = PetitionOrigin.建白)
            => new Petition(1, "減税", Faction.同盟, BoxKind.政治家, origin, "tax.cut");

        [Test]
        public void Submit_AcceptsBoxOrigin_Escalating()
        {
            var pet = NewPet(PetitionOrigin.建白);
            Assert.IsTrue(WorkflowRules.CanSubmit(pet));
            Assert.IsTrue(WorkflowRules.Submit(pet));
            Assert.AreEqual(PetitionStatus.伝播中, pet.status);

            var inject = NewPet(PetitionOrigin.注入);
            Assert.IsTrue(WorkflowRules.CanSubmit(inject)); // 注入も箱発＝受理
        }

        [Test]
        public void Submit_RejectsConsultationOrigin_AndNonDraft()
        {
            // 諮問（上→箱）は越階受理の対象外
            var consult = NewPet(PetitionOrigin.諮問);
            Assert.IsFalse(WorkflowRules.CanSubmit(consult));

            // 起案以外は受理しない
            var moving = NewPet(); moving.status = PetitionStatus.伝播中;
            Assert.IsFalse(WorkflowRules.CanSubmit(moving));
            Assert.IsFalse(WorkflowRules.Submit(moving));
        }

        [Test]
        public void Decide_OnlyFromAwaiting()
        {
            var pet = NewPet(); pet.status = PetitionStatus.決裁待ち;
            Assert.IsTrue(WorkflowRules.Decide(pet, approve: true));
            Assert.AreEqual(PetitionStatus.承認, pet.status);

            var rej = NewPet(); rej.status = PetitionStatus.決裁待ち;
            Assert.IsTrue(WorkflowRules.Decide(rej, approve: false));
            Assert.AreEqual(PetitionStatus.却下, rej.status);

            // 伝播中は決裁できない
            var mid = NewPet(); mid.status = PetitionStatus.伝播中;
            Assert.IsFalse(WorkflowRules.Decide(mid, true));
            Assert.AreEqual(PetitionStatus.伝播中, mid.status);
        }

        [Test]
        public void Execute_OnlyFromApproved_ReturnsEffectiveMagnitude()
        {
            var pet = NewPet(); pet.status = PetitionStatus.承認;
            float applied = WorkflowRules.Execute(pet, fidelity: 0.4f); // 官僚が骨抜き
            Assert.AreEqual(0.4f, applied, 1e-4f);
            Assert.AreEqual(PetitionStatus.執行済, pet.status);

            // 承認前は執行できない（0・遷移なし）
            var pending = NewPet(); pending.status = PetitionStatus.決裁待ち;
            Assert.AreEqual(0f, WorkflowRules.Execute(pending, 1f), 1e-4f);
            Assert.AreEqual(PetitionStatus.決裁待ち, pending.status);
        }

        [Test]
        public void IsActive_And_IsResolved()
        {
            var pet = NewPet();
            Assert.IsTrue(WorkflowRules.IsActive(pet));                 // 起案
            pet.status = PetitionStatus.執行済;
            Assert.IsFalse(WorkflowRules.IsActive(pet));
            Assert.IsTrue(WorkflowRules.IsResolved(pet));
            pet.status = PetitionStatus.黙殺;
            Assert.IsFalse(WorkflowRules.IsActive(pet));               // 黙殺は非アクティブ
            Assert.IsFalse(WorkflowRules.IsResolved(pet));             // が終端でもない（再浮上しうる）
        }

        [Test]
        public void FullLifecycle_DraftToWateredDownExecution()
        {
            // 起案 → 越階受理 → 官僚を1階通過（信認0.8/摩擦0.25/正統性1）→ 決裁待ち → 承認 → 骨抜き執行
            var pet = NewPet(PetitionOrigin.建白);
            pet.carrierId = 42;

            Assert.IsTrue(WorkflowRules.Submit(pet));                  // 伝播中
            var step = PetitionFlowRules.Step(pet, heed: 0.8f, friction: 0.25f, legitimacy: 1f, roll: 0.3f);
            Assert.AreEqual(PetitionStep.通過, step);
            CollectionAssert.Contains(pet.hops, 42);                   // 無名の中継者を経た（チ。リレー）

            PetitionFlowRules.MarkAwaitingDecision(pet);
            Assert.AreEqual(PetitionStatus.決裁待ち, pet.status);

            Assert.IsTrue(WorkflowRules.Decide(pet, approve: true));   // 承認
            float friction = 0.6f;                                    // 財務官僚の省益（歳入減を嫌う）
            float applied = WorkflowRules.Execute(pet, PetitionFlowRules.ExecutionFidelity(friction));
            Assert.AreEqual(0.4f, applied, 1e-4f);                     // 通ったのに4割しか効かない
            Assert.AreEqual(PetitionStatus.執行済, pet.status);
        }

        [Test]
        public void NullSafe()
        {
            Assert.IsFalse(WorkflowRules.CanSubmit(null));
            Assert.IsFalse(WorkflowRules.Submit(null));
            Assert.IsFalse(WorkflowRules.Decide(null, true));
            Assert.AreEqual(0f, WorkflowRules.Execute(null, 1f), 1e-4f);
            Assert.IsFalse(WorkflowRules.IsActive(null));
            Assert.IsFalse(WorkflowRules.IsResolved(null));
        }
    }
}
