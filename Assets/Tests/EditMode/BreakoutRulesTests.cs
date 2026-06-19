using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>包囲突破の純ロジック（BreakoutRules）の EditMode テスト。既定 Params で期待値を固定（検算済み）。</summary>
    public class BreakoutRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f; // Pow を含む箇所のみ緩め

        [Test]
        public void PointWeakness_UniformRingHasOnlyFloor_PatchyIsFullyWeak()
        {
            // 0.1 + 0.9*(1-u)
            Assert.AreEqual(0.1f, BreakoutRules.BreakoutPointWeakness(1f), Eps);   // 一様＝穴は底だけ
            Assert.AreEqual(1.0f, BreakoutRules.BreakoutPointWeakness(0f), Eps);   // 不均一＝最弱点は脆い
            Assert.AreEqual(0.55f, BreakoutRules.BreakoutPointWeakness(0.5f), Eps);
        }

        [Test]
        public void ConcentratedPunch_IsForceTimesWeakness()
        {
            Assert.AreEqual(500f, BreakoutRules.ConcentratedPunch(1000f, 0.5f), Eps);
            Assert.AreEqual(0f, BreakoutRules.ConcentratedPunch(0f, 0.9f), Eps);
        }

        [Test]
        public void RingRupture_ScalesWithPunchShare()
        {
            Assert.AreEqual(1.0f, BreakoutRules.RingRupture(1000f, 0f), Eps);        // 守りゼロ＝完全に破れる
            Assert.AreEqual(0.353553f, BreakoutRules.RingRupture(500f, 500f), PowEps); // 拮抗＝0.5^1.5
            Assert.AreEqual(0f, BreakoutRules.RingRupture(0f, 500f), Eps);           // 打撃ゼロ＝破れない
        }

        [Test]
        public void EscapeCorridorWidth_TracksRupture()
        {
            Assert.AreEqual(0.5f, BreakoutRules.EscapeCorridorWidth(0.5f), Eps);
            Assert.AreEqual(1f, BreakoutRules.EscapeCorridorWidth(1.7f), Eps); // クランプ
        }

        [Test]
        public void EscapeFraction_LargeArmyIsHarderToExtract()
        {
            // cap/(cap+size), cap = width*rate(=1)
            Assert.AreEqual(0.5f, BreakoutRules.EscapeFraction(1f, 1f), Eps);
            Assert.AreEqual(0.2f, BreakoutRules.EscapeFraction(1f, 4f), Eps);      // 大軍ほど抜けにくい
            Assert.AreEqual(0.333333f, BreakoutRules.EscapeFraction(0.5f, 1f), Eps);
        }

        [Test]
        public void RearguardCollapse_RisesWithPressureOverGuard()
        {
            Assert.AreEqual(0.5f, BreakoutRules.RearguardCollapse(100f, 100f), Eps);
            Assert.AreEqual(0.75f, BreakoutRules.RearguardCollapse(50f, 150f), Eps);
            Assert.AreEqual(0f, BreakoutRules.RearguardCollapse(1000f, 0f), Eps);
        }

        [Test]
        public void BreakoutLosses_GrowWithTrappedAndCollapse()
        {
            Assert.AreEqual(0.5f, BreakoutRules.BreakoutLosses(0.5f, 0f), Eps);   // 半数取り残し・殿無傷
            Assert.AreEqual(0.44f, BreakoutRules.BreakoutLosses(0.8f, 0.5f), Eps); // 0.2 + 0.8*0.5*0.6
            Assert.AreEqual(0.6f, BreakoutRules.BreakoutLosses(1f, 1f), Eps);     // 全脱出でも殿全滅で損害
            Assert.AreEqual(1.0f, BreakoutRules.BreakoutLosses(0f, 0f), Eps);     // 一兵も抜けず＝壊滅
        }

        [Test]
        public void IsBreakoutSuccessful_UsesThreshold()
        {
            Assert.IsTrue(BreakoutRules.IsBreakoutSuccessful(0.5f));
            Assert.IsFalse(BreakoutRules.IsBreakoutSuccessful(0.2f));
            Assert.IsTrue(BreakoutRules.IsBreakoutSuccessful(0.4f)); // 閾値ちょうどは成功
        }

        [Test]
        public void Story_SmallForceBreaksPatchyRing_LargeForceTrappedInTightRing()
        {
            // 小勢が不均一な包囲環の薄い一点へ全力集中＝突破口を開いて脱出する。
            float weakSpot = BreakoutRules.BreakoutPointWeakness(0.1f); // ほぼ不均一＝0.91
            float punch = BreakoutRules.ConcentratedPunch(800f, weakSpot);
            float rupture = BreakoutRules.RingRupture(punch, 150f); // 薄い守りを上回る
            float width = BreakoutRules.EscapeCorridorWidth(rupture);
            float escapedSmall = BreakoutRules.EscapeFraction(width, 1f); // 小勢
            Assert.IsTrue(BreakoutRules.IsBreakoutSuccessful(escapedSmall),
                "不均一な包囲の弱点を突いた小勢は突破できる");

            // 大軍が一様で厚い包囲に閉じ込められ、殿も崩れる＝抜け切れず壊滅。
            float tightSpot = BreakoutRules.BreakoutPointWeakness(0.95f); // 一様＝底に近い
            float weakPunch = BreakoutRules.ConcentratedPunch(800f, tightSpot);
            float weakRupture = BreakoutRules.RingRupture(weakPunch, 900f); // 厚い守り
            float narrow = BreakoutRules.EscapeCorridorWidth(weakRupture);
            float escapedLarge = BreakoutRules.EscapeFraction(narrow, 6f); // 大軍
            Assert.IsFalse(BreakoutRules.IsBreakoutSuccessful(escapedLarge),
                "厚い一様な包囲に囲まれた大軍は抜け切れない");

            float collapse = BreakoutRules.RearguardCollapse(100f, 500f); // 殿が崩れる
            float losses = BreakoutRules.BreakoutLosses(escapedLarge, collapse);
            Assert.Greater(losses, BreakoutRules.BreakoutLosses(escapedSmall,
                BreakoutRules.RearguardCollapse(400f, 100f)),
                "大軍の失敗突破は小勢の成功突破より損害が大きい");
        }
    }
}
