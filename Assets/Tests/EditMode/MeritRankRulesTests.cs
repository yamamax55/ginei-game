using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍功授爵制＝始皇帝モデル（#900-905・QIN）を固定する：戦功→爵位ラダー・授爵見込み士気(QIN-2)・
    /// 制度畏怖(QIN-3)・収奪の短期最強長期崩壊(QIN-5)・授爵判定。決定論で境界・クランプ・各分岐を担保。
    /// </summary>
    public class MeritRankRulesTests
    {
        private static readonly MeritParams P = MeritParams.Default; // 1級10pt / 上限20 / 士気上限0.3 / 飽和100 / 畏怖0.25 / peak0.4 / decay20

        // ===== MeritToTier：ラダー写像＋クランプ =====
        [Test]
        public void MeritToTier_MapsByLadder()
        {
            Assert.AreEqual(3, MeritRankRules.MeritToTier(35f, P)); // 35/10=3.5→3級
            Assert.AreEqual(5, MeritRankRules.MeritToTier(50f, P)); // ちょうど5級
        }

        [Test]
        public void MeritToTier_NegativeOrZero_IsZero()
        {
            Assert.AreEqual(0, MeritRankRules.MeritToTier(-100f, P)); // 負は0級（境界）
            Assert.AreEqual(0, MeritRankRules.MeritToTier(0f, P));
        }

        [Test]
        public void MeritToTier_ClampsToMaxTier()
        {
            Assert.AreEqual(P.maxTier, MeritRankRules.MeritToTier(9999f, P)); // 上限20級でクランプ
        }

        // ===== IncentiveMoraleBonus：飽和とクランプ（QIN-2）=====
        [Test]
        public void IncentiveMoraleBonus_ProportionalThenSaturates()
        {
            Assert.AreEqual(0.15f, MeritRankRules.IncentiveMoraleBonus(50f, P), 1e-4f); // 50/100*0.3
            Assert.AreEqual(P.maxMoraleBonus, MeritRankRules.IncentiveMoraleBonus(100f, P), 1e-4f); // 飽和点で上限
            Assert.AreEqual(P.maxMoraleBonus, MeritRankRules.IncentiveMoraleBonus(500f, P), 1e-4f); // 超過は頭打ち（クランプ）
        }

        [Test]
        public void IncentiveMoraleBonus_NonPositive_IsZero()
        {
            Assert.AreEqual(0f, MeritRankRules.IncentiveMoraleBonus(0f, P), 1e-6f);
            Assert.AreEqual(0f, MeritRankRules.IncentiveMoraleBonus(-10f, P), 1e-6f);
        }

        // ===== InstitutionalAwe：比例とクランプ（QIN-3）=====
        [Test]
        public void InstitutionalAwe_ProportionalAndClamped()
        {
            Assert.AreEqual(0.125f, MeritRankRules.InstitutionalAwe(0.5f, P), 1e-4f); // 0.5*0.25
            Assert.AreEqual(P.maxAwe, MeritRankRules.InstitutionalAwe(1f, P), 1e-4f); // 上限
            Assert.AreEqual(P.maxAwe, MeritRankRules.InstitutionalAwe(2f, P), 1e-4f); // 1超はクランプ
            Assert.AreEqual(0f, MeritRankRules.InstitutionalAwe(-1f, P), 1e-6f); // 0未満はクランプ（境界）
        }

        // ===== ExtractiveDecay：短期最強・長期崩壊（QIN-5）=====
        [Test]
        public void ExtractiveDecay_ShortTerm_IsStrongest()
        {
            // t=0：decay=1 → s + peak（短期は安定度に出力ボーナスが乗る）
            Assert.AreEqual(0.9f, MeritRankRules.ExtractiveDecay(0.5f, 0f, P), 1e-4f);
        }

        [Test]
        public void ExtractiveDecay_LongTerm_Collapses()
        {
            float shortRun = MeritRankRules.ExtractiveDecay(0.5f, 0f, P);
            float longRun = MeritRankRules.ExtractiveDecay(0.5f, 200f, P);
            Assert.Less(longRun, shortRun);          // 長期は短期より低い（磨耗）
            Assert.Less(longRun, 0.5f);              // 基準安定度を割り込む（収奪の崩壊）
            Assert.GreaterOrEqual(longRun, 0f);      // 0未満にはならない（クランプ）
        }

        // ===== AwardRank：昇爵判定と上限ガード =====
        [Test]
        public void AwardRank_PromotesWhenMeritExceedsCurrent()
        {
            Assert.IsTrue(MeritRankRules.AwardRank(35f, 2, P));  // 3級到達＞現2級
            Assert.IsFalse(MeritRankRules.AwardRank(35f, 3, P)); // 同級は昇爵せず（境界）
            Assert.IsFalse(MeritRankRules.AwardRank(35f, 5, P)); // 現級が上回れば昇爵せず
        }

        [Test]
        public void AwardRank_AtMaxTier_NeverPromotes()
        {
            Assert.IsFalse(MeritRankRules.AwardRank(9999f, P.maxTier, P)); // 上限到達は据え置き（クランプ）
        }
    }
}
