using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 軍拡競争を固定する：脅威認知＝建艦超過分、対抗建艦は猜疑心が増幅、螺旋は猜疑心ありで拮抗からも
    /// 競り上がり（猜疑心ゼロなら自然軍縮）、経済圧迫は建艦率/経済規模、相互自制は双方の配当。
    /// 物語テスト＝対称螺旋では双方が貧しくなりながら相対優位は変わらない。クランプを担保。
    /// </summary>
    public class ArmsRaceRulesTests
    {
        private static readonly ArmsRaceParams P = ArmsRaceParams.Default;
        // 反応係数1/猜疑増幅1/疲労0.1/建艦上限10/圧迫係数1/自制配当0.5

        [Test]
        public void ThreatPerception_ExcessOnly_NegativeClamped()
        {
            Assert.AreEqual(2f, ArmsRaceRules.ThreatPerception(3f, 1f), 1e-5f); // 相手が2超過
            Assert.AreEqual(0f, ArmsRaceRules.ThreatPerception(1f, 3f), 1e-5f); // 自分が上＝脅威なし
            Assert.AreEqual(0f, ArmsRaceRules.ThreatPerception(2f, 2f), 1e-5f); // 拮抗＝脅威なし
            Assert.AreEqual(1f, ArmsRaceRules.ThreatPerception(1f, -5f), 1e-5f); // 負の率は0扱い
        }

        [Test]
        public void ReactionBuildRate_ParanoiaAmplifies_ClampedToMax()
        {
            Assert.AreEqual(2f, ArmsRaceRules.ReactionBuildRate(2f, 0f, P), 1e-5f);   // 等量で応える
            Assert.AreEqual(4f, ArmsRaceRules.ReactionBuildRate(2f, 1f, P), 1e-5f);   // 猜疑心最大＝2倍
            Assert.AreEqual(3f, ArmsRaceRules.ReactionBuildRate(2f, 0.5f, P), 1e-5f); // 中間
            Assert.AreEqual(10f, ArmsRaceRules.ReactionBuildRate(20f, 1f, P), 1e-5f); // 上限クランプ
            Assert.AreEqual(0f, ArmsRaceRules.ReactionBuildRate(-3f, 1f, P), 1e-5f);  // 負の脅威は0
        }

        [Test]
        public void SpiralTick_ParityEscalatesWithParanoia_DecaysWithout()
        {
            // 拮抗(1,1)でも猜疑心最大なら螺旋：脅威=0+1×1=1→反応2→ 1+(2−0.1)=2.9 が双方
            var (a, b) = ArmsRaceRules.SpiralTick(1f, 1f, 1f, 1f, 1f, P);
            Assert.AreEqual(2.9f, a, 1e-5f);
            Assert.AreEqual(2.9f, b, 1e-5f);

            // 猜疑心ゼロの拮抗＝脅威なし＝疲労項だけが効いて自然軍縮：1−0.1=0.9
            var (a0, b0) = ArmsRaceRules.SpiralTick(1f, 1f, 0f, 0f, 1f, P);
            Assert.AreEqual(0.9f, a0, 1e-5f);
            Assert.AreEqual(0.9f, b0, 1e-5f);

            // 非対称（猜疑心ゼロ）：劣勢側だけが超過分に反応して追い上げる
            var (low, high) = ArmsRaceRules.SpiralTick(1f, 3f, 0f, 0f, 1f, P);
            Assert.AreEqual(2.9f, low, 1e-5f);  // 1+(2−0.1)
            Assert.AreEqual(2.7f, high, 1e-5f); // 3+(0−0.3)
        }

        [Test]
        public void EconomicBurden_RatioOfEconomy_ZeroEconomyIsFullBurden()
        {
            Assert.AreEqual(0.5f, ArmsRaceRules.EconomicBurden(5f, 10f, P), 1e-5f);
            Assert.AreEqual(1f, ArmsRaceRules.EconomicBurden(20f, 10f, P), 1e-5f); // 上限1
            Assert.AreEqual(0f, ArmsRaceRules.EconomicBurden(0f, 10f, P), 1e-5f);  // 建艦なし＝無料
            Assert.AreEqual(1f, ArmsRaceRules.EconomicBurden(1f, 0f, P), 1e-5f);   // 経済なしの建艦＝全圧迫
            Assert.AreEqual(0f, ArmsRaceRules.EconomicBurden(0f, 0f, P), 1e-5f);
        }

        [Test]
        public void RelativeAdvantage_ShareForm()
        {
            Assert.AreEqual(0.5f, ArmsRaceRules.RelativeAdvantage(100f, 100f), 1e-5f); // 拮抗
            Assert.AreEqual(0.75f, ArmsRaceRules.RelativeAdvantage(300f, 100f), 1e-5f);
            Assert.AreEqual(0f, ArmsRaceRules.RelativeAdvantage(0f, 100f), 1e-5f);
            Assert.AreEqual(0.5f, ArmsRaceRules.RelativeAdvantage(0f, 0f), 1e-5f);     // 双方ゼロ＝拮抗
        }

        [Test]
        public void MutualRestraintGain_MinRateDividend()
        {
            // 相対優位を崩さず削れるのは低い方まで：min(4,6)×0.5=2 が各陣営の配当
            Assert.AreEqual(2f, ArmsRaceRules.MutualRestraintGain(4f, 6f, P), 1e-5f);
            Assert.AreEqual(2f, ArmsRaceRules.MutualRestraintGain(6f, 4f, P), 1e-5f); // 対称
            Assert.AreEqual(0f, ArmsRaceRules.MutualRestraintGain(0f, 6f, P), 1e-5f); // 片方丸腰＝配当なし
        }

        [Test]
        public void Story_SymmetricSpiral_BothImpoverished_AdvantageUnchanged()
        {
            // 安全保障のジレンマ：拮抗した両国が猜疑心で軍拡螺旋に入ると、
            // 双方の経済圧迫だけが膨らみ、相対優位は最初から最後まで動かない。
            float rateA = 1f, rateB = 1f;
            float totalA = 100f, totalB = 100f;
            const float economy = 100f;
            float burdenStart = ArmsRaceRules.EconomicBurden(rateA, economy, P); // 0.01

            for (int i = 0; i < 20; i++)
            {
                (rateA, rateB) = ArmsRaceRules.SpiralTick(rateA, rateB, 1f, 1f, 1f, P);
                totalA += rateA;
                totalB += rateB;
                // 螺旋のどの時点でも拮抗のまま
                Assert.AreEqual(0.5f, ArmsRaceRules.RelativeAdvantage(totalA, totalB), 1e-5f);
            }

            // 建艦率は上限まで競り上がり…
            Assert.AreEqual(P.maxBuildRate, rateA, 1e-5f);
            Assert.AreEqual(P.maxBuildRate, rateB, 1e-5f);
            // …経済圧迫は当初の10倍（0.01→0.1）＝双方が貧しくなった
            float burdenEnd = ArmsRaceRules.EconomicBurden(rateA, economy, P);
            Assert.AreEqual(0.1f, burdenEnd, 1e-5f);
            Assert.Greater(burdenEnd, burdenStart);
            // それでも優位は不変＝得たものはない。相互自制なら双方 5 の配当が浮いていた
            Assert.AreEqual(0.5f, ArmsRaceRules.RelativeAdvantage(totalA, totalB), 1e-5f);
            Assert.AreEqual(5f, ArmsRaceRules.MutualRestraintGain(rateA, rateB, P), 1e-5f);
        }
    }
}
