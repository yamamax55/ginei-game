using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 個人武勲→昇進の出世回路（TKO-2・<see cref="MeritRecordRules"/>・#2479）を固定する：功績点の重み・累積、
    /// 功績段数、年功ベースへの上乗せ（<see cref="RankSystem.NextRankTier"/> 経由）と最高位での頭打ち、
    /// 功績ゼロの後方互換（年功のみ）、実力スコアの飽和、段階適用。爵位（<see cref="MeritRankRules"/>）とは別軸。
    /// </summary>
    public class MeritRecordRulesTests
    {
        private static MeritRecordRules.MeritRecordParams P => MeritRecordRules.MeritRecordParams.Default;

        // 准将5/少将6/中将7/大将8/元帥10（9欠番）の階級表。
        private static FactionData Faction()
        {
            var f = ScriptableObject.CreateInstance<FactionData>();
            f.ranks = new List<FactionData.RankEntry>
            {
                new FactionData.RankEntry(7, "准将"),
                new FactionData.RankEntry(8, "少将"),
                new FactionData.RankEntry(9, "中将"),
                new FactionData.RankEntry(10, "大将"),
                new FactionData.RankEntry(12, "元帥"),
            };
            return f;
        }

        [Test]
        public void PointsFor_WeightsByKindAndMagnitude()
        {
            Assert.AreEqual(10f, MeritRecordRules.PointsFor(ExploitKind.旗艦撃破, 1f, P), 1e-4);
            Assert.AreEqual(5f, MeritRecordRules.PointsFor(ExploitKind.撃沈, 5f, P), 1e-4);
            Assert.AreEqual(8f, MeritRecordRules.PointsFor(ExploitKind.防衛達成, 1f, P), 1e-4);
            Assert.AreEqual(0f, MeritRecordRules.PointsFor(ExploitKind.撃沈, -3f, P), 1e-4, "負の規模は0");
        }

        [Test]
        public void Record_AccumulatesPointsAndCount()
        {
            var rec = new MeritRecord(1);
            MeritRecordRules.Record(rec, ExploitKind.旗艦撃破, 1f, P); // +10
            MeritRecordRules.Record(rec, ExploitKind.撃沈, 30f, P);   // +30
            MeritRecordRules.Record(rec, ExploitKind.建白採用, 1f, P); // +4
            Assert.AreEqual(44f, rec.points, 1e-4);
            Assert.AreEqual(3, rec.exploitCount);
        }

        [Test]
        public void EarnedPromotionGrades_FloorsByPointsPerPromotion()
        {
            var rec = new MeritRecord(1) { points = 120f }; // /50 = 2
            Assert.AreEqual(2, MeritRecordRules.EarnedPromotionGrades(rec, P));
            rec.points = 49f;
            Assert.AreEqual(0, MeritRecordRules.EarnedPromotionGrades(rec, P));
        }

        [Test]
        public void PromotedTier_AddsMeritGradesOntoSeniorityBaseline()
        {
            var f = Faction();
            var rec = new MeritRecord(1) { points = 120f }; // 2段
            // 准将(7) から 2段 → 少将(8) → 中将(9)
            Assert.AreEqual(9, MeritRecordRules.PromotedTier(f, 7, rec, P));
        }

        [Test]
        public void PromotedTier_ZeroMerit_KeepsSeniorityBaseline_BackwardCompat()
        {
            var f = Faction();
            var rec = new MeritRecord(1); // 0点
            Assert.AreEqual(8, MeritRecordRules.PromotedTier(f, 8, rec, P), "功績0は年功ベースのまま");
        }

        [Test]
        public void PromotedTier_CapsAtHighestRank()
        {
            var f = Faction();
            var rec = new MeritRecord(1) { points = 1000f }; // 20段ぶん
            // 大将(10) から上は元帥(12) のみ＝1段で頭打ち
            Assert.AreEqual(12, MeritRecordRules.PromotedTier(f, 10, rec, P));
            // 既に元帥(12) なら据え置き
            Assert.AreEqual(12, MeritRecordRules.PromotedTier(f, 12, rec, P));
        }

        [Test]
        public void MeritScore01_Saturates()
        {
            Assert.AreEqual(0.5f, MeritRecordRules.MeritScore01(new MeritRecord(1) { points = 100f }, P), 1e-4);
            Assert.AreEqual(1f, MeritRecordRules.MeritScore01(new MeritRecord(1) { points = 200f }, P), 1e-4);
            Assert.AreEqual(1f, MeritRecordRules.MeritScore01(new MeritRecord(1) { points = 999f }, P), 1e-4, "上限クランプ");
        }

        [Test]
        public void TryApplyPromotion_StepwiseUntilNoPending()
        {
            var f = Faction();
            var rec = new MeritRecord(1) { points = 120f }; // 2段
            int tier = 7;

            Assert.IsTrue(MeritRecordRules.TryApplyPromotion(f, tier, rec, P, out tier));
            Assert.AreEqual(8, tier);
            Assert.AreEqual(1, rec.meritPromotionsApplied);
            Assert.IsTrue(MeritRecordRules.HasPendingPromotion(rec, P));

            Assert.IsTrue(MeritRecordRules.TryApplyPromotion(f, tier, rec, P, out tier));
            Assert.AreEqual(9, tier);
            Assert.AreEqual(2, rec.meritPromotionsApplied);
            Assert.IsFalse(MeritRecordRules.HasPendingPromotion(rec, P));

            Assert.IsFalse(MeritRecordRules.TryApplyPromotion(f, tier, rec, P, out tier), "残段なしは昇進しない");
            Assert.AreEqual(9, tier);
        }
    }
}
