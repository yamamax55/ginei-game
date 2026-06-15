using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 階級ピラミッドの年次フロー（TKO-13 Game配線・<see cref="RankFlowRules"/>/<see cref="MilitaryRankRegistry"/>・#2490）と
    /// 昇進の定員ゲート（<see cref="MonthlyCouncilRules"/> の canPromoteTo・<see cref="RankDistributionRules.GradeForOfficerTier"/>）を固定する。
    /// </summary>
    public class RankFlowRulesTests
    {
        private static RankDistributionRules.PyramidParams PY => RankDistributionRules.PyramidParams.Default;
        private static RankFlowRules.FlowParams FL => RankFlowRules.FlowParams.Default;

        [Test]
        public void Registry_GetCreatesPerFaction_AndClear()
        {
            MilitaryRankRegistry.Clear();
            var a = MilitaryRankRegistry.Get(Faction.帝国);
            a.Set(MilitaryGrade.二等兵, 100);
            Assert.AreSame(a, MilitaryRankRegistry.Get(Faction.帝国), "同一勢力は同一インスタンス");
            Assert.IsTrue(MilitaryRankRegistry.Has(Faction.帝国));
            Assert.IsFalse(MilitaryRankRegistry.Has(Faction.同盟));
            MilitaryRankRegistry.Clear();
            Assert.IsFalse(MilitaryRankRegistry.Has(Faction.帝国));
        }

        [Test]
        public void Recruit_AddsToBottom()
        {
            var d = new RankDistribution();
            RankFlowRules.Recruit(d, 500);
            Assert.AreEqual(500, d.Get(MilitaryGrade.二等兵));
            RankFlowRules.Recruit(d, -10); // 負は無視
            Assert.AreEqual(500, d.Get(MilitaryGrade.二等兵));
        }

        [Test]
        public void Attrition_RemovesFraction()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.二等兵, 1000);
            d.Set(MilitaryGrade.少尉, 100);
            int left = RankFlowRules.Attrition(d, 0.1f);
            Assert.AreEqual(110, left, "1000+100 の10%");
            Assert.AreEqual(900, d.Get(MilitaryGrade.二等兵));
            Assert.AreEqual(90, d.Get(MilitaryGrade.少尉));
        }

        [Test]
        public void PromoteByVacancy_OnlyFillsVacancies()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.二等兵, 100);
            d.Set(MilitaryGrade.一等兵, 0);
            // 一等兵の目標10、二等兵から最大50%上げられるが空きは10まで
            var target = new int[RankDistributionRules.GradeCount];
            target[(int)MilitaryGrade.一等兵] = 10;
            int moved = RankFlowRules.PromoteByVacancy(d, target, 0.5f);
            Assert.AreEqual(10, moved, "空き10までしか上がらない");
            Assert.AreEqual(90, d.Get(MilitaryGrade.二等兵));
            Assert.AreEqual(10, d.Get(MilitaryGrade.一等兵));
        }

        [Test]
        public void PromoteByVacancy_BlockedWhenAboveFull()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.大佐, 50);
            d.Set(MilitaryGrade.准将, 50);
            var target = new int[RankDistributionRules.GradeCount];
            target[(int)MilitaryGrade.准将] = 50; // 准将は満員＝空き0
            int moved = RankFlowRules.PromoteByVacancy(d, target, 0.5f);
            Assert.AreEqual(0, moved, "上が詰まれば昇進しない＝上ほど狭き門");
            Assert.AreEqual(50, d.Get(MilitaryGrade.大佐));
        }

        [Test]
        public void TickYear_MovesTowardPyramid_BaseStaysWidest()
        {
            var d = new RankDistribution();
            d.Set(MilitaryGrade.二等兵, 10000); // 新設軍＝全員最下級
            for (int y = 0; y < 20; y++)
                RankFlowRules.TickYear(d, extraIntake: 1000, PY, FL);

            // 兵卒が依然として最多（ピラミッドの底）
            Assert.Greater(d.Get(MilitaryGrade.二等兵), d.Get(MilitaryGrade.少尉));
            Assert.Greater(d.Get(MilitaryGrade.少尉), d.Get(MilitaryGrade.元帥));
            // 上位にも人が流れている（昇進が機能）
            Assert.Greater(RankDistributionRules.OfficerCount(d), 0);
        }

        [Test]
        public void GradeForOfficerTier_IsInverseOfToOfficerTier()
        {
            for (int t = 1; t <= 10; t++)
                Assert.AreEqual(t, RankDistributionRules.ToOfficerTier(RankDistributionRules.GradeForOfficerTier(t)));
            Assert.AreEqual(MilitaryGrade.少尉, RankDistributionRules.GradeForOfficerTier(1));
            Assert.AreEqual(MilitaryGrade.元帥, RankDistributionRules.GradeForOfficerTier(10));
        }

        [Test]
        public void CouncilGate_BlocksPromotionWhenNoVacancy_KeepsMeritPending()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(5, "准将"), new FactionData.RankEntry(6, "少将"),
            };
            var hero = new Person(2, "主人公", Faction.帝国, PersonRole.軍人) { rankTier = 5 };
            var merit = new MeritRecord(2) { points = 120f }; // 昇進保留あり
            var lord = new Person(1, "君主", Faction.帝国, PersonRole.軍人) { isSovereign = true, rankTier = 10 };

            // 定員ゲート＝どこへも昇進不可
            var outcome = MonthlyCouncilRules.Hold(hero, merit, f, null, lord,
                currentMonth: 1, newMandateId: 9, roll: () => 0.9f,
                meritPrm: MeritRecordRules.MeritRecordParams.Default,
                mandatePrm: SovereignMandateRules.MandateParams.Default,
                councilPrm: MonthlyCouncilRules.CouncilParams.Default,
                relations: null, canPromoteTo: _ => false);

            Assert.IsFalse(outcome.promoted, "定員空きなし＝昇進しない");
            Assert.AreEqual(5, hero.rankTier, "据え置き");
            Assert.AreEqual(0, merit.meritPromotionsApplied, "武勲は消費されず保留");
            Assert.IsTrue(MeritRecordRules.HasPendingPromotion(merit, MeritRecordRules.MeritRecordParams.Default));
        }
    }
}
