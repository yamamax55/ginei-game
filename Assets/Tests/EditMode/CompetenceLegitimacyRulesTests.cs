using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 尚賢の正統性直結（MOZI-4 #1565）の純ロジック検証。能力本位度・正統性保全・
    /// 無能登用ペナルティ・人材誘引・縁故の侵食・尚賢判定を既定 Params の具体値で固定する。
    /// </summary>
    public class CompetenceLegitimacyRulesTests
    {
        private const float Eps = 1e-4f;

        /// <summary>能力本位度＝能力×(1−縁故)。賢者を縁故抜きで登用すれば高い。</summary>
        [Test]
        public void MeritocracyIndex_有能かつ非縁故で高い()
        {
            // 能力0.9・縁故0.2 → 0.9×0.8 = 0.72
            Assert.AreEqual(0.72f, CompetenceLegitimacyRules.MeritocracyIndex(0.9f, 0.2f), Eps);
            // 有能でも縁故全開なら0
            Assert.AreEqual(0f, CompetenceLegitimacyRules.MeritocracyIndex(0.9f, 1f), Eps);
        }

        /// <summary>正統性保全倍率＝1+能力本位度×0.3。尚賢ほど1超、縁故偏重で1。</summary>
        [Test]
        public void LegitimacyPreservation_尚賢が正統性を保全する()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 能力本位度1.0 → 1+1×0.3 = 1.3
            Assert.AreEqual(1.3f, CompetenceLegitimacyRules.LegitimacyPreservation(1f, prm), Eps);
            // 能力本位度0 → 1.0（保全効果なし）
            Assert.AreEqual(1f, CompetenceLegitimacyRules.LegitimacyPreservation(0f, prm), Eps);
        }

        /// <summary>無能登用ペナルティ＝(1−能力)×重要度×0.4。重要ポストへ無能ほど大きい。</summary>
        [Test]
        public void IncompetencePenalty_重要ポストの無能登用が正統性を蝕む()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 能力0・重要度1 → 1×1×0.4 = 0.4（最大の損ない）
            Assert.AreEqual(0.4f, CompetenceLegitimacyRules.IncompetencePenalty(0f, 1f, prm), Eps);
            // 賢者(能力1)を重要ポストへ → 0
            Assert.AreEqual(0f, CompetenceLegitimacyRules.IncompetencePenalty(1f, 1f, prm), Eps);
            // 凡庸(能力0.5)を末端(重要度0.2)へ → 0.5×0.2×0.4 = 0.04（小さい）
            Assert.AreEqual(0.04f, CompetenceLegitimacyRules.IncompetencePenalty(0.5f, 0.2f, prm), Eps);
        }

        /// <summary>人材誘引＝1+能力本位度×(2−1)。尚賢が賢才を呼ぶ。</summary>
        [Test]
        public void TalentAttraction_能力本位の体制ほど人材が集まる()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 能力本位度1 → 1+1×1 = 2倍
            Assert.AreEqual(2f, CompetenceLegitimacyRules.TalentAttraction(1f, prm), Eps);
            // 能力本位度0.5 → 1.5倍
            Assert.AreEqual(1.5f, CompetenceLegitimacyRules.TalentAttraction(0.5f, prm), Eps);
        }

        /// <summary>民の信頼＝能力本位度0.5×0.5+統治結果0.5×0.5。有能さが良い統治を生んでこそ。</summary>
        [Test]
        public void PopularConfidence_能力本位と良い統治結果の加重平均()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 能力本位度0.8・統治結果0.6・重み0.5 → 0.5×0.8+0.5×0.6 = 0.7
            Assert.AreEqual(0.7f, CompetenceLegitimacyRules.PopularConfidence(0.8f, 0.6f, prm), Eps);
        }

        /// <summary>縁故の侵食＝縁故×(dt/120)ぶん正統性を削る。放置で蝕む。</summary>
        [Test]
        public void NepotismDecayTick_縁故主義が時間で正統性を蝕む()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 正統性0.8・縁故0.5・dt12 → 0.8 − 0.5×(12/120) = 0.8 − 0.05 = 0.75
            Assert.AreEqual(0.75f, CompetenceLegitimacyRules.NepotismDecayTick(0.8f, 0.5f, 12f, prm), Eps);
            // 縁故0なら不変
            Assert.AreEqual(0.8f, CompetenceLegitimacyRules.NepotismDecayTick(0.8f, 0f, 12f, prm), Eps);
        }

        /// <summary>賢者登用の選抜＝尚賢なら能力・そうでなければ門地を評価値に取る。</summary>
        [Test]
        public void ElevateWorthy_能力で選ぶか身分で選ぶか()
        {
            // 能力0.9・門地0.3：尚賢→0.9、門地登用→0.3
            Assert.AreEqual(0.9f, CompetenceLegitimacyRules.ElevateWorthy(0.9f, 0.3f, true), Eps);
            Assert.AreEqual(0.3f, CompetenceLegitimacyRules.ElevateWorthy(0.9f, 0.3f, false), Eps);
        }

        /// <summary>尚賢判定＝能力本位度が閾値0.6以上。負の threshold は既定値。</summary>
        [Test]
        public void IsMeritocraticRegime_尚賢の体制か()
        {
            var prm = CompetenceLegitimacyParams.Default;
            // 0.7 ≥ 0.6 → true
            Assert.IsTrue(CompetenceLegitimacyRules.IsMeritocraticRegime(0.7f, -1f, prm));
            // 0.5 < 0.6 → false
            Assert.IsFalse(CompetenceLegitimacyRules.IsMeritocraticRegime(0.5f, -1f, prm));
            // 明示閾値0.4なら 0.5 ≥ 0.4 → true
            Assert.IsTrue(CompetenceLegitimacyRules.IsMeritocraticRegime(0.5f, 0.4f, prm));
        }
    }
}
