using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 練度を固定する：経験値の閾値で段階が上がり、戦闘倍率は eliteXp で上限に達する線形、激戦ほど経験を積み、
    /// 新兵補充は加重平均で練度を希釈する。境界・クランプを担保。
    /// </summary>
    public class VeterancyRulesTests
    {
        private static readonly VeterancyParams P = VeterancyParams.Default; // 一般10/精鋭30/古参60・最大+30%・1会戦10xp

        [Test]
        public void LevelOf_Thresholds()
        {
            Assert.AreEqual(ExperienceLevel.新兵, VeterancyRules.LevelOf(0f, P));
            Assert.AreEqual(ExperienceLevel.新兵, VeterancyRules.LevelOf(9.9f, P));
            Assert.AreEqual(ExperienceLevel.一般, VeterancyRules.LevelOf(10f, P)); // 閾値ちょうどで昇格
            Assert.AreEqual(ExperienceLevel.精鋭, VeterancyRules.LevelOf(30f, P));
            Assert.AreEqual(ExperienceLevel.古参, VeterancyRules.LevelOf(60f, P));
            Assert.AreEqual(ExperienceLevel.古参, VeterancyRules.LevelOf(999f, P));
        }

        [Test]
        public void GainFromBattle_ScalesWithIntensity()
        {
            Assert.AreEqual(10f, VeterancyRules.GainFromBattle(0f, 1f, P), 1e-5f);   // 激戦＝満額
            Assert.AreEqual(5f, VeterancyRules.GainFromBattle(0f, 0.5f, P), 1e-5f);  // 半分
            Assert.AreEqual(0f, VeterancyRules.GainFromBattle(0f, 0f, P), 1e-5f);    // 無風＝学ばない
            Assert.AreEqual(25f, VeterancyRules.GainFromBattle(20f, 0.5f, P), 1e-5f); // 累積
        }

        [Test]
        public void CombatFactor_LinearToEliteCap()
        {
            Assert.AreEqual(1f, VeterancyRules.CombatFactor(0f, P), 1e-5f);      // 新兵＝素
            Assert.AreEqual(1.15f, VeterancyRules.CombatFactor(30f, P), 1e-5f);  // 中間＝+15%
            Assert.AreEqual(1.3f, VeterancyRules.CombatFactor(60f, P), 1e-5f);   // 古参＝+30%
            Assert.AreEqual(1.3f, VeterancyRules.CombatFactor(120f, P), 1e-5f);  // 上限で頭打ち
        }

        [Test]
        public void DiluteOnReinforce_WeightedAverage()
        {
            // 歴戦60xp の残存50 に新兵(0xp)50 を補充＝練度半減
            Assert.AreEqual(30f, VeterancyRules.DiluteOnReinforce(60f, 50f, 50f), 1e-4f);
            // 補充なし＝据え置き
            Assert.AreEqual(60f, VeterancyRules.DiluteOnReinforce(60f, 100f, 0f), 1e-4f);
            // 全滅後の新編＝新兵の経験値
            Assert.AreEqual(0f, VeterancyRules.DiluteOnReinforce(60f, 0f, 100f), 1e-4f);
            // 双方ゼロは現値維持
            Assert.AreEqual(60f, VeterancyRules.DiluteOnReinforce(60f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void DiluteOnReinforce_ExperiencedReinforcements()
        {
            // 経験持ちの補充(40xp)なら希釈が浅い
            Assert.AreEqual(50f, VeterancyRules.DiluteOnReinforce(60f, 50f, 50f, 40f), 1e-4f);
        }
    }
}
