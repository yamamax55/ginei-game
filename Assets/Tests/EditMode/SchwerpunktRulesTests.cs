using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 重点（シュヴェルプンクト）の純ロジック <see cref="SchwerpunktRules"/> の EditMode テスト。
    /// 既定 <see cref="SchwerpunktParams.Default"/> で期待値固定（許容 1e-4f、Pow 箇所のみ緩める）。
    /// </summary>
    public class SchwerpunktRulesTests
    {
        const float Tol = 1e-4f;
        const float PowTol = 1e-3f;

        [Test]
        public void FocalPointStrength_FractionTimesTotal()
        {
            // 0.8 × 1000 = 800
            Assert.AreEqual(800f, SchwerpunktRules.FocalPointStrength(0.8f, 1000f), Tol);
            // 割合は 0..1 にクランプ、総戦力は非負
            Assert.AreEqual(1000f, SchwerpunktRules.FocalPointStrength(1.5f, 1000f), Tol);
            Assert.AreEqual(0f, SchwerpunktRules.FocalPointStrength(0.5f, -100f), Tol);
        }

        [Test]
        public void SupportingEffortEconomy_SavesWithEfficiency()
        {
            // 既定効率0.5：1 − 0.4×(1−0.5) = 0.8
            Assert.AreEqual(0.8f, SchwerpunktRules.SupportingEffortEconomy(0.4f), Tol);
            // 支作戦割合0 → 完全節約 1.0
            Assert.AreEqual(1f, SchwerpunktRules.SupportingEffortEconomy(0f), Tol);
        }

        [Test]
        public void BreakthroughConcentration_SquaresLocalSuperiority()
        {
            // (800/400)^2 = 4（二乗則）
            Assert.AreEqual(4f, SchwerpunktRules.BreakthroughConcentration(800f, 400f), PowTol);
            // 拮抗は 1.0
            Assert.AreEqual(1f, SchwerpunktRules.BreakthroughConcentration(500f, 500f), PowTol);
        }

        [Test]
        public void MisidentificationRisk_RisesWithDeficitVsEnemyMain()
        {
            // 重点300 が敵主力1000 に当たる：(1000−300)/1000 × 1.0 = 0.7（総崩れリスク高）
            Assert.AreEqual(0.7f, SchwerpunktRules.MisidentificationRisk(300f, 1000f), Tol);
            // 重点が敵主力以上なら選定ミスなし＝0
            Assert.AreEqual(0f, SchwerpunktRules.MisidentificationRisk(1200f, 1000f), Tol);
        }

        [Test]
        public void WeightShift_NetValueOfMovingFocus()
        {
            // 新好機9 − 現重点5 − 転換コスト2 = 2（移すべき）
            Assert.AreEqual(2f, SchwerpunktRules.WeightShift(5f, 9f, 2f), Tol);
            // 好機が現重点＋コストに届かなければ負（移すべきでない）
            Assert.AreEqual(-3f, SchwerpunktRules.WeightShift(5f, 4f, 2f), Tol);
        }

        [Test]
        public void OverextensionFromNarrowFocus_OnlyAboveThreshold()
        {
            // 既定閾値0.7・感度2.0：(0.9−0.7)×2 = 0.4
            Assert.AreEqual(0.4f, SchwerpunktRules.OverextensionFromNarrowFocus(0.9f), Tol);
            // 閾値以下は 0
            Assert.AreEqual(0f, SchwerpunktRules.OverextensionFromNarrowFocus(0.6f), Tol);
        }

        [Test]
        public void DecisiveBreakthrough_DiscountedByEnemyDepth()
        {
            // clamp01(4−1)/(1+1) = 1.0/2 = 0.5
            Assert.AreEqual(0.5f, SchwerpunktRules.DecisiveBreakthrough(4f, 1f), Tol);
            // 縦深0なら満額 1.0
            Assert.AreEqual(1f, SchwerpunktRules.DecisiveBreakthrough(4f, 0f), Tol);
            // 拮抗以下の突破力は決定的でない＝0
            Assert.AreEqual(0f, SchwerpunktRules.DecisiveBreakthrough(0.8f, 1f), Tol);
        }

        [Test]
        public void IsSchwerpunktDecisive_AtThreshold()
        {
            Assert.IsTrue(SchwerpunktRules.IsSchwerpunktDecisive(0.5f));  // 既定閾値0.5
            Assert.IsFalse(SchwerpunktRules.IsSchwerpunktDecisive(0.3f));
        }

        [Test]
        public void Story_FocusedAssaultBreaksThrough_ButMisreadFocusCollapses()
        {
            float total = 1000f;

            // 重点を正しく見抜く：弱点（敵局所400）へ総戦力の8割を集中
            float focal = SchwerpunktRules.FocalPointStrength(0.8f, total); // 800
            Assert.AreEqual(800f, focal, Tol);
            // 支作戦は最小限で済ませる（残り2割で他正面を陽動）
            float economy = SchwerpunktRules.SupportingEffortEconomy(0.2f); // 1 − 0.2×0.5 = 0.9
            Assert.AreEqual(0.9f, economy, Tol);
            // 重点での突破力＝(800/400)^2 = 4
            float bc = SchwerpunktRules.BreakthroughConcentration(focal, 400f);
            Assert.AreEqual(4f, bc, PowTol);
            // 浅い縦深(1)を割り引いても決定的突破に達する
            float decisive = SchwerpunktRules.DecisiveBreakthrough(bc, 1f);
            Assert.AreEqual(0.5f, decisive, Tol);
            Assert.IsTrue(SchwerpunktRules.IsSchwerpunktDecisive(decisive), "正しい重点選定は決定的突破を得る");

            // 逆に重点選定を誤り、同じ800を敵主力1000に当ててしまうと劣勢→総崩れリスク
            float misreadRisk = SchwerpunktRules.MisidentificationRisk(focal, 1000f); // (1000−800)/1000 = 0.2
            Assert.AreEqual(0.2f, misreadRisk, Tol);
            // さらに薄い重点(300)で敵主力に当たれば致命的（0.7）
            float fatalRisk = SchwerpunktRules.MisidentificationRisk(300f, 1000f);
            Assert.AreEqual(0.7f, fatalRisk, Tol);
            Assert.Greater(fatalRisk, misreadRisk, "重点が薄いほど敵主力誤当りの総崩れリスクは大きい");
        }
    }
}
