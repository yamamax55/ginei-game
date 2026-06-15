using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 稟議のサンプルデータと決裁フローの通しを固定する（Unity Test Runner / TestHarness で回せる「決裁フローのテスト」）：
    /// 全サンプルの効果キーが登録済みで、建白→伝播→決裁→執行で世界が実際に動くことを保証する。
    /// </summary>
    public class RingiSampleDataTests
    {
        private static CampaignState OneFaction(Faction f, out FactionState fs)
        {
            var cs = new CampaignState();
            fs = new FactionState(f); // taxRate=0.3 / 信認 既定（箱0.5・傾聴1.0）/ community・regime 初期化済
            cs.states = new List<FactionState> { fs };
            return cs;
        }

        [Test]
        public void Catalog_IsNonEmpty_AndIndexable()
        {
            Assert.Greater(RingiSampleData.Count, 0);
            Assert.AreEqual(RingiSampleData.All[0].title, RingiSampleData.At(0).title);
            Assert.AreEqual(RingiSampleData.At(0).title, RingiSampleData.At(-5).title);   // 範囲外は先頭へ
            Assert.AreEqual(RingiSampleData.At(0).title, RingiSampleData.At(999).title);
        }

        [Test]
        public void EverySample_EffectKey_IsRegistered()
        {
            foreach (var s in RingiSampleData.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(s.effectKey), $"{s.title} に effectKey が無い");
                Assert.IsTrue(PetitionEffects.Has(s.effectKey), $"未登録の効果キー: {s.effectKey}（{s.title}）");
            }
        }

        [Test]
        public void EverySample_RunsDecisionFlow_ToExecution()
        {
            foreach (var s in RingiSampleData.All)
            {
                var cs = OneFaction(Faction.同盟, out var fs);
                var pet = new Petition(0, s.title, fs.faction, s.box, PetitionOrigin.建白, s.effectKey);

                Assert.IsTrue(WorkflowRules.Submit(pet), $"{s.title}: 受理できない");

                float heed = CredibilityRules.Heed(fs.credibility, s.box);
                float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
                // 摩擦0・roll0 で確実に通過させて決裁まで運ぶ（フロー自体の検証）
                Assert.AreEqual(PetitionStep.通過,
                    RingiPipeline.Propagate(pet, heed, friction: 0f, legitimacy, roll: 0f), $"{s.title}: 伝播で止まった");

                RingiPipeline.SendToDecision(pet);
                Assert.IsTrue(RingiPipeline.Decide(pet, approve: true), $"{s.title}: 決裁できない");

                float applied = RingiPipeline.ExecuteAndApply(pet, cs, executionFriction: 0f);
                Assert.AreEqual(1f, applied, 1e-4f, $"{s.title}: 満額執行のはず");
                Assert.AreEqual(PetitionStatus.執行済, pet.status, $"{s.title}: 執行済にならない");
            }
        }

        [Test]
        public void TaxCut_MovesWorld_EndToEnd()
        {
            var cs = OneFaction(Faction.同盟, out var fs); // taxRate 0.3
            var pet = new Petition(0, "減税", fs.faction, BoxKind.政治家, PetitionOrigin.建白, "tax.cut");
            WorkflowRules.Submit(pet);
            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家);
            RingiPipeline.Propagate(pet, heed, 0f, FactionLoyaltyRules.BaselineLoyalty(fs), 0f);
            RingiPipeline.SendToDecision(pet);
            RingiPipeline.Decide(pet, true);
            RingiPipeline.ExecuteAndApply(pet, cs, 0f); // 満額
            Assert.AreEqual(0.2f, fs.taxRate, 1e-4f);   // 0.3 - 0.1 ＝世界が動いた
        }
    }
}
