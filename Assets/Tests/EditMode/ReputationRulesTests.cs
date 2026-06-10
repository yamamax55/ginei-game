using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 名声システムを固定する：勝利で名声が上がり格上撃破ほど跳ね、敗北で削れる。名声は士気/徴募/敵威圧へ線形に波及し、
    /// 平時は無名へ減衰する。0..1 クランプと格上ボーナスの境界を担保。
    /// </summary>
    public class ReputationRulesTests
    {
        private static readonly ReputationParams P = ReputationParams.Default;

        [Test]
        public void RenownAfterBattle_WinGains_LossErodes()
        {
            // 互角(ratio=1)で勝てば基礎増分0.1
            Assert.AreEqual(0.6f, ReputationRules.RenownAfterBattle(0.5f, true, 1f, ReputationParams.Default), 1e-5f);
            // 負ければ0.08削れる
            Assert.AreEqual(0.42f, ReputationRules.RenownAfterBattle(0.5f, false, 1f, ReputationParams.Default), 1e-5f);
        }

        [Test]
        public void RenownAfterBattle_UpsetGivesMore()
        {
            // 格上(ratio=2)を破る＝基礎0.1×(1+ (2-1)*1.0)=0.2
            Assert.AreEqual(0.3f, ReputationRules.RenownAfterBattle(0.1f, true, 2f, ReputationParams.Default), 1e-5f);
            // 格下(ratio=0.5)に勝っても上乗せ無し＝基礎0.1のみ
            Assert.AreEqual(0.2f, ReputationRules.RenownAfterBattle(0.1f, true, 0.5f, ReputationParams.Default), 1e-5f);
        }

        [Test]
        public void RenownAfterBattle_ClampedToUnit()
        {
            Assert.AreEqual(1f, ReputationRules.RenownAfterBattle(0.95f, true, 5f, ReputationParams.Default), 1e-5f);
            Assert.AreEqual(0f, ReputationRules.RenownAfterBattle(0.02f, false, 1f, ReputationParams.Default), 1e-5f);
        }

        [Test]
        public void Bonuses_ScaleLinearlyWithRenown()
        {
            Assert.AreEqual(0.2f, ReputationRules.MoraleBonus(1f), 1e-5f);     // moraleScale=0.2
            Assert.AreEqual(0.15f, ReputationRules.RecruitmentBonus(0.5f), 1e-5f); // recruitScale=0.3×0.5
            Assert.AreEqual(0.2f, ReputationRules.IntimidationFactor(1f), 1e-5f);  // intimidationScale=0.2
            Assert.AreEqual(0f, ReputationRules.MoraleBonus(0f), 1e-5f);
        }

        [Test]
        public void Decay_MovesTowardBaseline()
        {
            // 減衰率0.02/dt、dt=1 で 0.5→0.48
            Assert.AreEqual(0.48f, ReputationRules.Decay(0.5f, 1f), 1e-5f);
            // baseline を超えて下回らない（MoveTowards）
            Assert.AreEqual(0.1f, ReputationRules.Decay(0.105f, 1f, ReputationParams.Default, 0.1f), 1e-5f);
        }

        [Test]
        public void Reputation_CountersAndBattles()
        {
            var rep = new Reputation(0.4f, 3, 2);
            Assert.AreEqual(5, rep.Battles);
            Assert.AreEqual(0.4f, rep.renown, 1e-5f);
            // クランプ＆非負
            var clamped = new Reputation(2f, -1, -3);
            Assert.AreEqual(1f, clamped.renown, 1e-5f);
            Assert.AreEqual(0, clamped.victories);
            Assert.AreEqual(0, clamped.defeats);
        }
    }
}
