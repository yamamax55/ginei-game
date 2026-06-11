using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 目安箱エンジン→世界（FactionState/CampaignState）の通しを固定する（世界接続）：
    /// 減税の建白が 信認×省益 を経て官僚に骨抜きされ、効いたぶんだけ税率が動く。信認が低ければ世界は動かない。
    /// ＝MEYASU-1/2/3 ＋ 効果レジストリ の統合スペック。
    /// </summary>
    public class PetitionLoopTests
    {
        private static CampaignState OneFaction(Faction f, out FactionState fs)
        {
            var cs = new CampaignState();
            fs = new FactionState(f); // taxRate=0.3 / credibility 既定（箱0.5・傾聴1.0）
            cs.states = new List<FactionState> { fs };
            return cs;
        }

        // ---- 効果レジストリ単体 ----

        [Test]
        public void Effect_TaxCut_ScalesByMagnitude()
        {
            var fs = new FactionState(Faction.同盟); // taxRate 0.3
            Assert.IsTrue(PetitionEffects.Apply("tax.cut", fs, 1f));
            Assert.AreEqual(0.2f, fs.taxRate, 1e-4f);  // 満額 -0.1

            var fs2 = new FactionState(Faction.同盟);
            Assert.IsTrue(PetitionEffects.Apply("tax.cut", fs2, 0.5f));
            Assert.AreEqual(0.25f, fs2.taxRate, 1e-4f); // 半額 -0.05
        }

        [Test]
        public void Effect_UnknownKey_IsNoop()
        {
            var fs = new FactionState(Faction.同盟);
            Assert.IsFalse(PetitionEffects.Apply("does.not.exist", fs, 1f));
            Assert.AreEqual(0.3f, fs.taxRate, 1e-4f);
            Assert.IsFalse(PetitionEffects.Has("does.not.exist"));
            Assert.IsTrue(PetitionEffects.Has("tax.cut"));
        }

        [Test]
        public void Effect_ViaCampaign_FindsFaction()
        {
            var cs = OneFaction(Faction.帝国, out var fs);
            Assert.IsTrue(PetitionEffects.Apply("tax.hike", cs, Faction.帝国, 1f));
            Assert.AreEqual(0.4f, fs.taxRate, 1e-4f); // +0.1
            // 居ない勢力は false
            Assert.IsFalse(PetitionEffects.Apply("tax.hike", cs, Faction.同盟, 1f));
        }

        // ---- 通しの統合スペック ----

        [Test]
        public void FullLoop_TaxCut_MovesWorld_ButWateredDownByBureaucrats()
        {
            var cs = OneFaction(Faction.同盟, out var fs);
            var pet = new Petition(1, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            pet.carrierId = 42;

            // 越階で受理 → 官僚を1階通過（信認＝箱0.5×傾聴1.0／省益摩擦0.25／正統性1）
            Assert.IsTrue(WorkflowRules.Submit(pet));
            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家); // 0.5
            var step = PetitionFlowRules.Step(pet, heed, friction: 0.25f, legitimacy: 1f, roll: 0.1f);
            Assert.AreEqual(PetitionStep.通過, step);
            CollectionAssert.Contains(pet.hops, 42); // 無名の中継者を経た

            // 決裁待ち → 政治家が承認
            PetitionFlowRules.MarkAwaitingDecision(pet);
            Assert.IsTrue(WorkflowRules.Decide(pet, approve: true));

            // 執行：財務官僚の省益(0.6)で骨抜き → 実効4割だけ世界に効く
            float applied = WorkflowRules.Execute(pet, PetitionFlowRules.ExecutionFidelity(0.6f));
            Assert.AreEqual(0.4f, applied, 1e-4f);
            Assert.IsTrue(PetitionEffects.Apply(pet.effectKey, cs, pet.faction, applied));

            Assert.AreEqual(0.26f, fs.taxRate, 1e-4f);            // 0.3 - 0.1*0.4（通ったのに満額効かない）
            Assert.AreEqual(PetitionStatus.執行済, pet.status);
        }

        [Test]
        public void FullLoop_LowCredibility_IsIgnored_WorldUnchanged()
        {
            var cs = OneFaction(Faction.同盟, out var fs);
            // 政治家箱への信認を枯らす（壁紙化）
            CredibilityRules.Adjust(fs.credibility, BoxKind.政治家, -0.45f); // 0.05
            Assert.IsTrue(CredibilityRules.IsWallpapered(fs.credibility, BoxKind.政治家));

            var pet = new Petition(2, "減税", Faction.同盟, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            Assert.IsTrue(WorkflowRules.Submit(pet));

            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家); // 0.05
            var step = PetitionFlowRules.Step(pet, heed, friction: 0.25f, legitimacy: 1f, roll: 0.5f);
            Assert.AreEqual(PetitionStep.黙殺, step); // 信認が低い＝届かず黙殺

            // 黙殺は決裁・執行に進めない＝世界は動かない
            Assert.IsFalse(WorkflowRules.Decide(pet, true));
            Assert.AreEqual(0f, WorkflowRules.Execute(pet, 1f), 1e-4f);
            Assert.AreEqual(0.3f, fs.taxRate, 1e-4f); // 据え置き
        }
    }
}
