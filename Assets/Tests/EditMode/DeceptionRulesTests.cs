using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>DeceptionRules（戦略的欺瞞・#1126）の EditMode テスト。既定Paramsで期待値を固定。</summary>
    public class DeceptionRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>信憑性＝もっともらしさ×経路信頼×（1−敵情報力×0.7）。既定値で固定。</summary>
        [Test]
        public void DeceptionCredibility_既定値で確定する()
        {
            // 0.8 × 0.9 × (1 − 0.5×0.7) = 0.72 × 0.65 = 0.468
            float cred = DeceptionRules.DeceptionCredibility(0.8f, 0.9f, 0.5f);
            Assert.AreEqual(0.468f, cred, Eps);
        }

        /// <summary>敵が聡明なほど信憑性は割り引かれる＝聡明さが防壁。</summary>
        [Test]
        public void DeceptionCredibility_聡明な敵ほど信じない()
        {
            float dull = DeceptionRules.DeceptionCredibility(1f, 1f, 0f);
            float sharp = DeceptionRules.DeceptionCredibility(1f, 1f, 1f);
            Assert.AreEqual(1f, dull, Eps);
            // 1 × 1 × (1 − 1×0.7) = 0.3
            Assert.AreEqual(0.3f, sharp, Eps);
            Assert.Less(sharp, dull);
        }

        /// <summary>植え付ける誤差＝信憑性×誤差規模。信じた分だけ実態から外れる。</summary>
        [Test]
        public void PerceptionBias_信憑性に比例する()
        {
            Assert.AreEqual(20f, DeceptionRules.PerceptionBias(0.5f, 40f), Eps);
            Assert.AreEqual(0f, DeceptionRules.PerceptionBias(0f, 40f), Eps);
        }

        /// <summary>誘導効果＝信憑性×囮規模×最大誘導0.5。本命を手薄にする。</summary>
        [Test]
        public void MisdirectionEffect_既定値で確定する()
        {
            // 0.6 × 0.8 × 0.5 = 0.24
            Assert.AreEqual(0.24f, DeceptionRules.MisdirectionEffect(0.6f, 0.8f), Eps);
        }

        /// <summary>矛盾の蓄積で信憑性が崩れる＝偽情報は観測で剥がれる。</summary>
        [Test]
        public void ConsistencyDecayTick_矛盾で崩れ無矛盾で持続する()
        {
            // 0.8 − 1.0×0.5×1 = 0.3
            Assert.AreEqual(0.3f, DeceptionRules.ConsistencyDecayTick(0.8f, 1f, 1f), Eps);
            // 矛盾なし＝持続
            Assert.AreEqual(0.8f, DeceptionRules.ConsistencyDecayTick(0.8f, 0f, 1f), Eps);
        }

        /// <summary>露見は信憑性が低く防諜が強いほど起きる＝決定論 roll で分岐。</summary>
        [Test]
        public void Exposure_低信憑性かつ強防諜で見破られる()
        {
            // 露見率 = (1 − 0.2) × 1.0 = 0.8
            Assert.AreEqual(0.8f, DeceptionRules.ExposureChance(0.2f, 1f), Eps);
            Assert.IsTrue(DeceptionRules.Exposure(0.2f, 1f, 0.5f));   // roll<0.8
            Assert.IsFalse(DeceptionRules.Exposure(0.2f, 1f, 0.9f));  // roll>=0.8
            // 高信憑性は見破られにくい
            Assert.IsFalse(DeceptionRules.Exposure(0.9f, 1f, 0.5f));  // 露見率0.1
        }

        /// <summary>露見の逆効果＝規模×1.5。ばれた嘘は規模ぶんの信用を焼く＝二度と使えない。</summary>
        [Test]
        public void BacklashOnExposure_規模に比例した信用焼失()
        {
            // 0.6 × 1.5 = 0.9（大きな嘘ほど露見の代償が大きい）
            Assert.AreEqual(0.9f, DeceptionRules.BacklashOnExposure(0.6f), Eps);
            float small = DeceptionRules.BacklashOnExposure(0.1f);
            float big = DeceptionRules.BacklashOnExposure(0.8f);
            Assert.Less(small, big);
        }
    }
}
