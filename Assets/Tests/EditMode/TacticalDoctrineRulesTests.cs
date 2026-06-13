using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// TacticalDoctrineRules：戦術ドクトリン統合窓口のテスト。
    /// AmbushRules/VeterancyRules/ReconRules を単一倍率セットに束ねる挙動を固定する。
    /// </summary>
    public class TacticalDoctrineRulesTests
    {
        // ─── Evaluate：基本動作 ──────────────────────────────────────────

        [Test]
        public void Evaluate_Neutral_ReturnsNearOne()
        {
            var result = TacticalDoctrineRules.Evaluate(TacticalContext.Neutral);
            Assert.AreEqual(1f, result.attackMultiplier, 1e-4f,  "中立→攻撃倍率1.0");
            Assert.AreEqual(1f, result.defenseMultiplier, 1e-4f, "中立→防御倍率1.0");
            Assert.AreEqual(1f, result.reconConfidence, 1e-4f,   "完全情報→確信度1.0");
        }

        [Test]
        public void Evaluate_Veteran_HigherThanRookie()
        {
            // 古参（xp=60）は新兵（xp=0）より攻撃・防御ともに高い
            var rookie = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, false, 0f));
            var elite = TacticalDoctrineRules.Evaluate(new TacticalContext(60f, 1f, false, false, 0f));
            Assert.Greater(elite.attackMultiplier, rookie.attackMultiplier,  "古参>新兵（攻撃）");
            Assert.Greater(elite.defenseMultiplier, rookie.defenseMultiplier, "古参>新兵（防御）");
        }

        [Test]
        public void Evaluate_SprungAmbush_BoostsAttack()
        {
            // 伏兵仕掛け中→攻撃倍率が通常より高い
            var normal = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, false, 0f));
            var ambush = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, true,  false, 0f));
            Assert.Greater(ambush.attackMultiplier, normal.attackMultiplier, "伏兵→攻撃倍率↑");
        }

        [Test]
        public void Evaluate_AmbushVictim_AtTime0_DebuffsDefense()
        {
            // 奇襲直後（time=0）→防御倍率が通常より低い
            var normal = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, false, 0f));
            var victim = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, true,  0f));
            Assert.Less(victim.defenseMultiplier, normal.defenseMultiplier, "被奇襲直後→防御倍率↓");
        }

        [Test]
        public void Evaluate_AmbushVictim_RecoverOverTime()
        {
            // 時間が経つほど防御倍率が回復する（timeSinceAmbush が大きいほど高い）
            var early = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, true, 0f));
            var later = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, true, 9999f));
            Assert.Greater(later.defenseMultiplier, early.defenseMultiplier, "奇襲から時間が経つほど回復");
        }

        [Test]
        public void Evaluate_LowRecon_LowConfidence()
        {
            // 偵察ゼロ→確信度ゼロ（霧の中）
            var foggy = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 0f, false, false, 0f));
            Assert.AreEqual(0f, foggy.reconConfidence, 1e-4f, "偵察なし→確信度0");
        }

        [Test]
        public void Evaluate_BoundsRespected()
        {
            // 極端入力でも MinMultiplier..MaxMultiplier に収まる
            var extreme = TacticalDoctrineRules.Evaluate(new TacticalContext(9999f, 1f, true, false, 0f));
            Assert.LessOrEqual(extreme.attackMultiplier, TacticalDoctrineRules.MaxMultiplier + 1e-4f);
            Assert.GreaterOrEqual(extreme.attackMultiplier, TacticalDoctrineRules.MinMultiplier - 1e-4f);
        }

        [Test]
        public void Evaluate_AmbushVictimFullyRecovered_EqualToNormal()
        {
            // 長時間経過で完全回復→非被奇襲と同値
            var normal = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, false, 0f));
            var recovered = TacticalDoctrineRules.Evaluate(new TacticalContext(0f, 1f, false, true, 9999f));
            Assert.AreEqual(normal.defenseMultiplier, recovered.defenseMultiplier, 1e-4f,
                "完全回復後→非被奇襲と同値");
        }

        // ─── ShouldAmbush ────────────────────────────────────────────────

        [Test]
        public void ShouldAmbush_HighConcealment_LowAlertness_HighSkill_LowRoll_ReturnsTrue()
        {
            // 隠蔽高・警戒低・AI高スキル・低ロール → 伏兵成立
            Assert.IsTrue(TacticalDoctrineRules.ShouldAmbush(0.9f, 0.1f, 1.0f, 0.0f));
        }

        [Test]
        public void ShouldAmbush_NoConcealment_ReturnsFalse()
        {
            // 隠蔽なし → 確率0 → 常に false
            Assert.IsFalse(TacticalDoctrineRules.ShouldAmbush(0f, 0f, 1f, 0f));
        }

        [Test]
        public void ShouldAmbush_ZeroSkill_ReturnsFalse()
        {
            // AI スキル0 → chance が0へ → 常に false
            Assert.IsFalse(TacticalDoctrineRules.ShouldAmbush(1f, 0f, 0f, 0.0f));
        }
    }
}
