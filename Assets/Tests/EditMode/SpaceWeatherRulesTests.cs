using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 宙域気象を固定する：荒れやすさが発生率を押し上げ（roll判定は決定論）、嵐は山なりの強度曲線を描き、
    /// 封鎖度の閾値で回廊が閉じ、通信途絶は jamming へ加算でき、シールド・観測網・作戦準備＝備えた側だけが
    /// 嵐を味方にできる。境界・クランプを担保。
    /// </summary>
    public class SpaceWeatherRulesTests
    {
        private static readonly SpaceWeatherParams P = SpaceWeatherParams.Default;
        // 基礎発生0.02/荒れ上乗せ0.18/強度傾き0.5/封鎖閾値0.5/通信途絶最大0.8/突破損害最大0.6/予報半減期10

        [Test]
        public void OnsetChance_And_Strikes_Deterministic()
        {
            // 穏やかな宙域＝基礎2%、最荒れ＝20%
            Assert.AreEqual(0.02f, SpaceWeatherRules.OnsetChance(0f, P), 1e-5f);
            Assert.AreEqual(0.2f, SpaceWeatherRules.OnsetChance(1f, P), 1e-5f);
            // roll判定＝roll<chance で発生（決定論・境界は発生しない）
            Assert.IsTrue(SpaceWeatherRules.Strikes(0.2f, 0.1f));
            Assert.IsFalse(SpaceWeatherRules.Strikes(0.2f, 0.2f));
            Assert.IsFalse(SpaceWeatherRules.Strikes(0f, 0f));
            // 種類enumの値を固定（封鎖・通信・損害の出方は値駆動で係数に委ねる）
            Assert.AreEqual(0, (int)SpaceWeatherKind.恒星嵐);
            Assert.AreEqual(1, (int)SpaceWeatherKind.重力波バースト);
            Assert.AreEqual(2, (int)SpaceWeatherKind.放射線帯活性化);
        }

        [Test]
        public void StormIntensityTick_RisesThenFalls()
        {
            // 寿命前半＝立ち上がり（phase0・dt1：+0.5）
            Assert.AreEqual(0.5f, SpaceWeatherRules.StormIntensityTick(0f, 0f, 1f, P), 1e-5f);
            // ピーク（phase0.5）＝変化なし
            Assert.AreEqual(0.5f, SpaceWeatherRules.StormIntensityTick(0.5f, 0.5f, 1f, P), 1e-5f);
            // 寿命後半＝減衰（phase1・dt1：−0.5）
            Assert.AreEqual(0.1f, SpaceWeatherRules.StormIntensityTick(0.6f, 1f, 1f, P), 1e-5f);
            // 0未満には落ちない
            Assert.AreEqual(0f, SpaceWeatherRules.StormIntensityTick(0.1f, 1f, 1f, P), 1e-5f);
        }

        [Test]
        public void StormLifetime_MountainShapedCurve()
        {
            // 寿命を10tickで積分＝中盤にピーク・終盤に減衰する山なりを担保
            float intensity = 0f;
            float peak = 0f;
            for (int i = 0; i < 10; i++)
            {
                intensity = SpaceWeatherRules.StormIntensityTick(intensity, i / 10f, 0.1f, P);
                if (intensity > peak) peak = intensity;
            }
            Assert.AreEqual(0.15f, peak, 1e-5f);      // ピーク（phase 0.5 到達時）
            Assert.AreEqual(0.05f, intensity, 1e-5f); // 終盤＝ピークから減衰している
            Assert.Less(intensity, peak);
        }

        [Test]
        public void CorridorClosure_QuadraticAndThreshold()
        {
            // 封鎖度＝強度の二乗（弱い嵐はほぼ妨げない）
            Assert.AreEqual(0f, SpaceWeatherRules.CorridorClosure(0f), 1e-5f);
            Assert.AreEqual(0.25f, SpaceWeatherRules.CorridorClosure(0.5f), 1e-5f);
            Assert.AreEqual(0.81f, SpaceWeatherRules.CorridorClosure(0.9f), 1e-5f);
            // 閾値0.5＝封鎖度未満なら通れる・以上で回廊が閉じる（両軍とも）
            Assert.IsTrue(SpaceWeatherRules.IsPassable(0.5f, P));   // 封鎖0.25<0.5
            Assert.IsFalse(SpaceWeatherRules.IsPassable(0.9f, P));  // 封鎖0.81≥0.5
        }

        [Test]
        public void CommsDegradation_AddsToJamming()
        {
            // 強度比例（最大0.8）＝CommunicationsRules の jamming へ加算できるスケール
            Assert.AreEqual(0.8f, SpaceWeatherRules.CommsDegradation(1f, P), 1e-5f);
            Assert.AreEqual(0.4f, SpaceWeatherRules.CommsDegradation(0.5f, P), 1e-5f);
            Assert.AreEqual(0f, SpaceWeatherRules.CommsDegradation(-1f, P), 1e-5f);
            // 全力の嵐単独で CommsParams.Default の途絶閾値0.8に達する＝天然のジャミング
            Assert.IsTrue(CommunicationsRules.IsCutOff(SpaceWeatherRules.CommsDegradation(1f, P)));
        }

        [Test]
        public void TransitDamageRisk_ShieldIsPreparedness()
        {
            // 無防備×最大強度＝損害率0.6、完全シールド＝無傷（備えた側だけが安く抜ける）
            Assert.AreEqual(0.6f, SpaceWeatherRules.TransitDamageRisk(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0f, SpaceWeatherRules.TransitDamageRisk(1f, 1f, P), 1e-5f);
            Assert.AreEqual(0.15f, SpaceWeatherRules.TransitDamageRisk(0.5f, 0.5f, P), 1e-5f);
        }

        [Test]
        public void ForecastAccuracy_ObservatoryTimesLeadTime()
        {
            // 観測網完全×直前＝完全予報、先読み10（半減期）＝精度半減
            Assert.AreEqual(1f, SpaceWeatherRules.ForecastAccuracy(1f, 0f, P), 1e-5f);
            Assert.AreEqual(0.5f, SpaceWeatherRules.ForecastAccuracy(1f, 10f, P), 1e-5f);
            Assert.AreEqual(0.25f, SpaceWeatherRules.ForecastAccuracy(1f, 30f, P), 1e-5f);
            // 観測網が無ければ何も読めない
            Assert.AreEqual(0f, SpaceWeatherRules.ForecastAccuracy(0f, 0f, P), 1e-5f);
            // 負の先読みは現在扱い
            Assert.AreEqual(0.5f, SpaceWeatherRules.ForecastAccuracy(0.5f, -5f, P), 1e-5f);
        }

        [Test]
        public void WeatherWindowValue_NeutralWithoutPreparation()
        {
            // 半端な嵐（封鎖0.5）×準備万全＝最高の隠れ蓑
            Assert.AreEqual(1f, SpaceWeatherRules.WeatherWindowValue(0.5f, 1f), 1e-5f);
            // 晴天＝隠れられない／完全封鎖＝此方も動けない
            Assert.AreEqual(0f, SpaceWeatherRules.WeatherWindowValue(0f, 1f), 1e-5f);
            Assert.AreEqual(0f, SpaceWeatherRules.WeatherWindowValue(1f, 1f), 1e-5f);
            // 準備ゼロなら嵐は何ももたらさない＝天気は中立、備えた側だけが味方にできる
            Assert.AreEqual(0f, SpaceWeatherRules.WeatherWindowValue(0.5f, 0f), 1e-5f);
            Assert.AreEqual(0.75f, SpaceWeatherRules.WeatherWindowValue(0.25f, 1f), 1e-5f);
        }
    }
}
