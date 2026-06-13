using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    public class SuppressionFireRulesTests
    {
        const float Eps = 1e-4f;
        // Pow を含む式（機動阻害）はわずかに緩める。
        const float PowEps = 1e-3f;

        [Test]
        public void SuppressionLevel_VolumeAndAccuracy_CapsAtMax()
        {
            // 基準弾量×精度1.0 で上限 0.95。
            Assert.AreEqual(0.95f, SuppressionFireRules.SuppressionLevel(100f, 1.0f), Eps);
            // 半分の弾量×精度0.8。
            Assert.AreEqual(0.38f, SuppressionFireRules.SuppressionLevel(50f, 0.8f), Eps);
            // 過大弾量でも上限を超えない。
            Assert.AreEqual(0.95f, SuppressionFireRules.SuppressionLevel(200f, 1.0f), Eps);
        }

        [Test]
        public void ActionPenalty_ScalesWithSuppression()
        {
            Assert.AreEqual(0.76f, SuppressionFireRules.ActionPenalty(0.95f), Eps);
            Assert.AreEqual(0f, SuppressionFireRules.ActionPenalty(0f), Eps);
        }

        [Test]
        public void ManeuverImpairment_CurvedBySuppression()
        {
            // Pow(0.95,1.5)*0.6 ≈ 0.555567。
            Assert.AreEqual(0.555567f, SuppressionFireRules.ManeuverImpairment(0.95f), PowEps);
            Assert.AreEqual(0f, SuppressionFireRules.ManeuverImpairment(0f), Eps);
        }

        [Test]
        public void AmmoConsumption_VolumeTimesTime()
        {
            Assert.AreEqual(100f, SuppressionFireRules.AmmoConsumption(100f, 2f), Eps);
            // 経過0で消費なし。
            Assert.AreEqual(0f, SuppressionFireRules.AmmoConsumption(100f, 0f), Eps);
        }

        [Test]
        public void SuppressionDecay_DropsOverTime()
        {
            Assert.AreEqual(0.4f, SuppressionFireRules.SuppressionDecay(0.8f, 1f), Eps);
            // 長時間途切れれば 0 で下げ止まる。
            Assert.AreEqual(0f, SuppressionFireRules.SuppressionDecay(0.3f, 5f), Eps);
        }

        [Test]
        public void PinDownEffect_LowMoraleIsPinnedMore()
        {
            // 同じ制圧度でも士気0の敵ほど強く釘付け。
            float low = SuppressionFireRules.PinDownEffect(0.8f, 0.0f);
            float high = SuppressionFireRules.PinDownEffect(0.8f, 1.0f);
            Assert.AreEqual(0.8f, low, Eps);
            Assert.AreEqual(0.4f, high, Eps);
            Assert.Greater(low, high);
        }

        [Test]
        public void CoveringFireValue_RewardsFriendlyManeuver()
        {
            Assert.AreEqual(1.9f, SuppressionFireRules.CoveringFireValue(0.95f, 2f), Eps);
            // 味方が動かなければ援護価値は0。
            Assert.AreEqual(0f, SuppressionFireRules.CoveringFireValue(0.95f, 0f), Eps);
        }

        [Test]
        public void IsSuppressed_Threshold()
        {
            Assert.IsTrue(SuppressionFireRules.IsSuppressed(0.6f, 0.5f));
            Assert.IsFalse(SuppressionFireRules.IsSuppressed(0.4f, 0.5f));
        }

        [Test]
        public void Narrative_BarragePinsEnemyAndCoversManeuver_ThenLapseReleases()
        {
            // 継続的な弾幕＝十分な弾量×精度で敵を制圧。
            float supp = SuppressionFireRules.SuppressionLevel(100f, 1.0f);
            Assert.IsTrue(SuppressionFireRules.IsSuppressed(supp));

            // 制圧された敵は反撃・前進が鈍る。
            Assert.Greater(SuppressionFireRules.ActionPenalty(supp), 0f);

            // その隙に味方が機動すれば援護価値が生じる（敵を抑えて動かす）。
            float covering = SuppressionFireRules.CoveringFireValue(supp, 1.0f);
            Assert.Greater(covering, 0f);

            // しかし射撃が途切れる（3秒の中断）と制圧は解ける。
            float lapsed = SuppressionFireRules.SuppressionDecay(supp, 3f);
            Assert.AreEqual(0f, lapsed, Eps);
            Assert.IsFalse(SuppressionFireRules.IsSuppressed(lapsed));
        }
    }
}
