using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>惑星防衛3層（#1070）の純ロジック検証。各層での攻撃減衰・層の相乗・最弱の突破口を担保する。</summary>
    public class PlanetaryDefenseRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>外層（防衛艦隊）の迎撃＝層戦力×係数で攻撃を削る。係数1.0＝層戦力がそのまま減衰量。</summary>
        [Test]
        public void LayerInterception_外層は層戦力ぶん攻撃を削る()
        {
            // 防衛艦隊scale=1.0、層30・攻撃100 → 30。上限=100×0.7=70 を下回るのでそのまま30。
            float lost = PlanetaryDefenseRules.LayerInterception(DefenseLayer.防衛艦隊, 30f, 100f);
            Assert.AreEqual(30f, lost, Eps);
        }

        /// <summary>1層で攻撃を全滅はできない＝maxLayerAttrition(0.7)が迎撃の上限。</summary>
        [Test]
        public void LayerInterception_1層の減衰は上限でクランプ()
        {
            // 層200・攻撃100 → 素では200だが上限=100×0.7=70。
            float lost = PlanetaryDefenseRules.LayerInterception(DefenseLayer.防衛艦隊, 200f, 100f);
            Assert.AreEqual(70f, lost, Eps);
        }

        /// <summary>内層ほど迎撃係数が小さい（軌道部隊0.6＜衛星0.8＜艦隊1.0）。</summary>
        [Test]
        public void LayerInterception_内層ほど係数が小さい()
        {
            float fleet = PlanetaryDefenseRules.LayerInterception(DefenseLayer.防衛艦隊, 50f, 1000f);
            float sat = PlanetaryDefenseRules.LayerInterception(DefenseLayer.防衛衛星, 50f, 1000f);
            float orb = PlanetaryDefenseRules.LayerInterception(DefenseLayer.軌道部隊, 50f, 1000f);
            Assert.AreEqual(50f, fleet, Eps);  // 50×1.0
            Assert.AreEqual(40f, sat, Eps);    // 50×0.8
            Assert.AreEqual(30f, orb, Eps);    // 50×0.6
            Assert.Less(orb, sat);
            Assert.Less(sat, fleet);
        }

        /// <summary>3層を抜けるたびに攻撃が痩せる＝惑星に届く残存攻撃力は各層で順に削られた残り。</summary>
        [Test]
        public void PenetratingForce_各層を抜くたび攻撃が痩せる()
        {
            // 攻撃100、層[艦隊20, 衛星20, 軌道部隊20]。上限は各段で remaining×0.7。
            // 艦隊: 20×1.0=20 → 100-20=80
            // 衛星: 20×0.8=16 → 80-16=64
            // 軌道: 20×0.6=12 → 64-12=52
            float[] layers = { 20f, 20f, 20f };
            float pen = PlanetaryDefenseRules.PenetratingForce(100f, layers);
            Assert.AreEqual(52f, pen, Eps);
            Assert.Less(pen, 100f); // 確かに痩せている
        }

        /// <summary>層が厚ければ攻撃は惑星に届かない（全層で削り切る）。</summary>
        [Test]
        public void PenetratingForce_厚い層は攻撃を届かせない()
        {
            // 攻撃100、各層1000。各段で上限=remaining×0.7。
            // 100→30→9→2.7
            float[] layers = { 1000f, 1000f, 1000f };
            float pen = PlanetaryDefenseRules.PenetratingForce(100f, layers);
            Assert.AreEqual(2.7f, pen, Eps);
        }

        /// <summary>層の相乗が単独の和を超える（揃った3層の相互支援＝重層防御の妙）。</summary>
        [Test]
        public void LayerSynergy_3層の相乗は和を超える()
        {
            // 各層40。和=120。幾何平均=40、相乗=0.25×40×(3-1)=20 → 140。
            float[] layers = { 40f, 40f, 40f };
            float syn = PlanetaryDefenseRules.LayerSynergy(layers);
            Assert.AreEqual(140f, syn, Eps);
            Assert.Greater(syn, 120f); // 単独の和を超える
        }

        /// <summary>単独の層（1層のみ健在）では相乗が無く、和そのもの。</summary>
        [Test]
        public void LayerSynergy_単独層は相乗なし()
        {
            float[] layers = { 40f, 0f, 0f };
            float syn = PlanetaryDefenseRules.LayerSynergy(layers);
            Assert.AreEqual(40f, syn, Eps);
        }

        /// <summary>最弱の層が突破口＝攻撃側が狙う穴（同値は外層を優先）。</summary>
        [Test]
        public void WeakestLayer_最弱の層が突破口()
        {
            float[] layers = { 50f, 10f, 30f };
            Assert.AreEqual(1, PlanetaryDefenseRules.WeakestLayer(layers)); // 衛星=10 が最弱

            float[] tie = { 20f, 20f, 30f };
            Assert.AreEqual(0, PlanetaryDefenseRules.WeakestLayer(tie)); // 同値は外層優先

            Assert.AreEqual(-1, PlanetaryDefenseRules.WeakestLayer(null));
        }

        /// <summary>健在な層が多いほど防御縦深ボーナスが増す（援軍を待てる時間）。</summary>
        [Test]
        public void DefenseDepthBonus_健在層が多いほど時間を稼ぐ()
        {
            Assert.AreEqual(1f, PlanetaryDefenseRules.DefenseDepthBonus(0), Eps);   // 裸＝ボーナス無し
            Assert.AreEqual(1.2f, PlanetaryDefenseRules.DefenseDepthBonus(1), Eps); // 1+0.2×1
            Assert.AreEqual(1.6f, PlanetaryDefenseRules.DefenseDepthBonus(3), Eps); // 1+0.2×3
            Assert.AreEqual(1f, PlanetaryDefenseRules.DefenseDepthBonus(-5), Eps);  // 負はクランプ
        }

        /// <summary>制空権争い＝軌道を制した側が地上を支配（戦力比・拮抗で0.5・攻撃皆無で守備1.0）。</summary>
        [Test]
        public void OrbitalSupremacyContest_軌道を制した側が支配()
        {
            Assert.AreEqual(0.5f, PlanetaryDefenseRules.OrbitalSupremacyContest(50f, 50f), Eps); // 拮抗
            Assert.AreEqual(0.75f, PlanetaryDefenseRules.OrbitalSupremacyContest(75f, 25f), Eps); // 守備優勢
            Assert.AreEqual(1f, PlanetaryDefenseRules.OrbitalSupremacyContest(0f, 0f), Eps); // 係争なし＝守備のもの
        }
    }
}
