using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// BattleMetaRules（#2260 会戦結果のメタ反映）の EditMode テスト。
    /// 経験値算定・撃墜時の運命判定（roll 注入で決定論）を担保する。
    /// </summary>
    public class BattleMetaRulesTests
    {
        // ───────────────────────────────────────────
        // ExperienceFromBattle
        // ───────────────────────────────────────────

        [Test]
        public void ExperienceFromBattle_ZeroDamageZeroKills_ReturnsZero()
        {
            float exp = BattleMetaRules.ExperienceFromBattle(0, 0, isWinner: true);
            Assert.AreEqual(0f, exp, 1e-4f, "戦果ゼロは経験0");
        }

        [Test]
        public void ExperienceFromBattle_DamageScalesByThousand()
        {
            // 与ダメ 1000 → ExperiencePerThousandDamage * WinnerExperienceMult
            float exp = BattleMetaRules.ExperienceFromBattle(1000, 0, isWinner: true);
            float expected = BattleMetaRules.ExperiencePerThousandDamage * BattleMetaRules.WinnerExperienceMult;
            Assert.AreEqual(expected, exp, 1e-4f, "与ダメ1000の経験量が正しい");
        }

        [Test]
        public void ExperienceFromBattle_KillAddsExperience()
        {
            // 与ダメ0・撃墜1 → ExperiencePerKill * LoserExperienceMult
            float exp = BattleMetaRules.ExperienceFromBattle(0, 1, isWinner: false);
            float expected = BattleMetaRules.ExperiencePerKill * BattleMetaRules.LoserExperienceMult;
            Assert.AreEqual(expected, exp, 1e-4f, "撃墜数1の経験量が正しい");
        }

        [Test]
        public void ExperienceFromBattle_WinnerGetsMoreThanLoser()
        {
            // 同じ戦果でも勝者は多く成長する。
            float winnerExp = BattleMetaRules.ExperienceFromBattle(5000, 3, isWinner: true);
            float loserExp  = BattleMetaRules.ExperienceFromBattle(5000, 3, isWinner: false);
            Assert.Greater(winnerExp, loserExp, "勝者側の経験量が多い");
        }

        [Test]
        public void ExperienceFromBattle_NegativeInputsClamped()
        {
            // 負の与ダメ・撃墜数は 0 扱い。
            float exp = BattleMetaRules.ExperienceFromBattle(-9999, -5, isWinner: true);
            Assert.AreEqual(0f, exp, 1e-4f, "負の入力は0扱い");
        }

        // ───────────────────────────────────────────
        // ResolveCommanderFate
        // ───────────────────────────────────────────

        [Test]
        public void ResolveCommanderFate_LowRoll_BecomeCaptive()
        {
            // roll が非常に小さい → CaptivityRules.IsCaptured が true → 捕虜。
            // CaptureChance(encircled=false, cmd=0.5, morale=0.5) ≈ 0.075。
            // roll=0.01 < 0.075 → 捕虜。
            var fate = BattleMetaRules.ResolveCommanderFate(0.5f, 0.5f, roll: 0.01f);
            Assert.AreEqual(CommanderFate.捕虜, fate, "低roll→捕虜");
        }

        [Test]
        public void ResolveCommanderFate_MidRoll_NoCapture_ThenKIAOrEscape()
        {
            // roll が捕虜閾値を超えると、捕虜にならない。
            // 1-roll が KIAThreshold(0.25) 未満 → 戦死、以上 → 離脱。
            // 捕虜判定を確実に外すため cmd=1.0, morale=1.0 → CaptureChance ≒ 0。
            // roll=0.8 → 1-roll=0.2 < 0.25 → 戦死。
            var fate = BattleMetaRules.ResolveCommanderFate(1.0f, 1.0f, roll: 0.8f);
            Assert.AreEqual(CommanderFate.戦死, fate, "1-roll<KIAThreshold→戦死");
        }

        [Test]
        public void ResolveCommanderFate_HighCommandSkill_LowCaptureChance()
        {
            // 指揮・士気が高いほど捕虜になりにくい（CaptivityRules の性質を通してテスト）。
            float highCmd = CaptivityRules.CaptureChance(BattleMetaRules.DefaultEncircled, 0.9f, 0.9f);
            float lowCmd  = CaptivityRules.CaptureChance(BattleMetaRules.DefaultEncircled, 0.1f, 0.1f);
            Assert.Less(highCmd, lowCmd, "能力が高いほど捕虜になりにくい");
        }

        [Test]
        public void ResolveCommanderFate_Escape()
        {
            // 捕虜判定を外し（cmd=1.0, morale=1.0, roll大）、かつ1-roll≥KIAThreshold → 離脱。
            // roll=0.5 → 1-roll=0.5 ≥ 0.25 → 離脱。
            var fate = BattleMetaRules.ResolveCommanderFate(1.0f, 1.0f, roll: 0.5f);
            Assert.AreEqual(CommanderFate.離脱, fate, "残り→離脱");
        }

        [Test]
        public void ResolveCommanderFate_Deterministic()
        {
            // 同じ roll なら必ず同じ結果（決定論）。
            var a = BattleMetaRules.ResolveCommanderFate(0.5f, 0.5f, roll: 0.5f);
            var b = BattleMetaRules.ResolveCommanderFate(0.5f, 0.5f, roll: 0.5f);
            Assert.AreEqual(a, b, "決定論＝同roll→同結果");
        }

        // ───────────────────────────────────────────
        // CommandFactorFromStat
        // ───────────────────────────────────────────

        [Test]
        public void CommandFactorFromStat_ConvertsToZeroOne()
        {
            Assert.AreEqual(0f, BattleMetaRules.CommandFactorFromStat(0f), 1e-4f);
            Assert.AreEqual(0.5f, BattleMetaRules.CommandFactorFromStat(50f), 1e-4f);
            Assert.AreEqual(1f, BattleMetaRules.CommandFactorFromStat(100f), 1e-4f);
        }

        [Test]
        public void CommandFactorFromStat_ClampedOutOfRange()
        {
            Assert.AreEqual(0f, BattleMetaRules.CommandFactorFromStat(-50f), 1e-4f, "負はクランプ");
            Assert.AreEqual(1f, BattleMetaRules.CommandFactorFromStat(200f), 1e-4f, "200以上はクランプ");
        }

        // ───────────────────────────────────────────
        // GrowthRules との結合（ExperienceFromBattle → GainExperience → EffectiveStatBonus）
        // ───────────────────────────────────────────

        [Test]
        public void Integration_BattleExperience_IncreasesEffectiveStat()
        {
            // 与ダメ5000・撃墜2・勝者 の経験を GrowthRules に渡すと実効ボーナスが増える。
            var growth = new Growth(GrowthArchetype.叩き上げ, 0f);
            int baseStat = 60;

            int bonusBefore = GrowthRules.EffectiveStatBonus(growth, baseStat);

            float amount = BattleMetaRules.ExperienceFromBattle(5000, 2, isWinner: true);
            GrowthRules.GainExperience(growth, amount, dt: 1f);

            int bonusAfter = GrowthRules.EffectiveStatBonus(growth, baseStat);

            Assert.Greater(bonusAfter, bonusBefore, "会戦経験で実効能力ボーナスが増える");
        }
    }
}
