using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 野心（ロイエンタール型）を固定する：野心は功績×低い天井で育つ（行き場のない実力）、猜疑は
    /// 野心の匂い×讒言で蓄積、相互増幅は積の共鳴（どちらか0なら回らない・回ると加速＝自己成就予言）、
    /// 反逆圧力は野心だけでは0＝猜疑との共鳴と「追い込まれ」が引き金、解く手は残る猜疑で比較。
    /// 境界とパラメータクランプを担保。
    /// </summary>
    public class AmbitionRulesTests
    {
        private static readonly AmbitionParams P = AmbitionParams.Default;
        // 野心成長0.5/猜疑率0.1/讒言増幅1/共鳴利得0.2/自発重み0.4/追込重み0.6/信頼可視化0.6/人質0.3/名誉職0.45

        [Test]
        public void AmbitionGrowth_TalentWithoutOutletBecomesAmbition()
        {
            // 「行き場のない実力が野心になる」＝功績大×天井低で最大成長。
            Assert.AreEqual(0.5f, AmbitionRules.AmbitionGrowth(0f, 1f, 0f, P), 1e-5f);
            // 天井が開いていれば（ceiling=1）功績は野心にならない。
            Assert.AreEqual(0f, AmbitionRules.AmbitionGrowth(0f, 1f, 1f, P), 1e-5f);
            // 功績0＝凡庸は野心も持てない。
            Assert.AreEqual(0f, AmbitionRules.AmbitionGrowth(0f, 0f, 0f, P), 1e-5f);
            // 半端な天井は半減。1で頭打ち。負入力はクランプ。
            Assert.AreEqual(0.25f, AmbitionRules.AmbitionGrowth(0f, 1f, 0.5f, P), 1e-5f);
            Assert.AreEqual(1f, AmbitionRules.AmbitionGrowth(0.9f, 1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.5f, AmbitionRules.AmbitionGrowth(-1f, 1f, -1f, P), 1e-5f);
        }

        [Test]
        public void SuspicionTick_AmbitionScentTimesWhispers()
        {
            // 野心の匂いだけでも猜疑はにじむ（讒言なし）。
            Assert.AreEqual(0.05f, AmbitionRules.SuspicionTick(0f, 0.5f, 0f, 1f, P), 1e-5f);
            // 讒言（courtWhispers=1）は蓄積を2倍に増幅する。
            Assert.AreEqual(0.1f, AmbitionRules.SuspicionTick(0f, 0.5f, 1f, 1f, P), 1e-5f);
            // 匂いがなければ（ambition=0）讒言だけでは刺さらない。
            Assert.AreEqual(0f, AmbitionRules.SuspicionTick(0f, 0f, 1f, 10f, P), 1e-5f);
            // 1で頭打ち。負の dt は進まない。
            Assert.AreEqual(1f, AmbitionRules.SuspicionTick(0.9f, 1f, 1f, 10f, P), 1e-5f);
            Assert.AreEqual(0.3f, AmbitionRules.SuspicionTick(0.3f, 1f, 1f, -1f, P), 1e-5f);
        }

        [Test]
        public void SpiralFeedback_RequiresBothToResonate()
        {
            // 積の共鳴＝どちらか一方が0ならスパイラルは回らない。
            Assert.AreEqual(0f, AmbitionRules.SpiralFeedback(1f, 0f, P), 1e-5f);  // 野心だけでは破局しない
            Assert.AreEqual(0f, AmbitionRules.SpiralFeedback(0f, 1f, P), 1e-5f);  // 猜疑だけでも破局しない
            Assert.AreEqual(0.2f, AmbitionRules.SpiralFeedback(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.05f, AmbitionRules.SpiralFeedback(0.5f, 0.5f, P), 1e-5f);
            Assert.AreEqual(0.2f, AmbitionRules.SpiralFeedback(2f, 2f, P), 1e-5f); // 過大入力はクランプ
        }

        [Test]
        public void SpiralFeedback_SelfFulfillingProphecyAccelerates()
        {
            // 自己成就予言＝疑われた実力者は力を蓄え、それがさらに疑いを呼ぶ。反復で双方が育ち、共鳴量は加速する。
            float ambition = 0.5f, suspicion = 0.3f;
            float fb1 = AmbitionRules.SpiralFeedback(ambition, suspicion, P);
            Assert.AreEqual(0.03f, fb1, 1e-5f); // 0.2×0.5×0.3
            ambition += fb1; suspicion += fb1;
            float fb2 = AmbitionRules.SpiralFeedback(ambition, suspicion, P);
            Assert.Greater(fb2, fb1); // 回るほど増幅が強まる＝加速
            ambition += fb2; suspicion += fb2;
            Assert.Greater(ambition, 0.5f);
            Assert.Greater(suspicion, 0.3f);
        }

        [Test]
        public void RebellionPressure_ResonanceNotAmbitionAlone_CorneredIsTheTrigger()
        {
            // 「反逆は野心の結果ではなく、猜疑との共鳴の結果」＝野心が最大でも猜疑0なら圧力0。
            Assert.AreEqual(0f, AmbitionRules.RebellionPressure(1f, 0f, false, P), 1e-5f);
            // 自発成分の最大は0.4＝野心と猜疑が極まっても、それだけでは立たない。
            Assert.AreEqual(0.4f, AmbitionRules.RebellionPressure(1f, 1f, false, P), 1e-5f);
            // 「追い込まれ」が引き金＝中程度の野心・猜疑でも追い込まれれば自発最大を超える。
            float cornered = AmbitionRules.RebellionPressure(0.5f, 0.5f, true, P);
            Assert.AreEqual(0.7f, cornered, 1e-5f); // 0.4×0.25＋0.6
            Assert.Greater(cornered, AmbitionRules.RebellionPressure(1f, 1f, false, P));
            // 全条件が揃えば1で飽和。
            Assert.AreEqual(1f, AmbitionRules.RebellionPressure(1f, 1f, true, P), 1e-5f);
        }

        [Test]
        public void DefusionOptions_TrustVisibilityIsDeepestRemedy()
        {
            var d = AmbitionRules.DefusionOptions(0.8f, P);
            Assert.AreEqual(0.32f, d.trustVisibility, 1e-5f);  // 信頼の可視化＝最も深く解く
            Assert.AreEqual(0.56f, d.hostage, 1e-5f);          // 人質＝浅い（不信の制度化）
            Assert.AreEqual(0.44f, d.honoraryTransfer, 1e-5f); // 名誉職転出＝中庸（ただし天井↓＝野心再燃の種）
            Assert.AreEqual(0.32f, d.Best, 1e-5f);
            // 猜疑0なら解くものもない。
            var none = AmbitionRules.DefusionOptions(0f, P);
            Assert.AreEqual(0f, none.Best, 1e-5f);
        }

        [Test]
        public void Params_CtorClampsToValidRange()
        {
            var p = new AmbitionParams(-1f, -1f, -1f, -1f, -1f, 2f, 2f, -1f, 2f);
            Assert.AreEqual(0f, p.ambitionGrowthRate, 1e-5f);
            Assert.AreEqual(0f, p.suspicionRate, 1e-5f);
            Assert.AreEqual(0f, p.whisperAmplify, 1e-5f);
            Assert.AreEqual(0f, p.spiralGain, 1e-5f);
            Assert.AreEqual(0f, p.voluntaryWeight, 1e-5f);
            Assert.AreEqual(1f, p.corneredWeight, 1e-5f);
            Assert.AreEqual(1f, p.trustVisibilityFactor, 1e-5f);
            Assert.AreEqual(0f, p.hostageFactor, 1e-5f);
            Assert.AreEqual(1f, p.honoraryTransferFactor, 1e-5f);
            // 係数0なら野心も猜疑も育たず、スパイラルも回らない（安全側）。
            Assert.AreEqual(0f, AmbitionRules.AmbitionGrowth(0f, 1f, 0f, p), 1e-5f);
            Assert.AreEqual(0f, AmbitionRules.SuspicionTick(0f, 1f, 1f, 10f, p), 1e-5f);
            Assert.AreEqual(0f, AmbitionRules.SpiralFeedback(1f, 1f, p), 1e-5f);
        }
    }
}
