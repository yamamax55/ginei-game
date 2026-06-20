using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 供給反応（価格弾力性of供給）を固定する：価格比、価格→産出の単調増加（高値で増産・安値で減産）、
    /// 均衡で1.0（後方互換）、上下限のクランプ、弾力性0で無反応、利鞘による増産の割り引き（赤字は増産しない）、
    /// Market 簡便版、null/境界の安全、決定論。経済フィードバックの欠けた環（価格→生産）を担保。
    /// </summary>
    public class SupplyResponseRulesTests
    {
        private static readonly SupplyResponseParams P = SupplyResponseParams.Default;
        // 既定＝弾力性0.5・下限0.5/上限2・利鞘感応度1

        [Test]
        public void PriceRatio_EqualsPriceOverBase()
        {
            Assert.AreEqual(2f, SupplyResponseRules.PriceRatio(200f, 100f), 1e-4f);
            Assert.AreEqual(0.5f, SupplyResponseRules.PriceRatio(50f, 100f), 1e-4f);
            Assert.AreEqual(1f, SupplyResponseRules.PriceRatio(100f, 100f), 1e-4f);
        }

        [Test]
        public void PriceRatio_ZeroOrNegativeBase_IsNeutralOne()
        {
            Assert.AreEqual(1f, SupplyResponseRules.PriceRatio(500f, 0f), 1e-4f);
            Assert.AreEqual(1f, SupplyResponseRules.PriceRatio(500f, -10f), 1e-4f);
        }

        [Test]
        public void PriceRatio_NegativePrice_ClampedToZero()
        {
            Assert.AreEqual(0f, SupplyResponseRules.PriceRatio(-50f, 100f), 1e-4f);
        }

        [Test]
        public void OutputFactor_AtEquilibrium_IsExactlyOne()
        {
            Assert.AreEqual(1f, SupplyResponseRules.OutputFactor(100f, 100f, P), 1e-4f);
        }

        [Test]
        public void OutputFactor_HighPrice_ExpandsOutput()
        {
            float f = SupplyResponseRules.OutputFactor(150f, 100f, P);
            Assert.Greater(f, 1f);
        }

        [Test]
        public void OutputFactor_LowPrice_CutsOutput()
        {
            float f = SupplyResponseRules.OutputFactor(60f, 100f, P);
            Assert.Less(f, 1f);
        }

        [Test]
        public void OutputFactor_MonotonicInPrice()
        {
            float prev = 0f;
            for (float price = 10f; price <= 400f; price += 10f)
            {
                float f = SupplyResponseRules.OutputFactor(price, 100f, P);
                Assert.GreaterOrEqual(f, prev - 1e-5f, "産出倍率は価格に対し非減少であるべき");
                prev = f;
            }
        }

        [Test]
        public void OutputFactor_ClampedToMaxAndMin()
        {
            // 価格を極端に振っても上下限を超えない。
            float hi = SupplyResponseRules.OutputFactor(100000f, 100f, P);
            float lo = SupplyResponseRules.OutputFactor(0.001f, 100f, P);
            Assert.LessOrEqual(hi, P.maxFactor + 1e-4f);
            Assert.GreaterOrEqual(lo, P.minFactor - 1e-4f);
            Assert.AreEqual(2f, hi, 1e-3f);   // 上限
            Assert.AreEqual(0.5f, lo, 1e-3f); // 下限
        }

        [Test]
        public void OutputFactor_ZeroElasticity_NoResponse()
        {
            var p = new SupplyResponseParams(0f, 0.5f, 2f, 1f);
            Assert.AreEqual(1f, SupplyResponseRules.OutputFactor(300f, 100f, p), 1e-4f);
            Assert.AreEqual(1f, SupplyResponseRules.OutputFactor(20f, 100f, p), 1e-4f);
        }

        [Test]
        public void OutputFactor_Deterministic()
        {
            float a = SupplyResponseRules.OutputFactor(173f, 100f, P);
            float b = SupplyResponseRules.OutputFactor(173f, 100f, P);
            Assert.AreEqual(a, b, 0f);
        }

        [Test]
        public void Params_ClampInvariants()
        {
            // 上限<下限を渡しても上限は下限以上に矯正される。
            var p = new SupplyResponseParams(-1f, 2f, 0.5f, -3f);
            Assert.GreaterOrEqual(p.maxFactor, p.minFactor);
            Assert.GreaterOrEqual(p.elasticity, 0f);
            Assert.GreaterOrEqual(p.marginGain, 0f);
        }

        [Test]
        public void OutputFactorWithMargin_HealthyMargin_KeepsExpansion()
        {
            // 単位費用が低い（厚い利鞘）＝価格シグナルどおり増産する。
            float plain = SupplyResponseRules.OutputFactor(200f, 100f, P);
            float withMargin = SupplyResponseRules.OutputFactorWithMargin(200f, 100f, 10f, P);
            Assert.Greater(plain, 1f);
            Assert.AreEqual(plain, withMargin, 0.05f); // ほぼ満額の増産
        }

        [Test]
        public void OutputFactorWithMargin_ThinMargin_DampensExpansion()
        {
            // 利鞘が薄い（費用が価格に近い）＝高値でも増産が鈍る。
            float thin = SupplyResponseRules.OutputFactorWithMargin(200f, 100f, 190f, P);
            float thick = SupplyResponseRules.OutputFactorWithMargin(200f, 100f, 10f, P);
            Assert.Less(thin, thick);
            Assert.GreaterOrEqual(thin, 1f); // 増産は鈍るが減産はしない
        }

        [Test]
        public void OutputFactorWithMargin_Loss_NoExpansion()
        {
            // 赤字（費用>価格）＝高値でも増産しない（1へ寄る）。
            float f = SupplyResponseRules.OutputFactorWithMargin(200f, 100f, 250f, P);
            Assert.AreEqual(1f, f, 1e-4f);
        }

        [Test]
        public void OutputFactorWithMargin_LowPrice_StillCuts()
        {
            // 減産ぶん（1未満）は利鞘に関係なくそのまま（赤字回避を強める方向）。
            float plain = SupplyResponseRules.OutputFactor(60f, 100f, P);
            float withMargin = SupplyResponseRules.OutputFactorWithMargin(60f, 100f, 200f, P);
            Assert.AreEqual(plain, withMargin, 1e-4f);
            Assert.Less(withMargin, 1f);
        }

        [Test]
        public void OutputFactorFor_NullMarket_IsNeutral()
        {
            Assert.AreEqual(1f, SupplyResponseRules.OutputFactorFor(null, 100f, P), 1e-4f);
        }

        [Test]
        public void OutputFactorFor_MatchesScalarOverload()
        {
            var m = new Market(GoodType.物資, 50f, 50f, 150f);
            float viaMarket = SupplyResponseRules.OutputFactorFor(m, 100f, P);
            float viaScalar = SupplyResponseRules.OutputFactor(150f, 100f, P);
            Assert.AreEqual(viaScalar, viaMarket, 1e-4f);
        }

        [Test]
        public void NegativeFeedback_ClosesLoop()
        {
            // 高値→増産（>1）／安値→減産（<1）＝供給が価格差を埋める方向に動く（負帰還）。
            float atGlut = SupplyResponseRules.OutputFactor(40f, 100f, P);   // 供給過剰で安い
            float atShortage = SupplyResponseRules.OutputFactor(180f, 100f, P); // 払底で高い
            Assert.Less(atGlut, 1f);       // 安いと減産→供給減→価格戻る
            Assert.Greater(atShortage, 1f); // 高いと増産→供給増→価格戻る
        }
    }
}
