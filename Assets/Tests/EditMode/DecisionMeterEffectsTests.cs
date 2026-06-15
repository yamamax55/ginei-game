using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 決裁の effectKey → マクロメーター増減の整合を固定する（決裁ゲーム MVP）。
    /// 全サンプル建白が「世界（FactionState）」と「勝敗メーター」の両方を同じキーで動かせることを保証する。
    /// </summary>
    public class DecisionMeterEffectsTests
    {
        [Test]
        public void EverySample_HasMeterDelta_Registered()
        {
            foreach (var s in RingiSampleData.All)
                Assert.IsTrue(DecisionMeterEffects.Has(s.effectKey),
                    $"メーター増減 未登録の効果キー: {s.effectKey}（{s.title}）");
        }

        [Test]
        public void EverySample_HasNonZeroMeterDelta()
        {
            foreach (var s in RingiSampleData.All)
            {
                var d = DecisionMeterEffects.For(s.effectKey);
                float sum = Mathf.Abs(d.military) + Mathf.Abs(d.support) + Mathf.Abs(d.treasury)
                          + Mathf.Abs(d.control) + Mathf.Abs(d.hegemony);
                Assert.Greater(sum, 0f, $"{s.title}: 何のメーターも動かさない（飾りの決裁）");
            }
        }

        [Test]
        public void UnknownKey_ReturnsZero()
        {
            Assert.IsFalse(DecisionMeterEffects.Has("does.not.exist"));
            var d = DecisionMeterEffects.For("does.not.exist");
            Assert.AreEqual(0f, d.military + d.support + d.treasury + d.control + d.hegemony, 1e-6f);
        }

        [Test]
        public void Offensive_AdvancesHegemony_AtCostOfMilitary()
        {
            var d = DecisionMeterEffects.For("mil.offensive");
            Assert.Greater(d.hegemony, 0f, "前線攻勢は覇権を進める");
            Assert.Less(d.military, 0f, "前線攻勢は軍事を磨り減らす");
        }
    }
}
