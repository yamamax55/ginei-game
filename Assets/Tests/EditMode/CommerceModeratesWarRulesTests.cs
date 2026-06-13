using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>
    /// 通商と温和政治（MONT-6 #1453・モンテスキューのやわらかな商業）の純ロジックテスト。
    /// 相互依存・戦争忌避・習俗の洗練・厭戦の加速・専制の商業抑圧・平和の配当・貿易戦争リスク・商業共和国判定を担保。
    /// </summary>
    public class CommerceModeratesWarRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>相互依存＝交易量×相互依存＝代替不能性が無ければ依存も無い。</summary>
        [Test]
        public void Interdependence_掛け合わせ()
        {
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, CommerceModeratesWarRules.Interdependence(0.8f, 0.5f), Eps);
            // 相互依存ゼロなら量があってもゼロ
            Assert.AreEqual(0f, CommerceModeratesWarRules.Interdependence(1f, 0f), Eps);
            // クランプ
            Assert.AreEqual(1f, CommerceModeratesWarRules.Interdependence(2f, 2f), Eps);
        }

        /// <summary>戦争忌避＝相互依存×0.8（既定）＝交易で結ばれた国ほど戦争を避ける。</summary>
        [Test]
        public void WarReluctance_相互依存に比例()
        {
            // 0.5 × 0.8 = 0.4
            Assert.AreEqual(0.4f, CommerceModeratesWarRules.WarReluctance(0.5f), Eps);
            // 相互依存ゼロなら忌避もゼロ（自由に戦える）
            Assert.AreEqual(0f, CommerceModeratesWarRules.WarReluctance(0f), Eps);
        }

        /// <summary>習俗の洗練＝商業水準×0.05×dt＝商業が荒々しさを温和にする。</summary>
        [Test]
        public void MoresRefinement_商業が習俗を温和化()
        {
            // 0.6 × 0.05 × 1.0 = 0.03
            Assert.AreEqual(0.03f, CommerceModeratesWarRules.MoresRefinement(0.6f, 1f), Eps);
            // 商業ゼロなら洗練されない
            Assert.AreEqual(0f, CommerceModeratesWarRules.MoresRefinement(0f, 1f), Eps);
        }

        /// <summary>厭戦の加速＝1＋交易量×破壊×0.5＝戦争が交易を壊すほど商人が早く和平を望む。</summary>
        [Test]
        public void WarWearinessAcceleration_交易破壊で加速()
        {
            // 1 + 1.0 × 1.0 × 0.5 = 1.5
            Assert.AreEqual(1.5f, CommerceModeratesWarRules.WarWearinessAcceleration(1f, 1f), Eps);
            // 交易が無ければ加速なし（係数1.0）
            Assert.AreEqual(1f, CommerceModeratesWarRules.WarWearinessAcceleration(0f, 1f), Eps);
        }

        /// <summary>専制の商業抑圧＝1−専制度＝専制下では温和化が働かない。</summary>
        [Test]
        public void DespotismCommerceSuppression_専制が温和化を殺す()
        {
            // 完全専制なら温和化倍率0（商業の温和化が消える）
            Assert.AreEqual(0f, CommerceModeratesWarRules.DespotismCommerceSuppression(1f), Eps);
            // 専制ゼロなら満額
            Assert.AreEqual(1f, CommerceModeratesWarRules.DespotismCommerceSuppression(0f), Eps);
            // 0.3 → 0.7
            Assert.AreEqual(0.7f, CommerceModeratesWarRules.DespotismCommerceSuppression(0.3f), Eps);
        }

        /// <summary>商業平和の配当＝相互依存×(1+平和継続×0.3)＝平和が交易を深める好循環。</summary>
        [Test]
        public void CommercialPeaceDividend_平和が交易を深める()
        {
            // 0.4 × (1 + 1.0 × 0.3) = 0.52
            Assert.AreEqual(0.52f, CommerceModeratesWarRules.CommercialPeaceDividend(0.4f, 1f), Eps);
            // 平和継続ゼロなら配当は相互依存そのまま
            Assert.AreEqual(0.4f, CommerceModeratesWarRules.CommercialPeaceDividend(0.4f, 0f), Eps);
        }

        /// <summary>貿易戦争リスク＝相互依存×経済的強制×0.6＝依存の武器化が摩擦を生む諸刃。</summary>
        [Test]
        public void TradeWarRisk_依存の武器化が摩擦()
        {
            // 0.5 × 1.0 × 0.6 = 0.3
            Assert.AreEqual(0.3f, CommerceModeratesWarRules.TradeWarRisk(0.5f, 1f), Eps);
            // 強制が無ければ依存があってもリスクゼロ（平和を促すだけ）
            Assert.AreEqual(0f, CommerceModeratesWarRules.TradeWarRisk(1f, 0f), Eps);
        }

        /// <summary>商業共和国判定＝商業が栄え（閾値0.5以上）かつ専制でない（閾値未満）。</summary>
        [Test]
        public void IsCommercialRepublic_商業栄え専制でない()
        {
            // 商業0.8・専制0.2 → 共和国
            Assert.IsTrue(CommerceModeratesWarRules.IsCommercialRepublic(0.8f, 0.2f));
            // 商業が栄えても専制が強ければ共和国でない（専制が商業を抑圧）
            Assert.IsFalse(CommerceModeratesWarRules.IsCommercialRepublic(0.8f, 0.7f));
            // 専制でなくても商業が低ければ共和国でない
            Assert.IsFalse(CommerceModeratesWarRules.IsCommercialRepublic(0.3f, 0.1f));
        }
    }
}
