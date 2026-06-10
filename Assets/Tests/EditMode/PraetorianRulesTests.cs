using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>親衛隊の両刃（プラエトリアニ型）の純ロジックを検証。厚遇のジレンマ＝忠誠と政治力が同時に育つことを担保。</summary>
    public class PraetorianRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>身辺安全＝精鋭×忠誠（強くても不忠なら守らない）。</summary>
        [Test]
        public void ProtectionStrength_精鋭と忠誠の積()
        {
            Assert.AreEqual(0.6f, PraetorianRules.ProtectionStrength(0.8f, 0.75f), Eps);
            // 強くても忠誠ゼロなら守らない
            Assert.AreEqual(0f, PraetorianRules.ProtectionStrength(1f, 0f), Eps);
        }

        /// <summary>政治力＝強さ×近さ（守る者が支配する。どちらか欠ければゼロ）。</summary>
        [Test]
        public void PoliticalLeverage_強さと近さの積()
        {
            Assert.AreEqual(0.5f, PraetorianRules.PoliticalLeverage(1f, 0.5f), Eps);
            Assert.AreEqual(0f, PraetorianRules.PoliticalLeverage(0.9f, 0f), Eps);
        }

        /// <summary>簒奪リスク＝政治力×不忠（強い近衛＋低忠誠＝皇帝を競売にかける）。忠誠1ならゼロ。</summary>
        [Test]
        public void KingmakerRisk_政治力と不忠の積()
        {
            // 政治力0.8・忠誠0.25 → 0.8×0.75 = 0.6
            Assert.AreEqual(0.6f, PraetorianRules.KingmakerRisk(0.8f, 0.25f), Eps);
            // 完全忠誠ならリスク無し
            Assert.AreEqual(0f, PraetorianRules.KingmakerRisk(1f, 1f), Eps);
            // 既定閾値0.6 で発火
            Assert.IsTrue(PraetorianRules.WouldDepose(0.8f, 0.25f));
            Assert.IsFalse(PraetorianRules.WouldDepose(0.8f, 0.9f));
        }

        /// <summary>厚遇のジレンマ＝同じ特権が忠誠と政治力を同時に育てる（守護者にして簒奪者）。</summary>
        [Test]
        public void Pampering_厚遇のジレンマ_忠誠と政治力が同時に育つ()
        {
            // 既定 pamperGain0.15/decay0.02：忠誠0.5・特権1・dt1 → 0.5+(0.15-0.02) = 0.63
            float loyalty = PraetorianRules.PamperingTick(0.5f, 1f, 1f);
            Assert.AreEqual(0.63f, loyalty, Eps);
            Assert.Greater(loyalty, 0.5f); // 厚遇で忠誠は上がる

            // 既定 leverageCreep0.10：近さ0.5・特権1・dt1 → 0.5+0.10 = 0.60
            float leverage = PraetorianRules.LeverageCreep(0.5f, 1f, 1f);
            Assert.AreEqual(0.60f, leverage, Eps);
            Assert.Greater(leverage, 0.5f); // 同じ厚遇で政治力＝危険も育つ
        }

        /// <summary>特権ゼロなら忠誠は減衰のみ（恩は風化する）。</summary>
        [Test]
        public void PamperingTick_特権なしは減衰のみ()
        {
            // 0.5 + (0 - 0.02) = 0.48
            Assert.AreEqual(0.48f, PraetorianRules.PamperingTick(0.5f, 0f, 1f), Eps);
        }

        /// <summary>冷遇の脆弱性＝近衛投資が薄いほど高い（暗殺・クーデターに無防備）。</summary>
        [Test]
        public void NeglectVulnerability_投資の逆()
        {
            Assert.AreEqual(0.7f, PraetorianRules.NeglectVulnerability(0.3f), Eps);
            Assert.AreEqual(0f, PraetorianRules.NeglectVulnerability(1f), Eps);
        }

        /// <summary>最適近衛規模＝外部脅威と内部信頼の中庸（強すぎても弱すぎても危ない谷）。</summary>
        [Test]
        public void OptimalGuardStrength_脅威と信頼の綱引き()
        {
            // (0.8 + 0.4)/2 = 0.6
            Assert.AreEqual(0.6f, PraetorianRules.OptimalGuardStrength(0.8f, 0.4f), Eps);
            // 入力クランプ（範囲外でも0..1）
            Assert.AreEqual(0.5f, PraetorianRules.OptimalGuardStrength(2f, 0f), Eps);
        }
    }
}
