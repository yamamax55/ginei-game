using NUnit.Framework;
using UnityEngine;

namespace Ginei.Tests
{
    /// <summary>斜行陣（片翼拒否＝refused flank）の純ロジックを既定Paramsの具体値で固定するテスト。</summary>
    public class RefusedFlankRulesTests
    {
        const float Eps = 0.0001f;
        const float PowEps = 0.001f;

        /// <summary>集中翼戦力＝総 × Lerp(0.5, 0.65, 集中度)。0で均等・1で偏重。</summary>
        [Test]
        public void MassedWingStrength_集中度で配分が偏る()
        {
            Assert.AreEqual(50f, RefusedFlankRules.MassedWingStrength(100f, 0f), Eps);  // 均等
            Assert.AreEqual(65f, RefusedFlankRules.MassedWingStrength(100f, 1f), Eps);  // 上限偏重
            Assert.AreEqual(57.5f, RefusedFlankRules.MassedWingStrength(100f, 0.5f), Eps); // 0.5+0.15*0.5=0.575
        }

        /// <summary>退げた翼の遅延＝撤退度 × 1.2 ÷ (1+敵前進)。大きく退げ敵が遅いほど猶予が増える。</summary>
        [Test]
        public void RefusedWingDelay_退げるほど猶予が増える()
        {
            Assert.AreEqual(0.6f, RefusedFlankRules.RefusedWingDelay(1f, 1f), Eps);   // 1*1.2/2
            Assert.AreEqual(1.2f, RefusedFlankRules.RefusedWingDelay(1f, 0f), Eps);   // 1*1.2/1
            Assert.AreEqual(0f, RefusedFlankRules.RefusedWingDelay(0f, 0f), Eps);     // 退げなければ猶予なし
        }

        /// <summary>集中翼の打撃＝pow(集中翼/(敵翼+1), 0.5)。局所優勢が打撃に直結。</summary>
        [Test]
        public void DecisiveWingImpact_局所優勢で打撃が増す()
        {
            // 65/36 = 1.80556, sqrt = 1.34371
            Assert.AreEqual(1.34371f, RefusedFlankRules.DecisiveWingImpact(65f, 35f), PowEps);
            // 62/31 = 2, sqrt = 1.41421
            Assert.AreEqual(1.41421f, RefusedFlankRules.DecisiveWingImpact(62f, 30f), PowEps);
        }

        /// <summary>時間差の度合い＝打撃 × (1+遅延)。退げた翼の遅延だけ決着が早まる。</summary>
        [Test]
        public void EchelonTiming_遅延が決着を早める()
        {
            Assert.AreEqual(3.2f, RefusedFlankRules.EchelonTiming(2f, 0.6f), Eps); // 2*1.6
            Assert.AreEqual(2f, RefusedFlankRules.EchelonTiming(2f, 0f), Eps);     // 遅延なしは素のまま
        }

        /// <summary>退げた翼の脆弱性＝敵圧力 × 0.8 ÷ (戦力+1)。薄いほど崩されやすい。</summary>
        [Test]
        public void RefusedWingVulnerability_薄い翼ほど崩れやすい()
        {
            Assert.AreEqual(0.4f, RefusedFlankRules.RefusedWingVulnerability(9f, 5f), Eps); // 5*0.8/10
            Assert.AreEqual(1f, RefusedFlankRules.RefusedWingVulnerability(0f, 100f), Eps); // クランプ上限
        }

        /// <summary>巻き取り効果＝打撃 × (1−結束) × 0.7。敵結束が低いほど横へ崩れる。</summary>
        [Test]
        public void RollUpEffect_敵結束が低いと戦線を巻き取る()
        {
            Assert.AreEqual(1.4f, RefusedFlankRules.RollUpEffect(2f, 0f), Eps);  // 2*1*0.7
            Assert.AreEqual(0f, RefusedFlankRules.RollUpEffect(2f, 1f), Eps);    // 固い戦線は巻き取れない
        }

        /// <summary>正味の利得＝時間差 × (1+巻取り)。判定は閾値超え。</summary>
        [Test]
        public void ObliqueAdvantage_時間差と巻取りの相乗()
        {
            Assert.AreEqual(4.5f, RefusedFlankRules.ObliqueAdvantage(3f, 0.5f), Eps); // 3*1.5
            Assert.IsTrue(RefusedFlankRules.IsObliqueSuccessful(3f, 2f));
            Assert.IsFalse(RefusedFlankRules.IsObliqueSuccessful(1f, 2f));
        }

        /// <summary>
        /// 物語：一翼に戦力を集中して敵一角を破り戦線を巻き取れば成功。
        /// だが退げた翼が薄く退きもせず先に崩れれば（時間差が稼げず）斜行陣は失敗する。
        /// </summary>
        [Test]
        public void Story_集中翼が先に決着すれば成功_退げた翼が先に崩れれば失敗()
        {
            const float total = 100f;
            const float threshold = 1f;

            // 成功路線：高集中(0.8)＝集中翼62/退げた翼38、よく退げて(0.8)敵前進遅い(0.5)
            float massedHi = RefusedFlankRules.MassedWingStrength(total, 0.8f); // 62
            float impactHi = RefusedFlankRules.DecisiveWingImpact(massedHi, 30f); // sqrt(2)=1.41421
            float delayHi = RefusedFlankRules.RefusedWingDelay(0.8f, 0.5f); // 0.8*1.2/1.5=0.64
            float echelonHi = RefusedFlankRules.EchelonTiming(impactHi, delayHi); // 1.41421*1.64=2.31931
            Assert.IsTrue(RefusedFlankRules.IsObliqueSuccessful(echelonHi, threshold),
                "集中翼が先に決着＝斜行陣は決まる");

            // 退げた翼が薄く(38)敵の強圧で脆弱＝崩される危険が高い
            float vuln = RefusedFlankRules.RefusedWingVulnerability(total - massedHi, 60f); // 60*0.8/39=1.23->clamp1
            Assert.Greater(vuln, 0.5f, "薄い翼は崩されやすい");

            // 巻き取り：敵結束が低ければ一角崩壊が横へ広がる
            float rollUp = RefusedFlankRules.RollUpEffect(impactHi, 0.2f);
            float advHi = RefusedFlankRules.ObliqueAdvantage(echelonHi, rollUp);

            // 失敗路線：退げず(0.1)敵前進速い(2)＝遅延わずか、集中も控えめ＝決着が間に合わない
            float massedLo = RefusedFlankRules.MassedWingStrength(total, 0.1f); // 0.5+0.15*0.1=0.515 ->51.5
            float impactLo = RefusedFlankRules.DecisiveWingImpact(massedLo, 200f); // 51.5/201<<1
            float delayLo = RefusedFlankRules.RefusedWingDelay(0.1f, 2f); // 0.1*1.2/3=0.04
            float echelonLo = RefusedFlankRules.EchelonTiming(impactLo, delayLo);
            Assert.IsFalse(RefusedFlankRules.IsObliqueSuccessful(echelonLo, threshold),
                "退げた翼が稼げず集中も足りなければ斜行陣は失敗");

            // 成功路線の正味利得は失敗路線を上回る
            float rollUpLo = RefusedFlankRules.RollUpEffect(impactLo, 0.2f);
            float advLo = RefusedFlankRules.ObliqueAdvantage(echelonLo, rollUpLo);
            Assert.Greater(advHi, advLo, "決まった斜行陣の利得が勝る");
        }
    }
}
