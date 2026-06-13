using NUnit.Framework;
using Ginei;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>ADM-2 成長の会戦配線：基準＋成長（軍神は天地人で100超）。GrowthRules/TenchijinRules へ委譲。</summary>
    public class AdmiralGrowthRulesTests
    {
        private static AdmiralData Admiral(int attack, bool transcendent)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack; a.isTranscendent = transcendent; a.staffOfficers = new AdmiralData[0];
            return a;
        }

        [Test]
        public void GrownStat_NullGrowth_IsBaseUnchanged()
        {
            Assert.AreEqual(80, AdmiralGrowthRules.GrownStat(80, null));
        }

        [Test]
        public void GrownStat_WithGrowth_CapsAt100ForNormalAdmiral()
        {
            var g = new Growth(GrowthArchetype.老練型, 100000f); // 飽和＝ボーナス大
            Assert.AreEqual(100, AdmiralGrowthRules.GrownStat(80, g)); // 80+成長 → 100で頭打ち
        }

        [Test]
        public void GrownStat_Transcendent_Exceeds100WhenTenchijinAligned()
        {
            var g = new Growth(GrowthArchetype.老練型, 100000f);
            // 軍神＋天地人が揃う → 120（上限突破）。
            Assert.AreEqual(120, AdmiralGrowthRules.GrownStat(80, g, Admiral(80, true), Tenchijin.Ideal));
            Assert.AreEqual(120, AdmiralGrowthRules.Attack(Admiral2(80, true, g), Tenchijin.Ideal));
            // 天地人が無ければ軍神でも100で頭打ち。
            Assert.AreEqual(100, AdmiralGrowthRules.Attack(Admiral2(80, true, g), Tenchijin.None));
            // 並の提督は揃っても100。
            Assert.AreEqual(100, AdmiralGrowthRules.Attack(Admiral2(80, false, g), Tenchijin.Ideal));
        }

        private static AdmiralData Admiral2(int attack, bool transcendent, Growth g)
        {
            var a = Admiral(attack, transcendent);
            a.growth = g;
            return a;
        }
    }
}
