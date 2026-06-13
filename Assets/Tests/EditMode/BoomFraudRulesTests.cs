using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>ブーム詐欺と信頼崩壊（KNDB-5 #1621）の純ロジック検証。既定Paramsで期待値を固定。</summary>
    public class BoomFraudRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>好況が強く監督が緩いほど詐欺の出現確率が上がる＝熱狂は審査を甘くする。</summary>
        [Test]
        public void FraudEmergenceChance_RisesWithBoomAndLooseOversight()
        {
            // 既定 emergenceBase=0.6。好況1×監督0 ⇒ 0.6×1×(1−0)=0.6
            Assert.AreEqual(0.6f, BoomFraudRules.FraudEmergenceChance(1f, 0f), Eps);
            // 監督完璧（1）なら出現しない
            Assert.AreEqual(0f, BoomFraudRules.FraudEmergenceChance(1f, 1f), Eps);
            // 好況0なら湧く土壌がない
            Assert.AreEqual(0f, BoomFraudRules.FraudEmergenceChance(0f, 0f), Eps);
            // 好況0.5×監督0.5 ⇒ 0.6×0.5×0.5=0.15
            Assert.AreEqual(0.15f, BoomFraudRules.FraudEmergenceChance(0.5f, 0.5f), Eps);
        }

        /// <summary>出現判定は決定論＝roll が確率未満なら生起。</summary>
        [Test]
        public void FraudEmerges_IsDeterministic()
        {
            Assert.IsTrue(BoomFraudRules.FraudEmerges(0.6f, 0.59f));
            Assert.IsFalse(BoomFraudRules.FraudEmerges(0.6f, 0.6f));
            Assert.IsFalse(BoomFraudRules.FraudEmerges(0.6f, 0.9f));
        }

        /// <summary>好況中ほど隠蔽倍率が上がる＝上げ相場は不正を覆い隠す（好況0で1.0）。</summary>
        [Test]
        public void ConcealmentDuringBoom_HidesMoreInStrongerBoom()
        {
            // 既定 concealmentScale=2.0。好況0 ⇒ 1.0（隠蔽なし）
            Assert.AreEqual(1f, BoomFraudRules.ConcealmentDuringBoom(0f), Eps);
            // 好況1 ⇒ 1＋2×1=3.0倍隠せる
            Assert.AreEqual(3f, BoomFraudRules.ConcealmentDuringBoom(1f), Eps);
            // 好況0.5 ⇒ 1＋2×0.5=2.0
            Assert.AreEqual(2f, BoomFraudRules.ConcealmentDuringBoom(0.5f), Eps);
        }

        /// <summary>収縮が深く詐欺蓄積が多いほど発覚する＝潮が引くと裸が見える。</summary>
        [Test]
        public void ExposureChance_RisesWithContractionAndFraudStock()
        {
            // 既定 exposureBase=0.8。収縮1×蓄積1 ⇒ 0.8
            Assert.AreEqual(0.8f, BoomFraudRules.ExposureChance(1f, 1f), Eps);
            // 好況中（収縮0）は暴かれない
            Assert.AreEqual(0f, BoomFraudRules.ExposureChance(0f, 1f), Eps);
            // 蓄積0なら暴くものがない
            Assert.AreEqual(0f, BoomFraudRules.ExposureChance(1f, 0f), Eps);
            // 収縮0.5×蓄積0.5 ⇒ 0.8×0.5×0.5=0.2
            Assert.AreEqual(0.2f, BoomFraudRules.ExposureChance(0.5f, 0.5f), Eps);
        }

        /// <summary>発覚した詐欺量だけ信頼が崩れる＝大きな露見ほど信頼を深く損なう。</summary>
        [Test]
        public void TrustCollapse_FallsByExposedFraud()
        {
            // 既定 trustCollapseScale=1.0。信頼0.9から詐欺0.3が露見 ⇒ 0.9−0.3=0.6
            Assert.AreEqual(0.6f, BoomFraudRules.TrustCollapse(0.3f, 0.9f), Eps);
            // 露見0なら信頼は不変
            Assert.AreEqual(0.9f, BoomFraudRules.TrustCollapse(0f, 0.9f), Eps);
            // 大量露見は下限0でクランプ
            Assert.AreEqual(0f, BoomFraudRules.TrustCollapse(1f, 0.5f), Eps);
        }

        /// <summary>詐欺ストックは出現で積もり発覚で吐き出される＝好況が育て不況が暴く動学。</summary>
        [Test]
        public void AccumulatedFraudTick_GrowsAndDrains()
        {
            // 蓄積：stock0.2、出現率1、発覚率0、dt1 ⇒ 0.2＋0.1×1×1=0.3
            Assert.AreEqual(0.3f, BoomFraudRules.AccumulatedFraudTick(0.2f, 1f, 0f, 1f), Eps);
            // 発覚：stock0.5、出現率0、発覚率1、dt1 ⇒ 0.5−0.2×1×0.5×1=0.4
            Assert.AreEqual(0.4f, BoomFraudRules.AccumulatedFraudTick(0.5f, 0f, 1f, 1f), Eps);
        }

        /// <summary>蓄積詐欺×深い収縮が連鎖リスクを生む＝隠れた裸が多いほど引き潮が致命的。</summary>
        [Test]
        public void SystemicRisk_RisesWithFraudAndContraction()
        {
            // 既定 systemicScale=1.0。蓄積1×収縮1 ⇒ 1.0
            Assert.AreEqual(1f, BoomFraudRules.SystemicRisk(1f, 1f), Eps);
            // 蓄積0.5×収縮0.4 ⇒ 0.2
            Assert.AreEqual(0.2f, BoomFraudRules.SystemicRisk(0.5f, 0.4f), Eps);
            // 収縮0（好況中）なら連鎖しない
            Assert.AreEqual(0f, BoomFraudRules.SystemicRisk(1f, 0f), Eps);
        }

        /// <summary>発覚判定は決定論＝roll が確率未満なら蓄積詐欺が暴かれる。</summary>
        [Test]
        public void FraudExposed_IsDeterministic()
        {
            Assert.IsTrue(BoomFraudRules.FraudExposed(0.8f, 0.79f));
            Assert.IsFalse(BoomFraudRules.FraudExposed(0.8f, 0.8f));
        }
    }
}
