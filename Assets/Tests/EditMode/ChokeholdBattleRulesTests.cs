using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>隘路戦闘の純ロジック（ChokeholdBattleRules）の EditMode テスト。既定 Params で期待値固定。</summary>
    public class ChokeholdBattleRulesTests
    {
        const float Eps = 1e-4f;
        const float PowEps = 1e-3f;

        [Test]
        public void EffectiveFrontage_ScalesWithWidthAndFloors()
        {
            // 回廊幅4→正面4、幅0でも下限0.1で完全には消えない。
            Assert.AreEqual(4f, ChokeholdBattleRules.EffectiveFrontage(4f), Eps);
            Assert.AreEqual(0.1f, ChokeholdBattleRules.EffectiveFrontage(0f), Eps);
            Assert.AreEqual(0.1f, ChokeholdBattleRules.EffectiveFrontage(-5f), Eps);
        }

        [Test]
        public void CommittableStrength_CapsAtFrontage()
        {
            // 総兵力100でも正面4なら4しか出せない。正面が総兵力より広ければ全量出せる。
            Assert.AreEqual(4f, ChokeholdBattleRules.CommittableStrength(100f, 4f), Eps);
            Assert.AreEqual(50f, ChokeholdBattleRules.CommittableStrength(50f, 200f), Eps);
        }

        [Test]
        public void NumericalNullification_LargeArmyMostlyNullified()
        {
            // 攻者100を正面4にぶつけると96%が無効化される。攻者0なら0。
            Assert.AreEqual(0.96f, ChokeholdBattleRules.NumericalNullification(100f, 4f), Eps);
            Assert.AreEqual(0f, ChokeholdBattleRules.NumericalNullification(0f, 4f), Eps);
        }

        [Test]
        public void DefenderAdvantage_NarrowFrontageFavorsFewDefenders()
        {
            // 守備10・正面4 → 1 + 2*(10/14) = 2.4285714。守備0なら等倍。
            Assert.AreEqual(2.4285714f, ChokeholdBattleRules.DefenderAdvantage(4f, 10f), PowEps);
            Assert.AreEqual(1f, ChokeholdBattleRules.DefenderAdvantage(4f, 0f), Eps);
            // 正面が広いほど守備有利は薄れる（単調）。
            Assert.Less(ChokeholdBattleRules.DefenderAdvantage(40f, 10f),
                        ChokeholdBattleRules.DefenderAdvantage(4f, 10f));
        }

        [Test]
        public void BreakthroughCost_RisesWhenCommittableIsThin()
        {
            // 守備10・投入4 → 1.5*10*(10/14) = 10.714286。守備0ならコスト0。
            Assert.AreEqual(10.714286f, ChokeholdBattleRules.BreakthroughCost(10f, 4f), PowEps);
            Assert.AreEqual(0f, ChokeholdBattleRules.BreakthroughCost(0f, 4f), Eps);
            // 投入兵力が太いほど（隘路でなくなるほど）突破コストは下がる。
            Assert.Less(ChokeholdBattleRules.BreakthroughCost(10f, 40f),
                        ChokeholdBattleRules.BreakthroughCost(10f, 4f));
        }

        [Test]
        public void BottleneckCongestion_LargeArmyClogsRear()
        {
            // 総兵力100・正面4 → 96%が後方渋滞。総兵力0なら0。
            Assert.AreEqual(0.96f, ChokeholdBattleRules.BottleneckCongestion(100f, 4f), Eps);
            Assert.AreEqual(0f, ChokeholdBattleRules.BottleneckCongestion(0f, 4f), Eps);
        }

        [Test]
        public void ChokePersistence_And_IsChokeHeld()
        {
            // 守備10・損耗率2 → 5 tick 持久。
            Assert.AreEqual(5f, ChokeholdBattleRules.ChokePersistence(10f, 2f), Eps);
            // 正面密度（守備/正面）が閾値1以上なら守られている。
            Assert.IsTrue(ChokeholdBattleRules.IsChokeHeld(10f, 4f, 1f));   // 2.5 >= 1
            Assert.IsFalse(ChokeholdBattleRules.IsChokeHeld(2f, 4f, 1f));   // 0.5 < 1
        }

        [Test]
        public void Story_NarrowCorridorNullifiesGreatArmyAndFewHold()
        {
            // イゼルローン回廊型：狭い回廊（幅2）に大軍（総兵力200）が押し寄せるが、
            // 少数の守備（20）が隘路を封じて持ちこたえる。
            var p = ChokeholdBattleParams.Default;
            float frontage = ChokeholdBattleRules.EffectiveFrontage(2f, p);
            Assert.AreEqual(2f, frontage, Eps);

            // 数の利が消える：攻者200のうち99%が正面に出られず無効化。
            float nullified = ChokeholdBattleRules.NumericalNullification(200f, frontage);
            Assert.AreEqual(0.99f, nullified, Eps);

            // 大軍は後方で渋滞する（99%が控えに詰まる）。
            float congestion = ChokeholdBattleRules.BottleneckCongestion(200f, frontage);
            Assert.AreEqual(0.99f, congestion, Eps);

            // 狭所ゆえ少数の守備が数倍の防御倍率（>1）を得る。
            float advantage = ChokeholdBattleRules.DefenderAdvantage(frontage, 20f, p);
            Assert.Greater(advantage, 1f);
            Assert.AreEqual(2.8181818f, advantage, PowEps);

            // 隘路は守られている＝少数で大軍を食い止める。
            Assert.IsTrue(ChokeholdBattleRules.IsChokeHeld(20f, frontage, 1f));
        }
    }
}
