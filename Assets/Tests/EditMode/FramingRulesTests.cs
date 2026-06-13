using NUnit.Framework;

namespace Ginei.Tests
{
    /// <summary>フレーミング効果（KAHN-4 #1840）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class FramingRulesTests
    {
        const float Eps = 0.0001f;

        /// <summary>利得フレーム＝事実×(1+0.6)。確実な結果ほど魅力が増す（リスク回避を誘う）。</summary>
        [Test]
        public void GainFrameAppeal_利得提示で魅力が増す()
        {
            // 0.5 × 1.6 = 0.8
            Assert.AreEqual(0.8f, FramingRules.GainFrameAppeal(0.5f), Eps);
            // クランプ（1.6倍が1を超えても0..1）
            Assert.AreEqual(1f, FramingRules.GainFrameAppeal(0.9f), Eps);
            Assert.AreEqual(0f, FramingRules.GainFrameAppeal(0f), Eps);
        }

        /// <summary>損失フレーム＝事実×(1−0.6)。同じ事実が割り引かれる（リスク選好を誘う）。</summary>
        [Test]
        public void LossFrameAppeal_損失提示で魅力が下がる()
        {
            // 0.5 × 0.4 = 0.2
            Assert.AreEqual(0.2f, FramingRules.LossFrameAppeal(0.5f), Eps);
            Assert.AreEqual(0f, FramingRules.LossFrameAppeal(0f), Eps);
            // 同じ事実でも利得フレームより低く出る＝反転の源
            Assert.Less(FramingRules.LossFrameAppeal(0.5f), FramingRules.GainFrameAppeal(0.5f));
        }

        /// <summary>フレーミング・シフト＝利得魅力と損失魅力の差の絶対値。</summary>
        [Test]
        public void FramingShift_同一事実でも枠組みで選好が振れる()
        {
            // |0.8 - 0.2| = 0.6
            Assert.AreEqual(0.6f, FramingRules.FramingShift(0.8f, 0.2f), Eps);
            // 差がなければシフトゼロ（合理的）
            Assert.AreEqual(0f, FramingRules.FramingShift(0.5f, 0.5f), Eps);
        }

        /// <summary>言い回し＝肯定性が0.5を境に評価を±0.3ずらす。「90%成功」と「10%失敗」で評価が変わる。</summary>
        [Test]
        public void PositiveNegativePhrasing_言い回しが評価をずらす()
        {
            // 完全肯定: 0.5 + (1-0.5)*2*0.3 = 0.5+0.3 = 0.8
            Assert.AreEqual(0.8f, FramingRules.PositiveNegativePhrasing(0.5f, 1f), Eps);
            // 完全否定: 0.5 - 0.3 = 0.2
            Assert.AreEqual(0.2f, FramingRules.PositiveNegativePhrasing(0.5f, 0f), Eps);
            // 中立(0.5)はずれなし
            Assert.AreEqual(0.5f, FramingRules.PositiveNegativePhrasing(0.5f, 0.5f), Eps);
        }

        /// <summary>選好反転＝利得フレームと損失フレームで選択が食い違えば true。</summary>
        [Test]
        public void PreferenceReversal_フレームで結論が変われば反転()
        {
            Assert.IsTrue(FramingRules.PreferenceReversal(true, false));
            Assert.IsTrue(FramingRules.PreferenceReversal(false, true));
            Assert.IsFalse(FramingRules.PreferenceReversal(true, true));
        }

        /// <summary>フレーム効力＝被影響度×枠組みの強さ。</summary>
        [Test]
        public void FrameEffectiveness_被影響な受け手に強い枠組みほど効く()
        {
            // 0.8 × 0.5 = 0.4
            Assert.AreEqual(0.4f, FramingRules.FrameEffectiveness(0.8f, 0.5f), Eps);
            // 受け手が無影響なら効力ゼロ
            Assert.AreEqual(0f, FramingRules.FrameEffectiveness(0f, 1f), Eps);
        }

        /// <summary>抵抗＝(計数+熟慮)/2 × 0.8。熟慮(System2)が立つほどフレーミングに抗う。</summary>
        [Test]
        public void FramingResistance_計数と熟慮が枠組みに抗う()
        {
            // (0.5+0.5)/2 × 0.8 = 0.4
            Assert.AreEqual(0.4f, FramingRules.FramingResistance(0.5f, 0.5f), Eps);
            // 計数も熟慮も最大なら抵抗は0.8（既定上限）
            Assert.AreEqual(0.8f, FramingRules.FramingResistance(1f, 1f), Eps);
            // どちらもゼロなら抵抗なし
            Assert.AreEqual(0f, FramingRules.FramingResistance(0f, 0f), Eps);
        }

        /// <summary>正味影響＝効力×(1−抵抗)。熟慮が削る。</summary>
        [Test]
        public void NetFramingInfluence_抵抗が効力を削る()
        {
            // 0.4 × (1-0.4) = 0.24
            Assert.AreEqual(0.24f, FramingRules.NetFramingInfluence(0.4f, 0.4f), Eps);
            // 抵抗最大なら正味ゼロ
            Assert.AreEqual(0f, FramingRules.NetFramingInfluence(1f, 1f), Eps);
        }

        /// <summary>
        /// 物語＝同一事実が利得/損失フレームで選好を反転させるが、熟慮(System2)がそれに抗う。
        /// 「90%生存」と「10%死亡」は同じ事実なのに、枠組みだけで結論が変わる。だが数字を吟味し
        /// 熟慮する受け手では正味の影響が削られ、フレーム駆動でなくなる。
        /// </summary>
        [Test]
        public void 物語_フレームが選好を反転させるが熟慮が抗う()
        {
            // 同一事実(0.5)を二つの枠組みで提示すると魅力が大きく食い違う
            float gain = FramingRules.GainFrameAppeal(0.5f); // 0.8（安全策が魅力的）
            float loss = FramingRules.LossFrameAppeal(0.5f); // 0.2（博打が魅力的）
            Assert.Greater(FramingRules.FramingShift(gain, loss), 0.5f, "同一事実でも選好が大きく振れる");

            // 安直な受け手（被影響大・枠組み強・熟慮ほぼなし）はフレーム駆動になる
            float effNaive = FramingRules.FrameEffectiveness(0.9f, 0.9f);
            float resNaive = FramingRules.FramingResistance(0.1f, 0.1f);
            float netNaive = FramingRules.NetFramingInfluence(effNaive, resNaive);
            Assert.IsTrue(FramingRules.IsFramingDriven(netNaive, 0.5f), "熟慮なき受け手は枠組みに流される");

            // 同じ枠組みでも、計数能力と熟慮(System2)が高い受け手は正味影響が薄まりフレーム駆動でなくなる
            float resWise = FramingRules.FramingResistance(1f, 1f);
            float netWise = FramingRules.NetFramingInfluence(effNaive, resWise);
            Assert.Less(netWise, netNaive, "熟慮はフレーミングの影響を削る");
            Assert.IsFalse(FramingRules.IsFramingDriven(netWise, 0.5f), "熟慮する受け手は枠組みに流されない");
        }
    }
}
