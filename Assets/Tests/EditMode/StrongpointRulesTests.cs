using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 拠点防御（防御陣地）：築城×地形×守備の防御力・縦深・相互支援・遅滞・逆襲・孤立化・弾力性・保持判定。
    /// 既定 Params で期待値を固定（手計算で検算）。
    /// </summary>
    public class StrongpointRulesTests
    {
        [Test]
        public void PositionStrength_FortifiedTerrainAndGarrisonMultiply()
        {
            // 要害（地形1→×1.5）＋満守備（×1.0）＋築城満点 → 1*1.5*1 = 1.5。
            Assert.AreEqual(1.5f, StrongpointRules.PositionStrength(1f, 1f, 1f), 1e-4f);
            // 平地（地形0→×0.5）＋満守備 → 1*0.5*1 = 0.5。
            Assert.AreEqual(0.5f, StrongpointRules.PositionStrength(1f, 0f, 1f), 1e-4f);
            // 築城0.5・地形0.5（→×1.0）・守備0（→×0.3） → 0.5*1.0*0.3 = 0.15。
            Assert.AreEqual(0.15f, StrongpointRules.PositionStrength(0.5f, 0.5f, 0f), 1e-4f);
        }

        [Test]
        public void DefenseInDepth_GrowsWithLinesAndCapsAtOne()
        {
            // 3線・基準間隔 → 3*0.25 = 0.75。
            Assert.AreEqual(0.75f, StrongpointRules.DefenseInDepth(3, 1f), 1e-4f);
            // 4線でフル（1.0で頭打ち）。
            Assert.AreEqual(1.0f, StrongpointRules.DefenseInDepth(4, 1f), 1e-4f);
            // 2線でも間隔が狭い（0.5）とベタ置きで縦深が逓減 → 2*0.25*0.5 = 0.25。
            Assert.AreEqual(0.25f, StrongpointRules.DefenseInDepth(2, 0.5f), 1e-4f);
        }

        [Test]
        public void MutualSupport_NeedsAdjacentPositionsWithinRange()
        {
            // 隣接2火点・基準射程 → 2*0.25 = 0.5。
            Assert.AreEqual(0.5f, StrongpointRules.MutualSupport(2, 1f), 1e-4f);
            // 隣接4でフル（1.0頭打ち）。
            Assert.AreEqual(1.0f, StrongpointRules.MutualSupport(4, 1f), 1e-4f);
            // 射程が半分しか届かない → 2*0.25*0.5 = 0.25。
            Assert.AreEqual(0.25f, StrongpointRules.MutualSupport(2, 0.5f), 1e-4f);
            // 孤立（隣接0）→ 0。
            Assert.AreEqual(0.0f, StrongpointRules.MutualSupport(0, 1f), 1e-4f);
        }

        [Test]
        public void DelayingAction_EvenZeroStrengthPositionDelaysViaDepth()
        {
            // 防御力満・縦深満 → 突破後もフルに粘る 1.0。
            Assert.AreEqual(1.0f, StrongpointRules.DelayingAction(1f, 1f), 1e-4f);
            // 防御力ゼロでも縦深満 → 基底0.2ぶんは粘る。
            Assert.AreEqual(0.2f, StrongpointRules.DelayingAction(0f, 1f), 1e-4f);
            // 防御0.5・縦深0.5 → core=0.25, +0.2*0.5*0.75=0.075 → 0.325。
            Assert.AreEqual(0.325f, StrongpointRules.DelayingAction(0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void CounterAttackReserve_NeedsBothTroopsAndMobilization()
        {
            // 予備満・即応満 → 最大の逆襲 0.75。
            Assert.AreEqual(0.75f, StrongpointRules.CounterAttackReserve(1f, 1f), 1e-4f);
            // どちらも無し → 逆襲できない 0。
            Assert.AreEqual(0.0f, StrongpointRules.CounterAttackReserve(0f, 0f), 1e-4f);
            // 予備はあるが即応ゼロ（動けない）→ 0.25 止まり。
            Assert.AreEqual(0.25f, StrongpointRules.CounterAttackReserve(1f, 0f), 1e-4f);
            // 即応はあるが予備ゼロ（撃つ兵が無い）→ 同じく 0.25。
            Assert.AreEqual(0.25f, StrongpointRules.CounterAttackReserve(0f, 1f), 1e-4f);
        }

        [Test]
        public void IsolationRisk_RisesWhenSupportCutAndEnemyInfiltrates()
        {
            // 相互支援ゼロ・敵浸透満 → 後背を断たれ孤立リスク満 1.0。
            Assert.AreEqual(1.0f, StrongpointRules.IsolationRisk(0f, 1f), 1e-4f);
            // 相互支援満 → 浸透されても孤立しない 0。
            Assert.AreEqual(0.0f, StrongpointRules.IsolationRisk(1f, 1f), 1e-4f);
            // 相互支援0.5・浸透0.8 → (1-0.5)*0.8 = 0.4。
            Assert.AreEqual(0.4f, StrongpointRules.IsolationRisk(0.5f, 0.8f), 1e-4f);
        }

        [Test]
        public void DefensiveElasticity_BlendsDepthAndCounterAttack()
        {
            // 縦深満・逆襲満 → 弾力満 1.0。
            Assert.AreEqual(1.0f, StrongpointRules.DefensiveElasticity(1f, 1f), 1e-4f);
            // 縦深だけ満・逆襲ゼロ → 半分 0.5（戻す力が無い）。
            Assert.AreEqual(0.5f, StrongpointRules.DefensiveElasticity(1f, 0f), 1e-4f);
            // 縦深0.6・逆襲0.4 → (0.5*0.6+0.5*0.4) = 0.5。
            Assert.AreEqual(0.5f, StrongpointRules.DefensiveElasticity(0.6f, 0.4f), 1e-4f);
        }

        [Test]
        public void IsPositionHeld_HoldsUnlessIsolationOvercomesStrength()
        {
            // 固い陣地・孤立リスク低 → 0.8 >= 0.3 で保持。
            Assert.IsTrue(StrongpointRules.IsPositionHeld(1f, 0.2f));
            // 防御0.5・孤立0.3 → 実効0.2 < 0.3 で陥落。
            Assert.IsFalse(StrongpointRules.IsPositionHeld(0.5f, 0.3f));
            // いくら固く（1.0）ても後背を断たれ（0.7）れば実効0.3 でぎりぎり保持（境界＝以上）。
            Assert.IsTrue(StrongpointRules.IsPositionHeld(1f, 0.7f));
        }

        [Test]
        public void ClampsHandleOutOfRangeAndNegativeCounts()
        {
            // 入力は全て安全域へ Clamp（負・過大でも破綻しない）。
            Assert.AreEqual(1.5f, StrongpointRules.PositionStrength(5f, 5f, 5f), 1e-4f); // 全て上振れ→上限地形×満守備
            Assert.AreEqual(0.0f, StrongpointRules.DefenseInDepth(-3, 1f), 1e-4f);        // 負の線数は0
            Assert.AreEqual(0.0f, StrongpointRules.MutualSupport(-2, 1f), 1e-4f);         // 負の隣接は0
            Assert.AreEqual(1.0f, StrongpointRules.IsolationRisk(-1f, 2f), 1e-4f);        // (1-clamp01(-1))*clamp01(2)=1*1
        }

        [Test]
        public void Narrative_DefenseInDepthHoldsWhereThinLineFalls()
        {
            // 物語：要塞宙域の二様の守り。
            // 〈薄い一線陣地〉＝築城は固いが守備は薄く、縦深なし・相互支援なしで後背へ浸透すると孤立して陥ちる。
            float thinStrength = StrongpointRules.PositionStrength(1f, 0.5f, 0.3f); // 薄い守備の単独陣地
            float thinSupport = StrongpointRules.MutualSupport(0, 1f);          // 孤立＝相互支援0
            float thinIsolation = StrongpointRules.IsolationRisk(thinSupport, 1f); // 浸透満で孤立1.0
            Assert.IsFalse(StrongpointRules.IsPositionHeld(thinStrength, thinIsolation),
                "孤立した単独陣地は固くても後背を断たれて陥落する");

            // 〈縦深陣地群〉＝個々は控えめでも複数線＋相互支援で浸透を防ぎ、予備の逆襲で押し戻す。
            float depth = StrongpointRules.DefenseInDepth(3, 1f);          // 0.75
            float support = StrongpointRules.MutualSupport(3, 1f);         // 0.75
            float netStrength = StrongpointRules.PositionStrength(0.6f, 0.8f, 0.7f); // 控えめな各陣地
            float netIsolation = StrongpointRules.IsolationRisk(support, 1f);        // 0.25（支援で浸透を阻む）
            Assert.IsTrue(StrongpointRules.IsPositionHeld(netStrength, netIsolation),
                "縦深と相互支援が浸透を阻めば各陣地が控えめでも保持できる");

            // 弾力性：縦深＋逆襲予備で押し込まれても戻す。
            float counter = StrongpointRules.CounterAttackReserve(1f, 1f);     // 0.75
            float elasticity = StrongpointRules.DefensiveElasticity(depth, counter);
            float delay = StrongpointRules.DelayingAction(netStrength, depth);
            Assert.Greater(elasticity, 0.5f, "縦深と逆襲を備えた防御は弾力的");
            Assert.Greater(delay, 0f, "突破されても遅滞で時間を稼ぐ");
            Assert.Less(netIsolation, thinIsolation, "相互支援が孤立化を抑える");
        }
    }
}
