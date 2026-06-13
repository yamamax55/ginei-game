using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 建白の伝播と官僚＝執行の壁（MEYASU-3 #1299）を固定する：生存ロール＝信認×(1-摩擦)×正統性、
    /// 通過で中継者を記録・高摩擦で歪み、失敗は強い抵抗で握り潰し/弱ければ黙殺、黙殺は正しさ判明で再浮上、
    /// 官僚の執行忠実度は摩擦で骨抜き（通ったのに効かない）。
    /// </summary>
    public class PetitionFlowRulesTests
    {
        private static readonly PetitionFlowParams P = PetitionFlowParams.Default; // bury0.5 / distort0.4 / resurface0.3 / fidelity1

        private static Petition NewPet(int carrier = 0)
        {
            var pet = new Petition(1, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            pet.carrierId = carrier;
            return pet;
        }

        [Test]
        public void SurvivalChance_FactorsHeedFrictionLegitimacy()
        {
            Assert.AreEqual(0.6f, PetitionFlowRules.SurvivalChance(0.8f, 0.25f, 1f), 1e-4f); // 0.8*0.75*1.0
            Assert.AreEqual(0.3f, PetitionFlowRules.SurvivalChance(0.8f, 0.25f, 0f), 1e-4f); // 0.8*0.75*0.5（低正統性で半減）
            // 高摩擦は通過率を潰す
            Assert.Less(PetitionFlowRules.SurvivalChance(0.8f, 0.9f, 1f), 0.15f);
        }

        [Test]
        public void Step_Pass_AdvancesAndRecordsCarrierHop()
        {
            var pet = NewPet(carrier: 7);
            // survival = 0.8*0.75*1 = 0.6、roll 0.3 < 0.6 → 通過
            var step = PetitionFlowRules.Step(pet, heed: 0.8f, friction: 0.25f, legitimacy: 1f, roll: 0.3f, P);
            Assert.AreEqual(PetitionStep.通過, step);
            Assert.AreEqual(PetitionStatus.伝播中, pet.status);
            CollectionAssert.Contains(pet.hops, 7);     // 中継者の足跡（チ。リレー）
            Assert.IsFalse(pet.distorted);              // 摩擦0.25 < 歪み閾0.4
        }

        [Test]
        public void Step_Pass_HighFriction_Distorts()
        {
            var pet = NewPet();
            // survival = 0.9*0.5*1 = 0.45、roll 0.1 < 0.45 → 通過、摩擦0.5 ≥ 0.4 → 歪む
            var step = PetitionFlowRules.Step(pet, heed: 0.9f, friction: 0.5f, legitimacy: 1f, roll: 0.1f, P);
            Assert.AreEqual(PetitionStep.通過, step);
            Assert.IsTrue(pet.distorted);
        }

        [Test]
        public void Step_Fail_HighFriction_IsCrushed()
        {
            var pet = NewPet();
            // survival = 0.3*0.3*1 = 0.09、roll 0.5 ≥ → 失敗、摩擦0.7 ≥ 0.5 → 握り潰し（却下）
            var step = PetitionFlowRules.Step(pet, heed: 0.3f, friction: 0.7f, legitimacy: 1f, roll: 0.5f, P);
            Assert.AreEqual(PetitionStep.握り潰し, step);
            Assert.AreEqual(PetitionStatus.却下, pet.status);
        }

        [Test]
        public void Step_Fail_LowFriction_IsIgnored()
        {
            var pet = NewPet();
            // survival = 0.5*0.8*1 = 0.4、roll 0.9 ≥ → 失敗、摩擦0.2 < 0.5 → 黙殺
            var step = PetitionFlowRules.Step(pet, heed: 0.5f, friction: 0.2f, legitimacy: 1f, roll: 0.9f, P);
            Assert.AreEqual(PetitionStep.黙殺, step);
            Assert.AreEqual(PetitionStatus.黙殺, pet.status);
        }

        [Test]
        public void Resurface_OnlyWhenSilenced_AndVindicated()
        {
            var pet = NewPet();
            pet.status = PetitionStatus.黙殺;
            pet.vindicated = false;
            Assert.IsFalse(PetitionFlowRules.Resurface(pet, 0.1f, P)); // 正しさ未判明＝再浮上しない

            pet.vindicated = true;
            Assert.IsFalse(PetitionFlowRules.Resurface(pet, 0.5f, P)); // roll 0.5 ≥ 0.3
            Assert.IsTrue(PetitionFlowRules.Resurface(pet, 0.1f, P));  // roll 0.1 < 0.3 → 再浮上
            Assert.AreEqual(PetitionStatus.再浮上, pet.status);
        }

        [Test]
        public void Resurface_NotSilenced_IsFalse()
        {
            var pet = NewPet();
            pet.status = PetitionStatus.承認;
            pet.vindicated = true;
            Assert.IsFalse(PetitionFlowRules.Resurface(pet, 0.0f, P));
        }

        [Test]
        public void MarkAwaitingDecision_FromPropagatingOrResurfaced()
        {
            var a = NewPet(); a.status = PetitionStatus.伝播中;
            PetitionFlowRules.MarkAwaitingDecision(a);
            Assert.AreEqual(PetitionStatus.決裁待ち, a.status);

            var b = NewPet(); b.status = PetitionStatus.却下; // 死んだ案件は決裁待ちに戻さない
            PetitionFlowRules.MarkAwaitingDecision(b);
            Assert.AreEqual(PetitionStatus.却下, b.status);
        }

        [Test]
        public void ExecutionFidelity_WateredDownByFriction()
        {
            Assert.AreEqual(1f, PetitionFlowRules.ExecutionFidelity(0f, P), 1e-4f);   // 摩擦なし＝忠実
            Assert.AreEqual(0.4f, PetitionFlowRules.ExecutionFidelity(0.6f, P), 1e-4f); // 省益で骨抜き＝通ったのに効かない
        }

        [Test]
        public void NullSafe()
        {
            Assert.AreEqual(PetitionStep.黙殺, PetitionFlowRules.Step(null, 1f, 0f, 1f, 0f, P));
            Assert.IsFalse(PetitionFlowRules.Resurface(null, 0f, P));
            PetitionFlowRules.MarkAwaitingDecision(null); // 例外を投げない
        }
    }
}
