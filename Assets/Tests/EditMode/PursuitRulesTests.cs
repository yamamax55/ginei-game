using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 追撃戦・殿軍を固定する：速度比で振り切り、苛烈度は追撃側の速度優位に比例、殿軍は本隊の2割で
    /// 最大遮蔽に達し本隊損害を細らせるが自身は重い犠牲を払う。境界・クランプを担保。
    /// </summary>
    public class PursuitRulesTests
    {
        private static readonly PursuitParams P = PursuitParams.Default;
        // 基礎損害0.3/殿軍遮蔽0.8/殿軍損害0.5/振り切り比1.2

        [Test]
        public void CleanBreak_BySpeedRatio()
        {
            Assert.IsTrue(PursuitRules.CleanBreak(12f, 10f, P));   // 比1.2＝ちょうど振り切り
            Assert.IsFalse(PursuitRules.CleanBreak(11f, 10f, P));
            Assert.IsTrue(PursuitRules.CleanBreak(5f, 0f, P));     // 追撃側が動けない＝逃げ切り
        }

        [Test]
        public void PursuitSeverity_ProportionalToSpeedAdvantage()
        {
            Assert.AreEqual(0f, PursuitRules.PursuitSeverity(10f, 10f, P), 1e-5f);   // 同速＝噛みつけない
            Assert.AreEqual(0.5f, PursuitRules.PursuitSeverity(10f, 15f, P), 1e-5f); // 1.5倍速＝0.5
            Assert.AreEqual(1f, PursuitRules.PursuitSeverity(10f, 20f, P), 1e-5f);   // 倍速＝最大
            Assert.AreEqual(1f, PursuitRules.PursuitSeverity(10f, 50f, P), 1e-5f);   // 上限1
            // 振り切り成立なら苛烈度0
            Assert.AreEqual(0f, PursuitRules.PursuitSeverity(12f, 10f, P), 1e-5f);
        }

        [Test]
        public void RearguardScreen_MaxAtOneFifthOfMainBody()
        {
            // 本隊1000 の2割=200 で最大遮蔽0.8
            Assert.AreEqual(0.8f, PursuitRules.RearguardScreen(200f, 1000f, P), 1e-5f);
            // 半分の殿軍＝半分の遮蔽
            Assert.AreEqual(0.4f, PursuitRules.RearguardScreen(100f, 1000f, P), 1e-5f);
            // 厚すぎても遮蔽は伸びない
            Assert.AreEqual(0.8f, PursuitRules.RearguardScreen(500f, 1000f, P), 1e-5f);
            // 殿軍なし＝遮蔽なし
            Assert.AreEqual(0f, PursuitRules.RearguardScreen(0f, 1000f, P), 1e-5f);
        }

        [Test]
        public void MainBodyLosses_CutByScreen()
        {
            // 遮蔽なし・苛烈度1＝1000×0.3=300
            Assert.AreEqual(300f, PursuitRules.MainBodyLosses(1000f, 1f, 0f, P), 1e-4f);
            // 最大遮蔽0.8＝損害は2割に細る＝60
            Assert.AreEqual(60f, PursuitRules.MainBodyLosses(1000f, 1f, 0.8f, P), 1e-4f);
            // 苛烈度0＝損害なし
            Assert.AreEqual(0f, PursuitRules.MainBodyLosses(1000f, 0f, 0f, P), 1e-5f);
        }

        [Test]
        public void RearguardLosses_HeavierThanMainBodyRate()
        {
            // 殿軍200×0.5×苛烈度1=100＝半減する犠牲
            Assert.AreEqual(100f, PursuitRules.RearguardLosses(200f, 1f, P), 1e-4f);
            // 追撃が来なければ犠牲なし
            Assert.AreEqual(0f, PursuitRules.RearguardLosses(200f, 0f, P), 1e-5f);
            // 殿軍の損害率(0.5) > 本隊の基礎損害率(0.3)＝殿軍は殴られ役
            Assert.Greater(P.rearguardCasualtyRate, P.basePursuitLossRatio);
        }

        [Test]
        public void Tradeoff_RearguardSavesMoreThanItCosts()
        {
            // 本隊1000・殿軍200・苛烈度1：殿軍なし損害300 vs 殿軍あり(本隊60+殿軍100)=160＝犠牲は引き合う
            float without = PursuitRules.MainBodyLosses(1000f, 1f, 0f, P);
            float screen = PursuitRules.RearguardScreen(200f, 1000f, P);
            float with_ = PursuitRules.MainBodyLosses(1000f, 1f, screen, P) + PursuitRules.RearguardLosses(200f, 1f, P);
            Assert.Less(with_, without);
        }
    }
}
