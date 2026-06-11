using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>世代断絶（失われた世代・#1416）の純ロジック検証。既定Params具体値で期待値を固定。</summary>
    public class GenerationalWoundRulesTests
    {
        const float EPS = 1e-4f;
        static GenerationalWoundParams P => GenerationalWoundParams.Default;

        /// <summary>世代の損耗＝若年戦死×出征世代規模の積（大きな世代の大量戦死ほど大きい）。</summary>
        [Test]
        public void GenerationLoss_IsProductOfCasualtiesAndCohort()
        {
            Assert.AreEqual(0.5f, GenerationalWoundRules.GenerationLoss(1f, 0.5f), EPS);
            Assert.AreEqual(0.16f, GenerationalWoundRules.GenerationLoss(0.4f, 0.4f), EPS);
            // クランプ＝過大入力でも1を超えない。
            Assert.AreEqual(1f, GenerationalWoundRules.GenerationLoss(2f, 3f), EPS);
            Assert.AreEqual(0f, GenerationalWoundRules.GenerationLoss(-1f, 0.5f), EPS);
        }

        /// <summary>将来の指導者欠乏＝質の高い人材ほど死ぬバイアスで世代損耗が増幅される。</summary>
        [Test]
        public void FutureLeaderShortage_AmplifiesByTalentBias()
        {
            // loss0.5 ×(1+0.3)=0.65 × leadershipFrac0.2 = 0.13。
            Assert.AreEqual(0.13f, GenerationalWoundRules.FutureLeaderShortage(0.5f, 0.2f, P), EPS);
            // 指導者比率0なら欠乏も0。
            Assert.AreEqual(0f, GenerationalWoundRules.FutureLeaderShortage(0.5f, 0f, P), EPS);
            // 増幅は1でクランプ＝full loss でも指導者比率を超えない。
            Assert.AreEqual(1f, GenerationalWoundRules.FutureLeaderShortage(1f, 1f, P), EPS);
        }

        /// <summary>遅効＝影響は遅延年数後に顕在化する。経過0では表面化せず、進めるほど目標へ寄る。</summary>
        [Test]
        public void DelayedImpactTick_ManifestsWithDelay()
        {
            // 経過0＝まだ祟らない（目標0）。
            Assert.AreEqual(0f, GenerationalWoundRules.DelayedImpactTick(0f, 0.8f, 0f, 5f, P), EPS);
            // 経過1.0で目標は loss 全量0.8。dt=25(遅延25年)で一気に目標へ到達。
            Assert.AreEqual(0.8f, GenerationalWoundRules.DelayedImpactTick(0f, 0.8f, 1f, 25f, P), EPS);
            // dt 小＝rate=10/25=0.4 ぶんだけ目標0.8へ前進（MoveTowards）。
            Assert.AreEqual(0.4f, GenerationalWoundRules.DelayedImpactTick(0f, 0.8f, 1f, 10f, P), EPS);
        }

        /// <summary>遅効は単調に進み目標を超えない＝今日の戦死が未来の指導層欠落として現れる。</summary>
        [Test]
        public void DelayedImpactTick_IsMonotonicTowardTarget()
        {
            float m = 0f;
            float prev = -1f;
            for (int i = 0; i < 30; i++)
            {
                m = GenerationalWoundRules.DelayedImpactTick(m, 0.6f, 1f, 2f, P);
                Assert.GreaterOrEqual(m + EPS, prev);
                Assert.LessOrEqual(m, 0.6f + EPS); // 目標(loss×経過=0.6)を超えない。
                prev = m;
            }
            Assert.AreEqual(0.6f, m, 1e-2f); // 十分進めば目標へ収束。
        }

        /// <summary>人材プールの枯渇＝失われた世代の分が枯れ、回復は遅い（買い戻せない）。</summary>
        [Test]
        public void TalentPoolDepletion_RecoversSlowly()
        {
            // 回復0＝損耗そのまま。
            Assert.AreEqual(0.8f, GenerationalWoundRules.TalentPoolDepletion(0.8f, 0f, P), EPS);
            // 回復進捗1.0でも poolRecoveryRate0.2 が上限＝0.8×(1-0.2)=0.64 までしか戻らない。
            Assert.AreEqual(0.64f, GenerationalWoundRules.TalentPoolDepletion(0.8f, 1f, P), EPS);
        }

        /// <summary>生存者の傷＝戦闘曝露が原因、世代喪失が深いほど周囲の喪失で増幅。</summary>
        [Test]
        public void SurvivorTrauma_DeepenedByGenerationLoss()
        {
            // exposure0.5 ×(1+loss0)=0.5 × traumaScale0.8 = 0.4。
            Assert.AreEqual(0.4f, GenerationalWoundRules.SurvivorTrauma(0.5f, 0f, P), EPS);
            // 喪失0.5で増幅＝0.5×1.5×0.8=0.6。
            Assert.AreEqual(0.6f, GenerationalWoundRules.SurvivorTrauma(0.5f, 0.5f, P), EPS);
            // 曝露0なら傷も0。
            Assert.AreEqual(0f, GenerationalWoundRules.SurvivorTrauma(0f, 1f, P), EPS);
        }

        /// <summary>知識の断絶＝指導者欠乏×師弟伝承の途絶（師がいないと欠乏が知識喪失へ直結）。</summary>
        [Test]
        public void InstitutionalKnowledgeGap_AmplifiedByBrokenMentorship()
        {
            // 伝承健在(broken0)＝欠乏そのまま。
            Assert.AreEqual(0.4f, GenerationalWoundRules.InstitutionalKnowledgeGap(0.4f, 0f), EPS);
            // 伝承半断絶＝0.4×1.5=0.6。
            Assert.AreEqual(0.6f, GenerationalWoundRules.InstitutionalKnowledgeGap(0.4f, 0.5f), EPS);
            // 完全断絶でクランプ＝高欠乏×2 は1で頭打ち。
            Assert.AreEqual(1f, GenerationalWoundRules.InstitutionalKnowledgeGap(0.7f, 1f), EPS);
        }

        /// <summary>長期弱体化＝人材プール枯渇が深いほど年あたりに静かに進む。失われた世代判定。</summary>
        [Test]
        public void NationalDecline_AndLostGeneration()
        {
            // 枯渇0.5 × declineRate0.05 × dt4 = 0.1 を現状0へ加算。
            Assert.AreEqual(0.1f, GenerationalWoundRules.NationalDeclineFromWound(0f, 0.5f, 4f, P), EPS);
            // 単調＝累積する。
            float d = GenerationalWoundRules.NationalDeclineFromWound(0.1f, 0.5f, 4f, P);
            Assert.AreEqual(0.2f, d, EPS);

            // 失われた世代判定＝既定しきい値0.5。
            Assert.IsTrue(GenerationalWoundRules.IsLostGeneration(0.5f));
            Assert.IsTrue(GenerationalWoundRules.IsLostGeneration(0.8f));
            Assert.IsFalse(GenerationalWoundRules.IsLostGeneration(0.49f));
        }
    }
}
