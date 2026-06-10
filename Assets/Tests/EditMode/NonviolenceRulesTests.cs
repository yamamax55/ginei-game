using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 非暴力＝道徳の柔術（#831/#832/#836）を固定する：可視化された弾圧が支持を上げ（柔術）、
    /// 不可視の弾圧は転換せず、支持の閾値突破で勝利、統治体へ非協力として波及。
    /// </summary>
    public class NonviolenceRulesTests
    {
        private static readonly NonviolenceParams P = NonviolenceParams.Default; // coef 0.5 / triumph 0.6

        [Test]
        public void VisibleRepression_RaisesSupport_MoralJiujitsu()
        {
            var m = new Movement(1, Faction.同盟, support: 0.1f, commitment: 0.5f);
            float shift = NonviolenceRules.Repress(m, brutality: 1f, mediaReach: 1f, P);
            Assert.AreEqual(0.25f, shift, 1e-4f);   // 1×1×0.5×0.5
            Assert.AreEqual(0.35f, m.support, 1e-4f);
        }

        [Test]
        public void InvisibleRepression_DoesNotConvert()
        {
            var m = new Movement(1, Faction.同盟, support: 0.3f, commitment: 0.8f);
            NonviolenceRules.Repress(m, brutality: 1f, mediaReach: 0f, P); // 隠れた弾圧は世論に届かない
            Assert.AreEqual(0.3f, m.support, 1e-4f);
        }

        [Test]
        public void RepeatedRepression_LeadsToTriumph()
        {
            var m = new Movement(1, Faction.同盟, support: 0.1f, commitment: 0.5f);
            Assert.IsFalse(NonviolenceRules.IsTriumphant(m, P));
            NonviolenceRules.Repress(m, 1f, 1f, P); // 0.35
            NonviolenceRules.Repress(m, 1f, 1f, P); // 0.60
            Assert.IsTrue(NonviolenceRules.IsTriumphant(m, P)); // 弾圧を重ねるほど運動が勝つ
        }

        [Test]
        public void LowCommitment_ConvertsLittle()
        {
            var m = new Movement(1, Faction.同盟, support: 0.1f, commitment: 0.1f);
            float shift = NonviolenceRules.Repress(m, 1f, 1f, P);
            Assert.AreEqual(0.05f, shift, 1e-4f); // 1×1×0.1×0.5
        }

        [Test]
        public void PressurePolity_WithdrawsCooperation()
        {
            var m = new Movement(1, Faction.同盟, support: 0.6f);
            var polity = new Polity(1, Faction.帝国, 1000000, 10000, cooperation: 1f);
            NonviolenceRules.PressurePolity(m, polity, 0.5f); // 0.6×0.5=0.3 引き下げ
            Assert.AreEqual(0.7f, polity.cooperation, 1e-4f);
        }
    }
}
