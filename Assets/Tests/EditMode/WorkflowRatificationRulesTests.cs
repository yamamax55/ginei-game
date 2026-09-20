using NUnit.Framework;

namespace Ginei.Tests
{
    public class WorkflowRatificationRulesTests
    {
        private static Petition MajorInquiry(Faction faction = Faction.同盟)
            => new Petition(1, "重大案件", faction, BoxKind.政治家, PetitionOrigin.諮問, "tax.cut")
            {
                severity = DecisionSeverity.重大,
                status = PetitionStatus.決裁待ち,
            };

        [Test]
        public void Defer_Requires_Major_And_Political_Deadlock()
        {
            var state = new FactionState(Faction.帝国) { governmentForm = GovernmentForm.君主制 };
            Petition pet = MajorInquiry(Faction.帝国);

            Assert.IsFalse(WorkflowRules.ShouldDeferToBox(pet, state, 0.8f));
            Assert.IsTrue(WorkflowRules.ShouldDeferToBox(pet, state, 0.4f));
            pet.severity = DecisionSeverity.重要;
            Assert.IsFalse(WorkflowRules.ShouldDeferToBox(pet, state, 0f));
        }

        [Test]
        public void Democratic_Consultation_Is_Rarer_Than_Autocratic_Consultation()
        {
            Petition pet = MajorInquiry();
            var democracy = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制 };
            democracy.regime.legitimacy = 0.2f;

            Assert.IsFalse(WorkflowRules.ShouldDeferToBox(pet, democracy, 0.3f), "合議が残る段階で箱へ丸投げした");
            Assert.IsTrue(WorkflowRules.ShouldDeferToBox(pet, democracy, 0.2f));
        }

        [Test]
        public void Approval_Moves_To_Execution_Without_Penalty()
        {
            var state = new FactionState(Faction.同盟) { governmentForm = GovernmentForm.共和制 };
            Petition pet = MajorInquiry();
            float credibility = CredibilityRules.Of(state.credibility, pet.box);
            float legitimacy = state.regime.legitimacy;

            RatificationResult result = WorkflowRules.ApplyRatification(pet, true, state);

            Assert.IsTrue(result.applied);
            Assert.IsTrue(result.approved);
            Assert.AreEqual(PetitionStatus.承認, pet.status);
            Assert.AreEqual(credibility, CredibilityRules.Of(state.credibility, pet.box), 1e-5f);
            Assert.AreEqual(legitimacy, state.regime.legitimacy, 1e-5f);
        }

        [Test]
        public void Constitutional_Rejection_Costs_Credibility_And_Legitimacy_Once()
        {
            var state = new FactionState(Faction.帝国) { governmentForm = GovernmentForm.立憲君主制 };
            Petition pet = MajorInquiry(Faction.帝国);
            float credibility = CredibilityRules.Of(state.credibility, pet.box);
            float legitimacy = state.regime.legitimacy;

            RatificationResult result = WorkflowRules.ApplyRatification(pet, false, state);

            Assert.IsTrue(result.applied);
            Assert.IsFalse(result.approved);
            Assert.IsTrue(result.constitutionalCrisis);
            Assert.AreEqual(PetitionStatus.却下, pet.status);
            Assert.Less(CredibilityRules.Of(state.credibility, pet.box), credibility);
            Assert.Less(state.regime.legitimacy, legitimacy);
            Assert.IsFalse(WorkflowRules.ApplyRatification(pet, false, state).applied, "却下の代償が二重適用できる");
        }

        [Test]
        public void Consultation_Severity_Survives_SaveLoad_And_Old_Save_Defaults_To_Normal()
        {
            var ledger = new PetitionLedger();
            ledger.Add(MajorInquiry());
            var saved = new System.Collections.Generic.List<PetitionSave>();
            CampaignSerializer.WritePetitions(saved, ledger);
            var restored = new PetitionLedger();
            CampaignSerializer.ReadPetitions(saved, restored);
            Assert.AreEqual(DecisionSeverity.重大, restored.items[0].severity);

            saved[0].severity = 0; // フィールドが無かった旧セーブ
            CampaignSerializer.ReadPetitions(saved, restored);
            Assert.AreEqual(DecisionSeverity.通常, restored.items[0].severity);
        }
    }
}
