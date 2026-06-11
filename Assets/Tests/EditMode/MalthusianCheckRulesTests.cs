using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>MalthusianCheckRules（マルサスチェック・MALT-2 #1575）の純ロジックテスト。既定Paramsで期待値固定。</summary>
    public class MalthusianCheckRulesTests
    {
        const float Eps = 1e-4f;

        /// <summary>予防的妨げ＝閾値未満は無変調1.0、逼迫で出生率が下がる。</summary>
        [Test]
        public void PreventiveCheck_閾値未満は無変調_逼迫で出生抑制()
        {
            // 閾値0.8未満＝抑制なし。
            Assert.AreEqual(1f, MalthusianCheckRules.PreventiveCheck(0.7f), Eps);
            // fsr=1.4＝over0.6/denom1.2＝intensity0.5→1-0.6×0.5=0.7。
            Assert.AreEqual(0.7f, MalthusianCheckRules.PreventiveCheck(1.4f), Eps);
        }

        /// <summary>積極的妨げ＝閾値未満は1.0、逼迫で死亡率が上がる。</summary>
        [Test]
        public void PositiveCheck_逼迫で死亡率が上がる()
        {
            Assert.AreEqual(1f, MalthusianCheckRules.PositiveCheck(0.5f), Eps);
            // fsr=1.4＝intensity0.5→1+0.5×0.5=1.25。
            Assert.AreEqual(1.25f, MalthusianCheckRules.PositiveCheck(1.4f), Eps);
        }

        /// <summary>出生死亡の変調倍率＝予防/積極チェックと一致（基準非破壊・倍率を返す）。</summary>
        [Test]
        public void 出生死亡の変調倍率がチェック係数と一致()
        {
            Assert.AreEqual(0.7f, MalthusianCheckRules.BirthRateModifier(0.06f, 1.4f), Eps);
            Assert.AreEqual(1.25f, MalthusianCheckRules.DeathRateModifier(1.4f), Eps);
        }

        /// <summary>純人口圧＝出生抑制ぶん＋死亡増ぶん（食糧水準へ引き戻す力）。</summary>
        [Test]
        public void NetPopulationPressure_出生抑制と死亡増の和()
        {
            // 逼迫なしは0。
            Assert.AreEqual(0f, MalthusianCheckRules.NetPopulationPressure(0.7f), Eps);
            // fsr=1.4＝(1-0.7)+(1.25-1)=0.3+0.25=0.55。
            Assert.AreEqual(0.55f, MalthusianCheckRules.NetPopulationPressure(1.4f), Eps);
        }

        /// <summary>飢饉死亡＝飢饉閾値超で立ち上がり、救援で和らぐ。閾値未満は0。</summary>
        [Test]
        public void FamineMortality_飢饉閾値超で発生し救援で軽減()
        {
            // fsr=1.0＝飢饉閾値1.2未満→0。
            Assert.AreEqual(0f, MalthusianCheckRules.FamineMortality(1.0f, 0f), Eps);
            // fsr=1.8＝over0.6/denom0.8＝intensity0.75・救援0→0.25×0.75=0.1875。
            Assert.AreEqual(0.1875f, MalthusianCheckRules.FamineMortality(1.8f, 0f), Eps);
            // 救援1.0＝relief(1-0.8)=0.2→0.1875×0.2=0.0375。
            Assert.AreEqual(0.0375f, MalthusianCheckRules.FamineMortality(1.8f, 1.0f), Eps);
        }

        /// <summary>均衡引き戻し＝人口過剰なら負（減）・余裕なら正（回復余地）。</summary>
        [Test]
        public void EquilibriumPull_収容力へ引き戻す()
        {
            // 人口120>収容100＝(100-120)×0.1×1=-2.0（妨げで減る）。
            Assert.AreEqual(-2.0f, MalthusianCheckRules.EquilibriumPull(120f, 100f, 1f), Eps);
            // 人口80<収容100＝(100-80)×0.1×1=+2.0（回復余地）。
            Assert.AreEqual(2.0f, MalthusianCheckRules.EquilibriumPull(80f, 100f, 1f), Eps);
        }

        /// <summary>チェック深刻度＝閾値超過を0..1へ正規化。</summary>
        [Test]
        public void CheckSeverity_閾値超過の深刻度()
        {
            Assert.AreEqual(0f, MalthusianCheckRules.CheckSeverity(0.7f, 0.8f), Eps);
            // over0.6/denom1.2=0.5。
            Assert.AreEqual(0.5f, MalthusianCheckRules.CheckSeverity(1.4f, 0.8f), Eps);
        }

        /// <summary>マルサス的危機判定＝食糧ストレス比が閾値超でtrue（負入力はクランプ）。</summary>
        [Test]
        public void IsMalthusianCrisis_閾値超で危機()
        {
            Assert.IsTrue(MalthusianCheckRules.IsMalthusianCrisis(1.4f, 1.2f));
            Assert.IsFalse(MalthusianCheckRules.IsMalthusianCrisis(1.0f, 1.2f));
            Assert.IsFalse(MalthusianCheckRules.IsMalthusianCrisis(-5f, 1.2f));
        }
    }
}
