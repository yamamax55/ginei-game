using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議の通しを薄いオーケストレータ（既存窓口の合成）で駆動する固定スペック：
    /// Submit が在庫へ載せ、ExecuteAndApply が「執行×官僚忠実度×効果適用」を一括で世界へ反映する。
    /// 数式は <see cref="WorkflowRules"/>/<see cref="PetitionFlowRules"/>/<see cref="PetitionEffects"/> と一致（二重実装しない）。
    /// </summary>
    public class RingiPipelineTests
    {
        private static CampaignState OneFaction(Faction f, out FactionState fs)
        {
            var cs = new CampaignState();
            fs = new FactionState(f); // taxRate=0.3 / 信認 既定（箱0.5・傾聴1.0）
            cs.states = new List<FactionState> { fs };
            return cs;
        }

        [Test]
        public void Submit_LoadsLedger_AndAdvancesStatus()
        {
            var led = new PetitionLedger();
            var pet = new Petition(0, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            Assert.IsTrue(RingiPipeline.Submit(led, pet));
            Assert.AreEqual(PetitionStatus.伝播中, pet.status);
            Assert.AreEqual(1, led.Count);
            Assert.AreSame(pet, led.Get(pet.id));
        }

        [Test]
        public void Submit_Rejects_Shimon_NotLoaded()
        {
            var led = new PetitionLedger();
            // 諮問（上→箱）は越階受理の対象外＝Submit 不可＝在庫に載らない
            var pet = new Petition(0, "諮問", Faction.同盟, BoxKind.政治家, PetitionOrigin.諮問, "tax.cut");
            Assert.IsFalse(RingiPipeline.Submit(led, pet));
            Assert.AreEqual(0, led.Count);
        }

        [Test]
        public void FullLoop_ViaPipeline_MovesWorld_WateredDownByBureaucrats()
        {
            var cs = OneFaction(Faction.同盟, out var fs);
            var led = new PetitionLedger();
            var pet = new Petition(0, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            pet.carrierId = 42;

            Assert.IsTrue(RingiPipeline.Submit(led, pet));

            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家); // 0.5
            Assert.AreEqual(PetitionStep.通過, RingiPipeline.Propagate(pet, heed, 0.25f, 1f, roll: 0.1f));
            CollectionAssert.Contains(pet.hops, 42);

            RingiPipeline.SendToDecision(pet);
            Assert.AreEqual(PetitionStatus.決裁待ち, pet.status);
            Assert.AreEqual(1, led.AwaitingDecision(BoxKind.政治家).Count); // 在庫の決裁トレイに乗る

            Assert.IsTrue(RingiPipeline.Decide(pet, approve: true));

            // 財務官僚の省益(0.6)で骨抜き → 実効4割だけ世界に効く（0.3 - 0.1*0.4 = 0.26）
            float applied = RingiPipeline.ExecuteAndApply(pet, cs, executionFriction: 0.6f);
            Assert.AreEqual(0.4f, applied, 1e-4f);
            Assert.AreEqual(0.26f, fs.taxRate, 1e-4f);
            Assert.AreEqual(PetitionStatus.執行済, pet.status);
        }

        [Test]
        public void ExecuteAndApply_NotApproved_IsNoop()
        {
            var cs = OneFaction(Faction.同盟, out var fs);
            var pet = new Petition(1, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            pet.status = PetitionStatus.黙殺; // 承認に至っていない
            Assert.AreEqual(0f, RingiPipeline.ExecuteAndApply(pet, cs, 0f), 1e-4f);
            Assert.AreEqual(0.3f, fs.taxRate, 1e-4f); // 世界は動かない
            Assert.AreEqual(PetitionStatus.黙殺, pet.status);
        }

        [Test]
        public void Propagate_LowHeed_IsIgnored()
        {
            var cs = OneFaction(Faction.同盟, out var fs);
            CredibilityRules.Adjust(fs.credibility, BoxKind.政治家, -0.45f); // 0.05＝壁紙化
            var pet = new Petition(2, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            WorkflowRules.Submit(pet);
            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家); // 0.05
            Assert.AreEqual(PetitionStep.黙殺, RingiPipeline.Propagate(pet, heed, 0.25f, 1f, roll: 0.5f));
        }
    }
}
