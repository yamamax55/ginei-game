using NUnit.Framework;
using UnityEngine;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 恒星放射環境を固定する：放射線強度（距離の二乗近似で減衰）、遮蔽の防護率、電子機器障害、
    /// センサーノイズ、乗員被曝（時間蓄積）、総合性能低下（独立故障の合成）、逆光戦術、作戦可否。
    /// 既定 Params の期待値を手計算で固定し境界を担保。
    /// </summary>
    public class StellarRadiationRulesTests
    {
        private static readonly StellarRadiationParams P = StellarRadiationParams.Default;
        // 基準距離1・下限0.5・障害感度1・ノイズ感度0.8・被曝率0.1・逆光利得0.5・不能閾値0.7

        [Test]
        public void RadiationIntensity_FallsOffWithDistanceSquared()
        {
            // 出力1・距離1＝基準＝満タン
            Assert.AreEqual(1f, StellarRadiationRules.RadiationIntensity(1f, 1f, P), 1e-3f);
            // 距離2倍＝二乗で1/4
            Assert.AreEqual(0.25f, StellarRadiationRules.RadiationIntensity(1f, 2f, P), 1e-3f);
            // 出力半分＝強度半分
            Assert.AreEqual(0.5f, StellarRadiationRules.RadiationIntensity(0.5f, 1f, P), 1e-3f);
            // 出力0＝放射線なし
            Assert.AreEqual(0f, StellarRadiationRules.RadiationIntensity(0f, 0.1f, P), 1e-4f);
        }

        [Test]
        public void RadiationIntensity_MinDistanceClampsBlowup()
        {
            // 距離0でも下限0.5でクランプ＝1/(0.5²)=4 を 0..1 にクランプ＝1（発散しない）
            Assert.AreEqual(1f, StellarRadiationRules.RadiationIntensity(1f, 0f, P), 1e-3f);
        }

        [Test]
        public void ShieldingEffectiveness_RatioOfShieldToTotal()
        {
            // 遮蔽0.5・放射0.5＝半々＝0.5
            Assert.AreEqual(0.5f, StellarRadiationRules.ShieldingEffectiveness(0.5f, 0.5f, P), 1e-4f);
            // 厚い遮蔽・弱い放射＝高防護
            Assert.AreEqual(0.8f, StellarRadiationRules.ShieldingEffectiveness(0.8f, 0.2f, P), 1e-4f);
            // 遮蔽ゼロ＝無防備
            Assert.AreEqual(0f, StellarRadiationRules.ShieldingEffectiveness(0f, 0.5f, P), 1e-4f);
        }

        [Test]
        public void ElectronicDisruption_RadiationTimesUnshielded()
        {
            // 放射0.8×非防護0.5×感度1＝0.4
            Assert.AreEqual(0.4f, StellarRadiationRules.ElectronicDisruption(0.8f, 0.5f, P), 1e-4f);
            // 完全防護＝障害ゼロ
            Assert.AreEqual(0f, StellarRadiationRules.ElectronicDisruption(1f, 1f, P), 1e-4f);
        }

        [Test]
        public void SensorNoise_ProportionalToRadiation()
        {
            // 放射0.5×ノイズ感度0.8＝0.4
            Assert.AreEqual(0.4f, StellarRadiationRules.SensorNoise(0.5f, P), 1e-4f);
            Assert.AreEqual(0f, StellarRadiationRules.SensorNoise(0f, P), 1e-4f);
        }

        [Test]
        public void CrewExposure_AccumulatesWithTime()
        {
            // 放射0.8×非防護0.5×時間5×被曝率0.1＝0.2
            Assert.AreEqual(0.2f, StellarRadiationRules.CrewExposure(0.8f, 0.5f, 5f, P), 1e-4f);
            // 滞在ゼロ＝被曝ゼロ
            Assert.AreEqual(0f, StellarRadiationRules.CrewExposure(0.8f, 0.5f, 0f, P), 1e-4f);
            // 時間が長いほど積み上がる（単調増加）
            Assert.Greater(StellarRadiationRules.CrewExposure(0.8f, 0.5f, 10f, P),
                           StellarRadiationRules.CrewExposure(0.8f, 0.5f, 5f, P));
        }

        [Test]
        public void PerformanceDegradation_CombinesIndependentFailures()
        {
            // 1-(1-0.4)(1-0.2)=1-0.48=0.52
            Assert.AreEqual(0.52f, StellarRadiationRules.PerformanceDegradation(0.4f, 0.2f, P), 1e-4f);
            // 両方健全＝低下なし
            Assert.AreEqual(0f, StellarRadiationRules.PerformanceDegradation(0f, 0f, P), 1e-4f);
            // 片方全壊＝総合も全壊
            Assert.AreEqual(1f, StellarRadiationRules.PerformanceDegradation(1f, 0f, P), 1e-4f);
        }

        [Test]
        public void SunBlindingTactic_MaxWhenStarBehind()
        {
            // 恒星が真後ろ（cos=1）＝最大利得0.5
            Assert.AreEqual(0.5f, StellarRadiationRules.SunBlindingTactic(1f, P), 1e-4f);
            // 正面（cos=0）＝中間0.25
            Assert.AreEqual(0.25f, StellarRadiationRules.SunBlindingTactic(0f, P), 1e-4f);
            // 自分が逆光側（cos=-1）＝利得なし
            Assert.AreEqual(0f, StellarRadiationRules.SunBlindingTactic(-1f, P), 1e-4f);
        }

        [Test]
        public void IsOperableInRadiation_BelowThreshold()
        {
            // 既定閾値0.7：0.52は作戦可
            Assert.IsTrue(StellarRadiationRules.IsOperableInRadiation(0.52f));
            // 0.8は不能
            Assert.IsFalse(StellarRadiationRules.IsOperableInRadiation(0.8f));
            // 閾値ちょうどは不能（未満で可）
            Assert.IsFalse(StellarRadiationRules.IsOperableInRadiation(0.7f, 0.7f));
        }

        [Test]
        public void Narrative_DivingIntoTheStarsCorona()
        {
            // 恒星のコロナへ突入：高出力(1.0)の恒星に近接(距離0.7)＝強烈な放射線
            float rad = StellarRadiationRules.RadiationIntensity(1f, 0.7f, P);
            Assert.Greater(rad, 0.9f); // 1/(0.7²)≈2.04 をクランプ＝ほぼ1

            // 重装甲の旗艦（遮蔽0.9）はそれでも防護を保つ
            float heavyShield = StellarRadiationRules.ShieldingEffectiveness(0.9f, rad, P);
            // 軽装の偵察艦（遮蔽0.2）は防護が崩れる
            float lightShield = StellarRadiationRules.ShieldingEffectiveness(0.2f, rad, P);
            Assert.Greater(heavyShield, lightShield);

            // 軽装艦は機器障害・被曝が深刻で性能が崩落
            float lightDisrupt = StellarRadiationRules.ElectronicDisruption(rad, lightShield, P);
            float lightExposure = StellarRadiationRules.CrewExposure(rad, lightShield, 5f, P);
            float lightPerf = StellarRadiationRules.PerformanceDegradation(lightDisrupt, lightExposure, P);

            // 重装艦は持ちこたえる
            float heavyDisrupt = StellarRadiationRules.ElectronicDisruption(rad, heavyShield, P);
            float heavyExposure = StellarRadiationRules.CrewExposure(rad, heavyShield, 5f, P);
            float heavyPerf = StellarRadiationRules.PerformanceDegradation(heavyDisrupt, heavyExposure, P);
            Assert.Less(heavyPerf, lightPerf);

            // 軽装艦は作戦不能・重装艦は作戦可
            Assert.IsFalse(StellarRadiationRules.IsOperableInRadiation(lightPerf));
            Assert.IsTrue(StellarRadiationRules.IsOperableInRadiation(heavyPerf));

            // しかし恒星を背にとれば逆光で敵を眩ます（重装艦の戦術的優位）
            float blinding = StellarRadiationRules.SunBlindingTactic(1f, P);
            Assert.Greater(blinding, 0f);
        }
    }
}
