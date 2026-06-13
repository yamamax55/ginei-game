using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    public class ManeuverEnvelopmentRulesTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void 回り込みは速いほど近いほど速く進む()
        {
            // gain = (10*0.5)/5 = 1.0 → 0+1.0 = 1.0（クランプ上限）
            Assert.AreEqual(1.0f, ManeuverEnvelopmentRules.EnvelopmentProgress(0f, 10f, 5f, 0.5f), Tol);
            // gain = (4*0.5)/8 = 0.25 → 0.2+0.25 = 0.45
            Assert.AreEqual(0.45f, ManeuverEnvelopmentRules.EnvelopmentProgress(0.2f, 4f, 8f, 0.5f), Tol);
        }

        [Test]
        public void 進捗が進むほど側背面が晒される()
        {
            Assert.AreEqual(0.6f, ManeuverEnvelopmentRules.FlankExposure(0.5f), Tol); // 0.5*1.2
            Assert.AreEqual(1.0f, ManeuverEnvelopmentRules.FlankExposure(0.9f), Tol); // 0.9*1.2=1.08→clamp
        }

        [Test]
        public void 側背面奪取で敵火力を封じる()
        {
            Assert.AreEqual(0.51f, ManeuverEnvelopmentRules.FirepowerDenial(0.6f), Tol); // 0.6*0.85
        }

        [Test]
        public void 包囲圧力は進捗と包む側の兵力割合の積()
        {
            Assert.AreEqual(0.6f, ManeuverEnvelopmentRules.EncirclementPressure(0.8f, 0.75f), Tol); // 0.8*0.75
        }

        [Test]
        public void 圧力が高いほど突破は難しく機動が高いほど脱出しやすい()
        {
            // capacity = 0.5 + 0.5*0.4 = 0.7 ; 0.7*(1-0.6) = 0.28
            Assert.AreEqual(0.28f, ManeuverEnvelopmentRules.BreakoutChance(0.6f, 0.5f), Tol);
            // 圧力0なら capacity そのまま
            Assert.AreEqual(0.7f, ManeuverEnvelopmentRules.BreakoutChance(0f, 0.5f), Tol);
        }

        [Test]
        public void 回り込みが長く敵が速いほど裏をかかれるリスクが高い()
        {
            Assert.AreEqual(0.4f, ManeuverEnvelopmentRules.CounterManeuverRisk(2f, 0.4f), Tol); // 2*0.4*0.5
        }

        [Test]
        public void 包囲の正味利得は封殺からリスクを差し引く()
        {
            Assert.AreEqual(0.11f, ManeuverEnvelopmentRules.EnvelopmentAdvantage(0.51f, 0.4f), Tol); // 0.51-0.4
            // リスクが利得を上回れば負はゼロでクランプ
            Assert.AreEqual(0.0f, ManeuverEnvelopmentRules.EnvelopmentAdvantage(0.3f, 0.5f), Tol);
        }

        [Test]
        public void 進捗がしきい値以上で包囲成立()
        {
            Assert.IsTrue(ManeuverEnvelopmentRules.IsEnveloped(0.7f));  // 既定しきい値0.7
            Assert.IsFalse(ManeuverEnvelopmentRules.IsEnveloped(0.6f));
        }

        [Test]
        public void 機動で側背面を取れば火力を封じるが敵が速いと裏をかかれる()
        {
            // 包囲が深く進む（進捗0.8）→側背面が大きく晒され敵火力を強く封じる
            float exposure = ManeuverEnvelopmentRules.FlankExposure(0.8f); // 0.8*1.2=0.96
            float denial = ManeuverEnvelopmentRules.FirepowerDenial(exposure); // 0.96*0.85=0.816
            Assert.AreEqual(0.816f, denial, Tol);

            // 敵の反応が鈍ければ（時間1・反応0.2）リスク小＝包囲は大きな正味利得を生む
            float slowRisk = ManeuverEnvelopmentRules.CounterManeuverRisk(1f, 0.2f); // 1*0.2*0.5=0.1
            float advSlow = ManeuverEnvelopmentRules.EnvelopmentAdvantage(denial, slowRisk); // 0.816-0.1
            Assert.AreEqual(0.716f, advSlow, Tol);

            // 同じ包囲でも敵が速く長くかかれば（時間3・反応0.6）裏をかかれ利得が消える
            float fastRisk = ManeuverEnvelopmentRules.CounterManeuverRisk(3f, 0.6f); // 3*0.6*0.5=0.9
            float advFast = ManeuverEnvelopmentRules.EnvelopmentAdvantage(denial, fastRisk); // 0.816-0.9→0
            Assert.AreEqual(0.0f, advFast, Tol);

            Assert.Greater(advSlow, advFast);
            Assert.IsTrue(ManeuverEnvelopmentRules.IsEnveloped(0.8f)); // 0.8≥0.7 で包囲成立
        }
    }
}
