using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// ADM-2 #2303＝会戦成長が実効能力へ畳み込まれること（<see cref="AdmiralData.EffectiveAttack"/> ほか）を固定する。
    /// GrowthRegistry が空のとき SO の growth フィールドへフォールバックし、成長ぶん実効値が伸びる（上限100）。
    /// </summary>
    public class AdmiralEffectiveGrowthTests
    {
        [SetUp]
        public void Reset() => GrowthRegistry.Clear(); // id キーの実行時成長を空に＝SO の growth でテスト

        private AdmiralData Make(int attack)
        {
            var a = ScriptableObject.CreateInstance<AdmiralData>();
            a.attack = attack;
            return a;
        }

        [Test]
        public void NoGrowth_EffectiveEqualsBase()
        {
            AdmiralData a = Make(80);
            a.growth = null;
            Assert.AreEqual(80, a.EffectiveAttack);
        }

        [Test]
        public void Growth_RaisesEffectiveAttack()
        {
            AdmiralData a = Make(80);
            var g = new Growth(GrowthArchetype.叩き上げ);
            GrowthRules.GainExperience(g, 1000f, 1f);
            a.growth = g;

            int expected = Mathf.Min(AdmiralData.MaxStatValue, AdmiralGrowthRules.GrownStat(80, g));
            Assert.AreEqual(expected, a.EffectiveAttack);
            Assert.Greater(a.EffectiveAttack, 80, "経験を積んだら実効攻撃が基準を上回る");
            Assert.LessOrEqual(a.EffectiveAttack, AdmiralData.MaxStatValue, "通常成長は上限100");
        }
    }
}
