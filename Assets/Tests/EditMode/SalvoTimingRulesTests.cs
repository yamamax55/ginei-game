using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 斉射タイミング：一斉集中(サルヴォ)で防御を飽和させ貫くが、再装填中は無防備。
    /// 既定 Params で期待値を固定（Pow 箇所のみ許容を緩める）。
    /// </summary>
    public class SalvoTimingRulesTests
    {
        [Test]
        public void SalvoConcentration_FullGunsAndDiscipline()
        {
            // 基準砲数200・規律1.0＝pow(1,0.7)*1*1=1.0（上限）。
            Assert.AreEqual(1f, SalvoTimingRules.SalvoConcentration(200f, 1f), 1e-4f);
            // 砲数0＝密度0。
            Assert.AreEqual(0f, SalvoTimingRules.SalvoConcentration(0f, 1f), 1e-4f);
            // 半数100・規律0.8＝pow(0.5,0.7)*0.8≒0.49246（Pow＝緩め）。
            Assert.AreEqual(0.49246f, SalvoTimingRules.SalvoConcentration(100f, 0.8f), 1e-3f);
        }

        [Test]
        public void DefenseSaturation_DensityVersusPointDefense()
        {
            // 密度1.0 対 迎撃1.0＝1/(1+1)=0.5。
            Assert.AreEqual(0.5f, SalvoTimingRules.DefenseSaturation(1f, 1f), 1e-4f);
            // 密度1.0 対 迎撃0.25＝1/1.25=0.8（薄い迎撃ほど飽和）。
            Assert.AreEqual(0.8f, SalvoTimingRules.DefenseSaturation(1f, 0.25f), 1e-4f);
            // 迎撃ゼロ＝完全飽和。
            Assert.AreEqual(1f, SalvoTimingRules.DefenseSaturation(1f, 0f), 1e-4f);
        }

        [Test]
        public void PenetratingHits_ThroughSaturation()
        {
            // 密度1.0×飽和0.8×貫通0.8=0.64。
            Assert.AreEqual(0.64f, SalvoTimingRules.PenetratingHits(1f, 0.8f), 1e-4f);
            // 飽和0＝貫通なし。
            Assert.AreEqual(0f, SalvoTimingRules.PenetratingHits(1f, 0f), 1e-4f);
        }

        [Test]
        public void ReloadVulnerability_LongReloadMoreExposed()
        {
            // 密度1.0・再装填4(=基準)＝1*1*0.5=0.5（最大無防備）。
            Assert.AreEqual(0.5f, SalvoTimingRules.ReloadVulnerability(1f, 4f), 1e-4f);
            // 再装填2＝半分＝0.25。
            Assert.AreEqual(0.25f, SalvoTimingRules.ReloadVulnerability(1f, 2f), 1e-4f);
            // 即時再装填＝無防備なし。
            Assert.AreEqual(0f, SalvoTimingRules.ReloadVulnerability(1f, 0f), 1e-4f);
        }

        [Test]
        public void SustainedVsAlpha_SignedTradeoff()
        {
            // 密度優勢＝alpha 寄り(+1)。
            Assert.AreEqual(1f, SalvoTimingRules.SustainedVsAlpha(1f, 0f), 1e-4f);
            // 持続優勢＝sustained 寄り(-1)。
            Assert.AreEqual(-1f, SalvoTimingRules.SustainedVsAlpha(0f, 1f), 1e-4f);
            // 拮抗＝0。
            Assert.AreEqual(0f, SalvoTimingRules.SustainedVsAlpha(0.5f, 0.5f), 1e-4f);
        }

        [Test]
        public void TimingWindow_SlowRechargeWiderWindow()
        {
            // 回復3秒(=基準)＝窓1.0。
            Assert.AreEqual(1f, SalvoTimingRules.TimingWindow(3f), 1e-4f);
            // 回復1.5秒＝窓0.5（速い回復＝狭い窓）。
            Assert.AreEqual(0.5f, SalvoTimingRules.TimingWindow(1.5f), 1e-4f);
            // 即時回復＝窓なし。
            Assert.AreEqual(0f, SalvoTimingRules.TimingWindow(0f), 1e-4f);
        }

        [Test]
        public void VolleyEffectiveness_NetOfVulnerability()
        {
            // 貫通0.64・無防備0.5＝0.64*(1-0.5)=0.32。
            Assert.AreEqual(0.32f, SalvoTimingRules.VolleyEffectiveness(0.64f, 0.5f), 1e-4f);
            // 無防備なし＝貫通そのまま。
            Assert.AreEqual(0.64f, SalvoTimingRules.VolleyEffectiveness(0.64f, 0f), 1e-4f);
        }

        [Test]
        public void IsDefenseSaturated_Threshold()
        {
            Assert.IsTrue(SalvoTimingRules.IsDefenseSaturated(0.6f, 0.5f));
            Assert.IsFalse(SalvoTimingRules.IsDefenseSaturated(0.4f, 0.5f));
            Assert.IsTrue(SalvoTimingRules.IsDefenseSaturated(0.5f, 0.5f)); // 境界＝飽和
        }

        /// <summary>
        /// 物語：全砲を一斉斉射すると敵の点防御を飽和させて多くの弾が貫くが、
        /// 撃ち切った直後は再装填で無防備になり、正味効果は無防備ぶん目減りする。
        /// 同じ砲を分散させて持続射撃すると飽和に届かず貫通が落ちる。
        /// </summary>
        [Test]
        public void Narrative_AlphaSalvoSaturatesButLeavesReloadGap()
        {
            // 全砲一斉＝密度最大。
            float alpha = SalvoTimingRules.SalvoConcentration(200f, 1f);
            Assert.AreEqual(1f, alpha, 1e-4f);

            // 厚めの点防御も一斉集中で飽和を抜く。
            float sat = SalvoTimingRules.DefenseSaturation(alpha, 0.5f); // 1/1.5≒0.6667
            Assert.IsTrue(SalvoTimingRules.IsDefenseSaturated(sat)); // 既定しきい0.5以上
            float pen = SalvoTimingRules.PenetratingHits(alpha, sat); // 1*0.6667*0.8≒0.5333
            Assert.Greater(pen, 0.5f);

            // 撃ち切った直後は無防備（長い再装填）。
            float vuln = SalvoTimingRules.ReloadVulnerability(alpha, 4f); // 0.5
            float netAlpha = SalvoTimingRules.VolleyEffectiveness(pen, vuln); // pen*0.5
            Assert.AreEqual(pen * 0.5f, netAlpha, 1e-4f);

            // alpha 寄りのトレードオフ＝正の符号。
            Assert.Greater(SalvoTimingRules.SustainedVsAlpha(alpha, 0.2f), 0f);

            // 同じ砲を分散して持続射撃＝密度が落ち飽和に届かず貫通が減る。
            float sustained = SalvoTimingRules.SalvoConcentration(60f, 1f); // pow(0.3,0.7)≒0.4305
            float satSus = SalvoTimingRules.DefenseSaturation(sustained, 0.5f);
            float penSus = SalvoTimingRules.PenetratingHits(sustained, satSus);
            Assert.Less(penSus, pen); // 一斉斉射の方が多く貫く
            // ただし持続側は再装填の無防備が小さい（密度が低い）。
            float vulnSus = SalvoTimingRules.ReloadVulnerability(sustained, 4f);
            Assert.Less(vulnSus, vuln);
        }
    }
}
