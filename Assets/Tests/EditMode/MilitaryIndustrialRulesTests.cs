using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍産複合体＝造船省益による過剰建艦バイアス（#1389・MCN-4）の純ロジック検証。
    /// 既定 Params（調達感度1・誇張感度1・慣性速度0.5・固定化指数2・財政感度1・癒着感度1・バブル閾値0.3）の具体値で期待を固定。
    /// </summary>
    public class MilitaryIndustrialRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>調達バイアス＝産業影響×省益の積。どちらか0なら膨らまず、両者高で必要超の調達が成立。</summary>
        [Test]
        public void ProcurementBias_ProductOfInfluenceAndInterest()
        {
            // 0.8×0.5×1 = 0.4
            Assert.AreEqual(0.4f, MilitaryIndustrialRules.ProcurementBias(0.8f, 0.5f), Eps);
            // 省益が0なら膨らまない
            Assert.AreEqual(0f, MilitaryIndustrialRules.ProcurementBias(0.9f, 0f), Eps);
            // 産業影響が0なら膨らまない
            Assert.AreEqual(0f, MilitaryIndustrialRules.ProcurementBias(0f, 0.9f), Eps);
        }

        /// <summary>過剰建艦圧力＝調達バイアス×(1−軍事的必要)。必要が低いほど過剰が際立つ。</summary>
        [Test]
        public void OverbuildPressure_HigherWhenNeedIsLow()
        {
            // bias0.6・need0 → 0.6
            Assert.AreEqual(0.6f, MilitaryIndustrialRules.OverbuildPressure(0.6f, 0f), Eps);
            // bias0.6・need0.5 → 0.3
            Assert.AreEqual(0.3f, MilitaryIndustrialRules.OverbuildPressure(0.6f, 0.5f), Eps);
            // 必要が満たされていれば過剰でない
            Assert.AreEqual(0f, MilitaryIndustrialRules.OverbuildPressure(0.6f, 1f), Eps);
        }

        /// <summary>脅威の誇張誘因＝産業影響×予算の懸かり×感度。予算が懸かるほど敵を大きく見せる。</summary>
        [Test]
        public void ThreatExaggeration_ScalesWithBudgetStake()
        {
            // 0.7×0.6×1 = 0.42
            Assert.AreEqual(0.42f, MilitaryIndustrialRules.ThreatExaggeration(0.7f, 0.6f), Eps);
            // 予算が懸かっていなければ誇張する理由がない
            Assert.AreEqual(0f, MilitaryIndustrialRules.ThreatExaggeration(0.9f, 0f), Eps);
        }

        /// <summary>平時の建艦慣性＝平時ほど過剰建艦が際立つ目標へ追従（一度動いたラインは止まらない）。</summary>
        [Test]
        public void PeacetimeMomentum_RisesTowardBiasTimesPeace()
        {
            // 目標 = bias0.8×peace1 = 0.8、rate0.5×dt1 で MoveTowards(0,0.8,0.5) = 0.5
            Assert.AreEqual(0.5f, MilitaryIndustrialRules.PeacetimeMomentum(0.8f, 1f, 1f), Eps);
            // dt2 なら目標0.8に届く
            Assert.AreEqual(0.8f, MilitaryIndustrialRules.PeacetimeMomentum(0.8f, 1f, 2f), Eps);
            // 戦時(peace0)なら平時慣性は立たない
            Assert.AreEqual(0f, MilitaryIndustrialRules.PeacetimeMomentum(0.8f, 0f, 1f), Eps);
        }

        /// <summary>過剰造船能力の固定化＝過剰圧力の指数2乗。深い過剰ほど超線形に固定化する。</summary>
        [Test]
        public void CapacityLockIn_SuperlinearInOverbuild()
        {
            // 0.5^2 = 0.25
            Assert.AreEqual(0.25f, MilitaryIndustrialRules.CapacityLockIn(0.5f), Eps);
            // 1^2 = 1（深い過剰は完全固定）
            Assert.AreEqual(1f, MilitaryIndustrialRules.CapacityLockIn(1f), Eps);
            Assert.AreEqual(0f, MilitaryIndustrialRules.CapacityLockIn(0f), Eps);
        }

        /// <summary>財政圧迫＝過剰建艦×(1.5−0.5×財政余力)。余力が薄いほど同じ過剰建艦が重い。</summary>
        [Test]
        public void FiscalDrain_HeavierWithLowCapacity()
        {
            // over0.4・cap1 → burden1.0 → 0.4
            Assert.AreEqual(0.4f, MilitaryIndustrialRules.FiscalDrainFromOverbuild(0.4f, 1f), Eps);
            // over0.4・cap0 → burden1.5 → 0.6（余力ゼロは1.5倍痛い）
            Assert.AreEqual(0.6f, MilitaryIndustrialRules.FiscalDrainFromOverbuild(0.4f, 0f), Eps);
        }

        /// <summary>天下り癒着＝発注側と受注側の結びつき×感度。回転ドアが調達を歪める。</summary>
        [Test]
        public void RevolvingDoor_ScalesWithTies()
        {
            Assert.AreEqual(0.7f, MilitaryIndustrialRules.RevolvingDoorCorruption(0.7f), Eps);
            Assert.AreEqual(0f, MilitaryIndustrialRules.RevolvingDoorCorruption(0f), Eps);
        }

        /// <summary>建艦バブル＝過剰圧×(1−必要)が閾値0.3以上。必要が低いのに過剰なほど成立、必要十分なら正当な軍拡。</summary>
        [Test]
        public void IsArmamentBubble_WhenOverbuildDivergesFromNeed()
        {
            // over0.8・need0 → 0.8 ≥ 0.3 ＝バブル
            Assert.IsTrue(MilitaryIndustrialRules.IsArmamentBubble(0.8f, 0f));
            // over0.8・need0.7 → 0.24 < 0.3 ＝バブルでない（必要がそこそこある）
            Assert.IsFalse(MilitaryIndustrialRules.IsArmamentBubble(0.8f, 0.7f));
            // 必要が満たされていれば過剰圧が高くてもバブルでない
            Assert.IsFalse(MilitaryIndustrialRules.IsArmamentBubble(1f, 1f));
        }

        /// <summary>全入力クランプ＝範囲外でも0..1の出力を保つ（決定論・例外なし）。</summary>
        [Test]
        public void Clamps_OutOfRangeInputs()
        {
            Assert.AreEqual(1f, MilitaryIndustrialRules.ProcurementBias(5f, 5f), Eps);
            Assert.AreEqual(0f, MilitaryIndustrialRules.OverbuildPressure(-1f, -1f), Eps);
            float drain = MilitaryIndustrialRules.FiscalDrainFromOverbuild(2f, -3f);
            Assert.That(drain, Is.InRange(0f, 1f));
        }
    }
}
