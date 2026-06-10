using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 陽動を固定する：信憑性＝規模×(1−敵情報×係数)、見破り判定（roll決定論）、吸引量と本命の手薄さ、
    /// 露見した陽動部隊の孤立損害。境界を担保。
    /// </summary>
    public class FeintRulesTests
    {
        private static readonly FeintParams P = FeintParams.Default;
        // 最大吸引0.5/見破り0.8/露見損害0.3

        [Test]
        public void Credibility_ScaleDiscountedByIntel()
        {
            Assert.AreEqual(1f, FeintRules.Credibility(1f, 0f, P), 1e-5f);     // 盲目の敵には大芝居が通る
            Assert.AreEqual(0.2f, FeintRules.Credibility(1f, 1f, P), 1e-5f);   // 目の良い敵＝1×(1−0.8)
            Assert.AreEqual(0.5f, FeintRules.Credibility(0.5f, 0f, P), 1e-5f); // 小部隊の芝居は半信半疑
            Assert.AreEqual(0f, FeintRules.Credibility(0f, 0f, P), 1e-5f);     // 芝居をしていない
        }

        [Test]
        public void SeenThrough_DeterministicByRoll()
        {
            // 信憑性0.5：roll<0.5 は信じる、roll≥0.5 は見破る
            Assert.IsFalse(FeintRules.SeenThrough(0.5f, 0f, 0.49f, P));
            Assert.IsTrue(FeintRules.SeenThrough(0.5f, 0f, 0.5f, P));
        }

        [Test]
        public void DrawnForce_CredibilityTimesMaxRatio()
        {
            // 敵1000・信憑性1＝最大50%＝500 が釣れる
            Assert.AreEqual(500f, FeintRules.DrawnForce(1000f, 1f, P), 1e-4f);
            Assert.AreEqual(250f, FeintRules.DrawnForce(1000f, 0.5f, P), 1e-4f);
            // 見破られた陽動（信憑性0）は何も釣れない
            Assert.AreEqual(0f, FeintRules.DrawnForce(1000f, 0f, P), 1e-5f);
        }

        [Test]
        public void MainFrontWeakening_FractionDrawn()
        {
            Assert.AreEqual(0.5f, FeintRules.MainFrontWeakening(1000f, 1f, P), 1e-5f);
            Assert.AreEqual(0f, FeintRules.MainFrontWeakening(0f, 1f, P), 1e-5f); // 敵なし＝0
        }

        [Test]
        public void ExposedLosses_PriceOfTheActFails()
        {
            Assert.AreEqual(30f, FeintRules.ExposedLosses(100f, P), 1e-4f); // 露見＝3割の出血
            Assert.AreEqual(0f, FeintRules.ExposedLosses(0f, P), 1e-5f);
        }
    }
}
