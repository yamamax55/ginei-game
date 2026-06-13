using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 白兵戦（移乗攻撃）を固定する：実効戦力＝兵数×技量（防御は健全度も乗算）、比で制圧/撃退/膠着、
    /// 無人艦は無血制圧、損耗は劣勢側ほど重い、拿捕の値打ちは健全度比例。境界を担保。
    /// </summary>
    public class BoardingRulesTests
    {
        private static readonly BoardingParams P = BoardingParams.Default; // 制圧1.5/撃退0.7/損耗0.2

        [Test]
        public void Powers_ProductOfFactors()
        {
            Assert.AreEqual(120f, BoardingRules.AssaultPower(100f, 1.2f), 1e-4f); // 精鋭は技量1超可
            Assert.AreEqual(50f, BoardingRules.DefensePower(100f, 1f, 0.5f), 1e-4f); // 損傷艦は守りにくい
            Assert.AreEqual(0f, BoardingRules.DefensePower(100f, 1f, 0f), 1e-5f);
        }

        [Test]
        public void Resolve_ByRatioThresholds()
        {
            Assert.AreEqual(BoardingOutcome.制圧, BoardingRules.Resolve(150f, 100f, P)); // 比1.5＝制圧
            Assert.AreEqual(BoardingOutcome.膠着, BoardingRules.Resolve(100f, 100f, P)); // 比1.0＝膠着
            Assert.AreEqual(BoardingOutcome.撃退, BoardingRules.Resolve(60f, 100f, P));  // 比0.6＝撃退
            Assert.AreEqual(BoardingOutcome.膠着, BoardingRules.Resolve(70f, 100f, P));  // 比0.7ちょうど＝膠着側
        }

        [Test]
        public void Resolve_UnmannedShip_IsBloodlessCapture()
        {
            Assert.AreEqual(BoardingOutcome.制圧, BoardingRules.Resolve(10f, 0f, P));
            Assert.AreEqual(0f, BoardingRules.AttackerCasualties(100f, 10f, 0f, P), 1e-5f); // 無血
        }

        [Test]
        public void Casualties_HeavierForTheWeakerSide()
        {
            // 攻撃側劣勢（比0.5）＝損耗重い：100×0.2×min(1, 1/0.5→1)=20
            Assert.AreEqual(20f, BoardingRules.AttackerCasualties(100f, 50f, 100f, P), 1e-4f);
            // 攻撃側優勢（比2.0）＝損耗軽い：100×0.2×0.5=10
            Assert.AreEqual(10f, BoardingRules.AttackerCasualties(100f, 200f, 100f, P), 1e-4f);
            // 防御側は攻撃側が優勢なほど重い：100×0.2×min(1,2)=20
            Assert.AreEqual(20f, BoardingRules.DefenderCasualties(100f, 200f, 100f, P), 1e-4f);
            Assert.AreEqual(10f, BoardingRules.DefenderCasualties(100f, 50f, 100f, P), 1e-4f);
        }

        [Test]
        public void DefenderCasualties_FullAgainstUnmannedRatio()
        {
            // 防御0（無限比）でも防御側損耗は上限1倍で頭打ち
            Assert.AreEqual(20f, BoardingRules.DefenderCasualties(100f, 100f, 0f, P), 1e-4f);
        }

        [Test]
        public void PrizeValue_ScalesWithIntegrity()
        {
            Assert.AreEqual(100f, BoardingRules.PrizeValue(100f, 1f), 1e-4f);
            Assert.AreEqual(30f, BoardingRules.PrizeValue(100f, 0.3f), 1e-4f); // 壊しすぎた艦は値打ちが落ちる
            Assert.AreEqual(0f, BoardingRules.PrizeValue(100f, 0f), 1e-5f);
        }

        [Test]
        public void PowerRatio_EdgeCases()
        {
            Assert.AreEqual(0f, BoardingRules.PowerRatio(0f, 100f), 1e-5f);
            Assert.IsTrue(float.IsPositiveInfinity(BoardingRules.PowerRatio(10f, 0f)));
        }
    }
}
