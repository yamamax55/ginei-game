using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// VictoryRules（突破・拠点保持の幾何/時間判定）の特性テスト（#2259）。
    /// 純ロジックのみ。MonoBehaviour・シーン不要。
    /// </summary>
    public class VictoryRulesTests
    {
        // ── BreakthroughAchieved ──────────────────────────────────────────────

        [Test]
        public void BreakthroughAchieved_WhenOutsideRadius_ReturnsTrue()
        {
            // 半径10 の外側（距離11）にいれば突破成功
            Assert.IsTrue(VictoryRules.BreakthroughAchieved(new Vector2(11f, 0f), 10f));
        }

        [Test]
        public void BreakthroughAchieved_WhenExactlyOnRadius_ReturnsTrue()
        {
            // ちょうど境界上（sqrMagnitude == r*r）も成功とみなす
            Assert.IsTrue(VictoryRules.BreakthroughAchieved(new Vector2(10f, 0f), 10f));
        }

        [Test]
        public void BreakthroughAchieved_WhenInsideRadius_ReturnsFalse()
        {
            // 半径10 の内側（距離9）はまだ突破していない
            Assert.IsFalse(VictoryRules.BreakthroughAchieved(new Vector2(9f, 0f), 10f));
        }

        [Test]
        public void BreakthroughAchieved_WhenRadiusZeroOrNegative_ReturnsFalse()
        {
            // battlefieldRadius が 0 以下の場合は常に false（無効設定）
            Assert.IsFalse(VictoryRules.BreakthroughAchieved(new Vector2(100f, 100f), 0f));
            Assert.IsFalse(VictoryRules.BreakthroughAchieved(new Vector2(100f, 100f), -5f));
        }

        [Test]
        public void BreakthroughAchieved_DiagonalPosition_CorrectlyJudged()
        {
            // 斜め方向でも距離で正しく判定される（sqrt(50) ≈ 7.07 > 7）
            Assert.IsTrue(VictoryRules.BreakthroughAchieved(new Vector2(5f, 5f), 7f));
            Assert.IsFalse(VictoryRules.BreakthroughAchieved(new Vector2(5f, 5f), 8f));
        }

        // ── IsInZone ─────────────────────────────────────────────────────────

        [Test]
        public void IsInZone_WhenInsideZone_ReturnsTrue()
        {
            // 中心(3,4) 半径5 の内側に(5,4)がある（距離2）
            Assert.IsTrue(VictoryRules.IsInZone(new Vector2(5f, 4f), new Vector2(3f, 4f), 5f));
        }

        [Test]
        public void IsInZone_WhenExactlyOnBoundary_ReturnsTrue()
        {
            // ちょうど境界上（距離 == 半径）も保持中とみなす
            Assert.IsTrue(VictoryRules.IsInZone(new Vector2(8f, 4f), new Vector2(3f, 4f), 5f));
        }

        [Test]
        public void IsInZone_WhenOutsideZone_ReturnsFalse()
        {
            // 中心(3,4) 半径5 の外側に(10,4)がある（距離7）
            Assert.IsFalse(VictoryRules.IsInZone(new Vector2(10f, 4f), new Vector2(3f, 4f), 5f));
        }

        [Test]
        public void IsInZone_WhenRadiusZeroOrNegative_ReturnsFalse()
        {
            // radius が 0 以下の場合は常に false
            Assert.IsFalse(VictoryRules.IsInZone(Vector2.zero, Vector2.zero, 0f));
            Assert.IsFalse(VictoryRules.IsInZone(Vector2.zero, Vector2.zero, -3f));
        }

        // ── HoldAchieved ─────────────────────────────────────────────────────

        [Test]
        public void HoldAchieved_WhenAccumulatedMeetsRequired_ReturnsTrue()
        {
            Assert.IsTrue(VictoryRules.HoldAchieved(30f, 30f));
        }

        [Test]
        public void HoldAchieved_WhenAccumulatedExceedsRequired_ReturnsTrue()
        {
            Assert.IsTrue(VictoryRules.HoldAchieved(45f, 30f));
        }

        [Test]
        public void HoldAchieved_WhenAccumulatedBelowRequired_ReturnsFalse()
        {
            Assert.IsFalse(VictoryRules.HoldAchieved(29.9f, 30f));
        }

        [Test]
        public void HoldAchieved_WhenRequiredZeroOrNegative_ReturnsFalse()
        {
            // requiredSeconds が 0 以下の場合は常に false（無効設定）
            Assert.IsFalse(VictoryRules.HoldAchieved(100f, 0f));
            Assert.IsFalse(VictoryRules.HoldAchieved(100f, -1f));
        }

        [Test]
        public void HoldAchieved_ZeroAccumulated_ReturnsFalse()
        {
            Assert.IsFalse(VictoryRules.HoldAchieved(0f, 30f));
        }
    }
}
