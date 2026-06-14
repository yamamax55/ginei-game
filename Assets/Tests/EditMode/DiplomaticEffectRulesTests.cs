using NUnit.Framework;
using Ginei;
using DS = Ginei.DiplomacyState;

namespace Ginei.Tests
{
    /// <summary>DiplomaticEffectRules（条約効果・制裁・援助・債務外交の合成オーケストレータ）の EditMode テスト。</summary>
    public class DiplomaticEffectRulesTests
    {
        private const float Eps = 1e-4f;

        // ─── 制裁 → 経済打撃 ───────────────────────────────────────────

        [Test]
        public void SanctionEconomicHit_制裁で経済が削られる()
        {
            // 抜け穴なし・全強度＝最大ペナルティ40%。経済1000 → 400 の打撃。
            float hit = DiplomaticEffectRules.SanctionEconomicHit(1000f, 1f);
            float expected = 1000f * SanctionsRules.OutputPenalty(1f, 0f);
            Assert.AreEqual(expected, hit, Eps);
            Assert.Greater(hit, 0f);
        }

        [Test]
        public void SanctionEconomicHit_抜け穴全開なら打撃ゼロ()
        {
            float hit = DiplomaticEffectRules.SanctionEconomicHit(1000f, 1f, 1f, SanctionsParams.Default);
            Assert.AreEqual(0f, hit, Eps);
        }

        [Test]
        public void SanctionEconomicHit_経済が負でも打撃は非負()
        {
            float hit = DiplomaticEffectRules.SanctionEconomicHit(-500f, 1f);
            Assert.AreEqual(0f, hit, Eps);
        }

        // ─── 援助 → opinion ───────────────────────────────────────────

        [Test]
        public void AidOpinionGain_援助でopinionが上がる()
        {
            float gain = DiplomaticEffectRules.AidOpinionGain(100f);
            Assert.AreEqual(ForeignAidRules.OpinionGain(100f, 0f), gain, Eps);
            Assert.Greater(gain, 0f);
        }

        [Test]
        public void AidOpinionGain_困窮ほど効く_雪中の炭()
        {
            float peacetime = DiplomaticEffectRules.AidOpinionGain(100f, 0f);
            float inNeed = DiplomaticEffectRules.AidOpinionGain(100f, 1f);
            Assert.Greater(inNeed, peacetime);
        }

        // ─── 債務 → 圧力 ─────────────────────────────────────────────

        [Test]
        public void DebtLeverage_返せない借金は圧力になる()
        {
            // 既定＝閾値比率0.5・飽和2.0。比率3（>飽和）でレバレッジ最大。
            float lev = DiplomaticEffectRules.DebtLeverage(300f, 100f);
            Assert.AreEqual(DebtDiplomacyRules.DebtLeverage(300f, 100f), lev, Eps);
            Assert.AreEqual(1f, lev, Eps);
        }

        [Test]
        public void DebtLeverage_返せる借金は力にならない()
        {
            // 比率0.2（<閾値0.5）でレバレッジ0。
            float lev = DiplomaticEffectRules.DebtLeverage(20f, 100f);
            Assert.AreEqual(0f, lev, Eps);
        }

        // ─── 条約効果 → opinion ───────────────────────────────────────

        [Test]
        public void TreatyOpinionDelta_同盟は最大の親密効果()
        {
            float alliance = DiplomaticEffectRules.TreatyOpinionDelta(TreatyType.同盟);
            float passage = DiplomaticEffectRules.TreatyOpinionDelta(TreatyType.通行);
            Assert.AreEqual(TreatyRules.OpinionEffect(TreatyType.同盟, TreatyRules.TreatyParams.Default), alliance, Eps);
            Assert.Greater(alliance, passage);
        }

        [Test]
        public void StatusOpinionDelta_外交状態から条約効果を引く()
        {
            float delta = DiplomaticEffectRules.StatusOpinionDelta(DS.DiplomaticStatus.同盟);
            Assert.AreEqual(TreatyRules.OpinionEffect(TreatyType.同盟, TreatyRules.TreatyParams.Default), delta, Eps);
        }

        [Test]
        public void StatusOpinionDelta_平時や交戦は条約効果なし()
        {
            Assert.AreEqual(0f, DiplomaticEffectRules.StatusOpinionDelta(DS.DiplomaticStatus.平時), Eps);
            Assert.AreEqual(0f, DiplomaticEffectRules.StatusOpinionDelta(DS.DiplomaticStatus.交戦), Eps);
        }

        // ─── 状態の読み取り専用（非破壊） ─────────────────────────────

        [Test]
        public void StatusOpinionDelta_状態を読むだけで変えない()
        {
            var state = new DiplomacyState();
            DiplomacyRules.SignTreaty(state, "帝国", "同盟", DS.DiplomaticStatus.同盟);
            var before = state.Status("帝国", "同盟");
            DiplomaticEffectRules.StatusOpinionDelta(state.Status("帝国", "同盟"));
            Assert.AreEqual(before, state.Status("帝国", "同盟"));
        }
    }
}
