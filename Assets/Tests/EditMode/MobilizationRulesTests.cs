using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 戦時動員を固定する：水準が上がるほど軍事生産は跳ね民需が削れる、総力戦の長期化は過熱で軍事生産すら
    /// 落ちる（下限1）、支持低下は水準の段数比例。境界・過熱判定を担保。
    /// </summary>
    public class MobilizationRulesTests
    {
        private static readonly MobilizationParams P = MobilizationParams.Default;
        // 部分1.5/総力2.5・民需0.8/0.4・過熱開始100・減衰0.01・支持0.01/段

        [Test]
        public void Factors_ByLevel()
        {
            Assert.AreEqual(1f, MobilizationRules.MilitaryFactor(MobilizationLevel.平時, P), 1e-5f);
            Assert.AreEqual(1.5f, MobilizationRules.MilitaryFactor(MobilizationLevel.部分動員, P), 1e-5f);
            Assert.AreEqual(2.5f, MobilizationRules.MilitaryFactor(MobilizationLevel.総力戦, P), 1e-5f);

            Assert.AreEqual(1f, MobilizationRules.CivilianFactor(MobilizationLevel.平時, P), 1e-5f);
            Assert.AreEqual(0.8f, MobilizationRules.CivilianFactor(MobilizationLevel.部分動員, P), 1e-5f);
            Assert.AreEqual(0.4f, MobilizationRules.CivilianFactor(MobilizationLevel.総力戦, P), 1e-5f);
        }

        [Test]
        public void EffectiveMilitaryFactor_OverheatErodesTotalWar()
        {
            // 過熱前＝基準値のまま
            Assert.AreEqual(2.5f, MobilizationRules.EffectiveMilitaryFactor(MobilizationLevel.総力戦, 100f, P), 1e-5f);
            // 100超過分×0.01 が削れる：時間200＝超過100×0.01=1.0 → 1.5
            Assert.AreEqual(1.5f, MobilizationRules.EffectiveMilitaryFactor(MobilizationLevel.総力戦, 200f, P), 1e-5f);
            // 下限1（平時相当）で頭打ち
            Assert.AreEqual(1f, MobilizationRules.EffectiveMilitaryFactor(MobilizationLevel.総力戦, 9999f, P), 1e-5f);
            // 部分動員は過熱しない
            Assert.AreEqual(1.5f, MobilizationRules.EffectiveMilitaryFactor(MobilizationLevel.部分動員, 9999f, P), 1e-5f);
        }

        [Test]
        public void IsOverheating_OnlyTotalWarBeyondTime()
        {
            Assert.IsFalse(MobilizationRules.IsOverheating(MobilizationLevel.総力戦, 100f, P)); // ちょうど＝未過熱
            Assert.IsTrue(MobilizationRules.IsOverheating(MobilizationLevel.総力戦, 101f, P));
            Assert.IsFalse(MobilizationRules.IsOverheating(MobilizationLevel.部分動員, 999f, P));
            Assert.IsFalse(MobilizationRules.IsOverheating(MobilizationLevel.平時, 999f, P));
        }

        [Test]
        public void SupportDrain_PerLevelPerTime()
        {
            Assert.AreEqual(0f, MobilizationRules.SupportDrain(MobilizationLevel.平時, 10f, P), 1e-5f);
            Assert.AreEqual(0.1f, MobilizationRules.SupportDrain(MobilizationLevel.部分動員, 10f, P), 1e-5f); // 1段×0.01×10
            Assert.AreEqual(0.2f, MobilizationRules.SupportDrain(MobilizationLevel.総力戦, 10f, P), 1e-5f);   // 2段×0.01×10
        }
    }
}
