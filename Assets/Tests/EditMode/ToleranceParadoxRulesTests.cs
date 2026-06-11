using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>寛容のパラドックス（POPR-4 #1518）の純ロジックテスト。</summary>
    public class ToleranceParadoxRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>不寛容派の悪用＝寛容な社会ほど攻撃的な者がその隙を突く（既定係数0.9）。</summary>
        [Test]
        public void IntolerantExploitation_寛容な社会ほど悪用される()
        {
            // 0.8 × 0.5 × 0.9 = 0.36
            float e = ToleranceParadoxRules.IntolerantExploitation(0.8f, 0.5f);
            Assert.AreEqual(0.36f, e, Eps);

            // 社会が不寛容（=0）なら隙が無く悪用されない
            Assert.AreEqual(0f, ToleranceParadoxRules.IntolerantExploitation(0f, 1f), Eps);

            // 寛容が高いほど悪用が大きい（単調）
            Assert.Greater(ToleranceParadoxRules.IntolerantExploitation(0.9f, 0.5f),
                           ToleranceParadoxRules.IntolerantExploitation(0.4f, 0.5f));
        }

        /// <summary>乗っ取りリスク＝多数化×無防備（寛容で無防衛）。制度防壁が固ければ防げる。</summary>
        [Test]
        public void TakeoverRisk_多数化と無防備で上がり制度防壁で下がる()
        {
            // share0.6 × (tol0.8 × (1-safeguards0.25)) = 0.6 × 0.6 = 0.36
            float r = ToleranceParadoxRules.TakeoverRisk(0.6f, 0.8f, 0.25f);
            Assert.AreEqual(0.36f, r, Eps);

            // 制度防壁が完璧なら勢力があっても乗っ取れない
            Assert.AreEqual(0f, ToleranceParadoxRules.TakeoverRisk(0.9f, 0.9f, 1f), Eps);

            // 勢力が大きいほどリスク大
            Assert.Greater(ToleranceParadoxRules.TakeoverRisk(0.8f, 0.8f, 0.25f),
                           ToleranceParadoxRules.TakeoverRisk(0.3f, 0.8f, 0.25f));
        }

        /// <summary>寛容の侵食＝乗っ取りが進むと社会全体の寛容が破壊される（寛容の自殺・既定0.06/dt）。</summary>
        [Test]
        public void ToleranceErosion_乗っ取りが寛容を破壊する()
        {
            // 0.9 - 0.06 × 0.5 × 2 = 0.9 - 0.06 = 0.84
            float t = ToleranceParadoxRules.ToleranceErosion(0.9f, 0.5f, 2f);
            Assert.AreEqual(0.84f, t, Eps);

            // 乗っ取りリスク0なら侵食しない
            Assert.AreEqual(0.9f, ToleranceParadoxRules.ToleranceErosion(0.9f, 0f, 5f), Eps);

            // 0未満にはならない
            Assert.AreEqual(0f, ToleranceParadoxRules.ToleranceErosion(0.01f, 1f, 100f), Eps);
        }

        /// <summary>抑制のジレンマ＝不寛容を抑えるほど自らの寛容原則を傷つける（既定係数0.7）。</summary>
        [Test]
        public void SuppressionDilemma_抑制が寛容原則を傷つける()
        {
            // 0.8 × 0.5 × 0.7 = 0.28
            float d = ToleranceParadoxRules.SuppressionDilemma(0.8f, 0.5f);
            Assert.AreEqual(0.28f, d, Eps);

            // 寛容原則を重んじないなら抑制の傷も無い
            Assert.AreEqual(0f, ToleranceParadoxRules.SuppressionDilemma(1f, 0f), Eps);
        }

        /// <summary>最適介入＝脅威で強め抑制コストで控える（早すぎても遅すぎてもいけない線引き）。</summary>
        [Test]
        public void OptimalIntervention_脅威と抑制コストで線を引く()
        {
            // 0.7 × (1 - 0.3) = 0.49
            float i = ToleranceParadoxRules.OptimalIntervention(0.7f, 0.3f);
            Assert.AreEqual(0.49f, i, Eps);

            // 脅威が小さければ介入は無用（早すぎる介入は自らが不寛容）
            Assert.AreEqual(0f, ToleranceParadoxRules.OptimalIntervention(0f, 0.2f), Eps);

            // 抑制コストが大きいほど控える
            Assert.Greater(ToleranceParadoxRules.OptimalIntervention(0.7f, 0.1f),
                           ToleranceParadoxRules.OptimalIntervention(0.7f, 0.8f));
        }

        /// <summary>自滅する寛容＝寛容が高いまま脅威が高い臨界（寛容しすぎて手遅れ）。</summary>
        [Test]
        public void SelfDefeatingTolerance_寛容も脅威も高いと臨界()
        {
            // 寛容0.8・脅威0.7・threshold0.6 → 両方threshold以上で true
            Assert.IsTrue(ToleranceParadoxRules.SelfDefeatingTolerance(0.8f, 0.7f, 0.6f));

            // 脅威が低ければ放置でも臨界に至らない
            Assert.IsFalse(ToleranceParadoxRules.SelfDefeatingTolerance(0.9f, 0.3f, 0.6f));

            // 既に抑制して寛容が低ければ（脅威が高くても）この臨界には当たらない
            Assert.IsFalse(ToleranceParadoxRules.SelfDefeatingTolerance(0.4f, 0.9f, 0.6f));
        }

        /// <summary>戦う民主主義＝制度防壁×脅威で防衛（脅威0なら平時に振るわない）。</summary>
        [Test]
        public void MilitantDemocracy_制度防壁と脅威で防衛する()
        {
            // 0.75 × 0.6 × 0.8 = 0.36
            float m = ToleranceParadoxRules.MilitantDemocracy(0.75f, 0.6f);
            Assert.AreEqual(0.36f, m, Eps);

            // 脅威が無ければ制度を振るわない（濫用回避）
            Assert.AreEqual(0f, ToleranceParadoxRules.MilitantDemocracy(1f, 0f), Eps);
        }

        /// <summary>寛容崩壊判定＝侵食後の寛容が閾値以下まで落ちた（寛容の自殺の完了）。</summary>
        [Test]
        public void IsToleranceCollapse_侵食後の寛容が閾値以下で崩壊()
        {
            Assert.IsTrue(ToleranceParadoxRules.IsToleranceCollapse(0.15f, 0.2f));
            Assert.IsFalse(ToleranceParadoxRules.IsToleranceCollapse(0.5f, 0.2f));
            // 境界（=threshold）は崩壊扱い
            Assert.IsTrue(ToleranceParadoxRules.IsToleranceCollapse(0.2f, 0.2f));
        }
    }
}
