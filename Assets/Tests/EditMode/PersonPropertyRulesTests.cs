using System.Collections.Generic;
using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 人物プロパティ群（PER-PROPS A/B/C）の純ロジック検証。
    /// CreedRules / SocialOriginRules / PersonGrievanceRules / VitalityRules / StandingRules /
    /// HiddenTraitRules / RelationshipSeedRules を境界・既定値固定・決定論で網羅する。
    /// </summary>
    public class PersonPropertyRulesTests
    {
        const float Eps = 1e-4f;

        // ===== A2 CreedRules =====

        [Test]
        public void Creed_SameCreed_NoTension()
        {
            Assert.AreEqual(0f, CreedRules.Tension(Creed.共和主義, Creed.共和主義), Eps);
        }

        [Test]
        public void Creed_Opposed_MaxTension()
        {
            Assert.AreEqual(1f, CreedRules.Tension(Creed.共和主義, Creed.帝政擁護), Eps);
            Assert.AreEqual(1f, CreedRules.Tension(Creed.門閥主義, Creed.能力主義), Eps);
            Assert.IsTrue(CreedRules.IsOpposed(Creed.能力主義, Creed.門閥主義));
            Assert.IsFalse(CreedRules.IsOpposed(Creed.共和主義, Creed.門閥主義));
        }

        [Test]
        public void Creed_ApathyAndModerate_LowTension()
        {
            Assert.AreEqual(0.1f, CreedRules.Tension(Creed.無関心, Creed.帝政擁護), Eps);
            Assert.AreEqual(0.2f, CreedRules.Tension(Creed.中道, Creed.帝政擁護), Eps);
        }

        [Test]
        public void Creed_Divergent_MidTension()
        {
            // 非対立・非無関心・非中道（共和 vs 門閥）＝相違 0.4
            Assert.AreEqual(0.4f, CreedRules.Tension(Creed.共和主義, Creed.門閥主義), Eps);
        }

        [Test]
        public void Creed_Parse_KnownAndUnknown()
        {
            Assert.AreEqual(Creed.共和主義, CreedRules.Parse("共和主義・民主"));
            Assert.AreEqual(Creed.帝政擁護, CreedRules.Parse("帝政"));
            Assert.AreEqual(Creed.門閥主義, CreedRules.Parse("門閥貴族制"));
            Assert.AreEqual(Creed.能力主義, CreedRules.Parse("実力本位"));
            Assert.AreEqual(Creed.無関心, CreedRules.Parse(""));
            Assert.AreEqual(Creed.無関心, CreedRules.Parse("謎"));
        }

        // ===== A4 SocialOriginRules =====

        [Test]
        public void SocialOrigin_Pedigree_FavoredUnderAristocracy()
        {
            Assert.AreEqual(0.2f, SocialOriginRules.RecruitmentFavor(SocialOrigin.門閥貴族, Creed.門閥主義), Eps);
            Assert.AreEqual(-0.3f, SocialOriginRules.RecruitmentFavor(SocialOrigin.平民, Creed.門閥主義), Eps);
        }

        [Test]
        public void SocialOrigin_Commoner_FavoredUnderMeritocracy()
        {
            Assert.AreEqual(0.25f, SocialOriginRules.RecruitmentFavor(SocialOrigin.植民星, Creed.能力主義), Eps);
            Assert.AreEqual(-0.15f, SocialOriginRules.RecruitmentFavor(SocialOrigin.門閥貴族, Creed.能力主義), Eps);
        }

        [Test]
        public void SocialOrigin_LoyaltyFriction_OnlyMismatch()
        {
            Assert.AreEqual(0.3f, SocialOriginRules.LoyaltyFriction(SocialOrigin.平民, Creed.門閥主義), Eps);
            Assert.AreEqual(0.3f, SocialOriginRules.LoyaltyFriction(SocialOrigin.門閥貴族, Creed.能力主義), Eps);
            Assert.AreEqual(0f, SocialOriginRules.LoyaltyFriction(SocialOrigin.門閥貴族, Creed.門閥主義), Eps);
            Assert.AreEqual(0f, SocialOriginRules.LoyaltyFriction(SocialOrigin.平民, Creed.無関心), Eps);
        }

        [Test]
        public void SocialOrigin_Classifiers()
        {
            Assert.IsTrue(SocialOriginRules.IsAristocratic(SocialOrigin.下級貴族));
            Assert.IsTrue(SocialOriginRules.IsCommoner(SocialOrigin.植民星));
            Assert.IsFalse(SocialOriginRules.IsCommoner(SocialOrigin.軍人家系));
        }

        // ===== A3 PersonGrievanceRules =====

        [Test]
        public void Grievance_AccumulateAndDecay_Clamped()
        {
            Assert.AreEqual(30, PersonGrievanceRules.Accumulate(0, 2));   // 2×15
            Assert.AreEqual(100, PersonGrievanceRules.Accumulate(95, 2)); // クランプ上限
            Assert.AreEqual(0, PersonGrievanceRules.Accumulate(0, -5));   // 負入力は無視
            Assert.AreEqual(60, PersonGrievanceRules.Decay(100, 5));      // 100-5×8
            Assert.AreEqual(0, PersonGrievanceRules.Decay(10, 5));        // クランプ下限
        }

        [Test]
        public void Grievance_DefectionPropensity_RisesWithAmbition()
        {
            float low = PersonGrievanceRules.DefectionPropensity(50, 0);
            float high = PersonGrievanceRules.DefectionPropensity(50, 100);
            Assert.AreEqual(0.5f, low, Eps);               // g=0.5, a=0 → 0.5
            Assert.AreEqual(0.75f, high, Eps);             // g=0.5×(1+0.5) = 0.75
            Assert.IsTrue(high > low);
            Assert.AreEqual(1f, PersonGrievanceRules.DefectionPropensity(100, 100), Eps); // クランプ
        }

        [Test]
        public void Grievance_Threshold()
        {
            Assert.IsFalse(PersonGrievanceRules.ShouldConsiderDefection(59));
            Assert.IsTrue(PersonGrievanceRules.ShouldConsiderDefection(60));
        }

        // ===== B7 VitalityRules =====

        [Test]
        public void Vitality_DeathRisk_MidIsNeutral()
        {
            Assert.AreEqual(1f, VitalityRules.DeathRiskFactor(50), Eps);
            Assert.AreEqual(1.5f, VitalityRules.DeathRiskFactor(0), Eps);   // 病弱
            Assert.AreEqual(0.7f, VitalityRules.DeathRiskFactor(100), Eps); // 頑健
        }

        [Test]
        public void Vitality_Recovery_Linear()
        {
            Assert.AreEqual(1f, VitalityRules.FatigueRecoveryFactor(50), Eps);
            Assert.AreEqual(0.6f, VitalityRules.FatigueRecoveryFactor(0), Eps);
            Assert.AreEqual(1.4f, VitalityRules.FatigueRecoveryFactor(100), Eps);
        }

        // ===== B5/C9 StandingRules =====

        [Test]
        public void Standing_NetStanding_BoundsAndInfamy()
        {
            Assert.AreEqual(0f, StandingRules.NetStanding(0, 0, 0, 0), Eps);
            Assert.AreEqual(1f, StandingRules.NetStanding(100, 100, 100, 0), Eps);
            // 高名・高人望でも悪名で総合が下がる
            float clean = StandingRules.NetStanding(80, 80, 80, 0);
            float dirty = StandingRules.NetStanding(80, 80, 80, 100);
            Assert.IsTrue(dirty < clean);
        }

        [Test]
        public void Standing_Unrest_And_Appeal()
        {
            Assert.AreEqual(0.5f, StandingRules.UnrestFromInfamy(100), Eps);
            Assert.AreEqual(0f, StandingRules.UnrestFromInfamy(0), Eps);
            Assert.AreEqual(0.5f, StandingRules.RecruitmentAppeal(50, 50), Eps);
            Assert.AreEqual(1f, StandingRules.RecruitmentAppeal(100, 100), Eps);
        }

        // ===== C10 HiddenTraitRules =====

        [Test]
        public void HiddenTrait_RevealChance_Monotonic()
        {
            Assert.AreEqual(0.5f, HiddenTraitRules.RevealChance(50, 50), Eps); // baseReveal+0.3-0.3
            // 情報が高いほど露見しやすい / 秘匿が高いほど露見しにくい
            Assert.IsTrue(HiddenTraitRules.RevealChance(100, 50) > HiddenTraitRules.RevealChance(0, 50));
            Assert.IsTrue(HiddenTraitRules.RevealChance(50, 0) > HiddenTraitRules.RevealChance(50, 100));
            Assert.AreEqual(0f, HiddenTraitRules.RevealChance(0, 100), Eps); // base0.5+0-0.6→クランプ0
        }

        [Test]
        public void HiddenTrait_IsRevealed_RollDeterministic()
        {
            // RevealChance(80,20)=0.5+0.48-0.12=0.86
            Assert.IsTrue(HiddenTraitRules.IsRevealed(80, 20, 0.5f));
            Assert.IsFalse(HiddenTraitRules.IsRevealed(80, 20, 0.9f));
        }

        // ===== A1 RelationshipSeedRules（既存 PersonRelationGraph への橋渡し） =====

        [Test]
        public void RelationshipSeed_Resolve_LinksKnownNames()
        {
            var graph = new PersonRelationGraph();
            var seeds = new List<RelationshipSeed>
            {
                new RelationshipSeed { targetName = "ミレイア", kind = RelationKind.親愛, intensity = 80 },
                new RelationshipSeed { targetName = "不明な人", kind = RelationKind.遺恨, intensity = 50 }, // 解決不可
                new RelationshipSeed { targetName = "", kind = RelationKind.恩義, intensity = 50 },         // 空名
            };
            var nameToId = new Dictionary<string, int> { { "アウレリア", 1 }, { "ミレイア", 2 } };

            int linked = RelationshipSeedRules.Resolve(graph, 1, seeds, nameToId);

            Assert.AreEqual(1, linked); // ミレイアのみ解決
            var edge = graph.Get(1, 2, RelationKind.親愛);
            Assert.IsNotNull(edge);
            Assert.AreEqual(0.8f, edge.strength, Eps); // 80/100
        }

        [Test]
        public void RelationshipSeed_Resolve_NullSafeAndSelfSkip()
        {
            Assert.AreEqual(0, RelationshipSeedRules.Resolve(null, 1, null, null));
            Assert.AreEqual(0.8f, RelationshipSeedRules.ToStrength(80), Eps);
            Assert.AreEqual(1f, RelationshipSeedRules.ToStrength(150), Eps); // クランプ

            var graph = new PersonRelationGraph();
            var seeds = new List<RelationshipSeed>
            {
                new RelationshipSeed { targetName = "自分", kind = RelationKind.親愛, intensity = 50 },
            };
            var nameToId = new Dictionary<string, int> { { "自分", 7 } };
            Assert.AreEqual(0, RelationshipSeedRules.Resolve(graph, 7, seeds, nameToId)); // 自己参照はスキップ
        }
    }
}
