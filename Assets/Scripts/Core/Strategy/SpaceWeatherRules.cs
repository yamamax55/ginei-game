using UnityEngine;

namespace Ginei
{
    /// <summary>宙域気象の種類。封鎖・通信・損害の出方が違う（値駆動＝係数の組で表す）。</summary>
    public enum SpaceWeatherKind
    {
        恒星嵐,         // 荷電粒子の奔流＝通信途絶が主、艦体損害も
        重力波バースト, // 時空の震え＝FTL封鎖が主（回廊が閉じる）
        放射線帯活性化  // 慢性的な被曝帯＝強行突破の損害が主
    }

    /// <summary>宙域気象の調整係数。</summary>
    public readonly struct SpaceWeatherParams
    {
        /// <summary>穏やかな宙域（volatility 0）でも残る基礎発生率。</summary>
        public readonly float baseOnsetChance;
        /// <summary>荒れやすさ最大（volatility 1）で上乗せされる発生率。</summary>
        public readonly float volatilityOnsetBonus;
        /// <summary>嵐の強度が1秒あたりに変化する最大速度（立ち上がり・減衰の傾き）。</summary>
        public readonly float stormRampRate;
        /// <summary>FTL通行不可とみなす封鎖度の閾値（これ以上で回廊が閉じる）。</summary>
        public readonly float closureThreshold;
        /// <summary>強度最大のときの通信途絶度の最大値。</summary>
        public readonly float maxCommsDegradation;
        /// <summary>強度最大・無防備（shield 0）で強行突破したときの損害率の最大値。</summary>
        public readonly float maxTransitDamage;
        /// <summary>予報精度が半減する先読み時間（観測網が完全でも、先の天気ほど読めない）。</summary>
        public readonly float forecastHalfLife;

        public SpaceWeatherParams(float baseOnsetChance, float volatilityOnsetBonus, float stormRampRate,
                                  float closureThreshold, float maxCommsDegradation, float maxTransitDamage,
                                  float forecastHalfLife)
        {
            this.baseOnsetChance = Mathf.Clamp01(baseOnsetChance);
            this.volatilityOnsetBonus = Mathf.Clamp01(volatilityOnsetBonus);
            this.stormRampRate = Mathf.Max(0f, stormRampRate);
            this.closureThreshold = Mathf.Clamp01(closureThreshold);
            this.maxCommsDegradation = Mathf.Clamp01(maxCommsDegradation);
            this.maxTransitDamage = Mathf.Clamp01(maxTransitDamage);
            this.forecastHalfLife = Mathf.Max(0.0001f, forecastHalfLife);
        }

        /// <summary>既定＝基礎発生2%・荒れ上乗せ18%・強度傾き0.5/s・封鎖閾値0.5・通信途絶最大0.8・突破損害最大0.6・予報半減期10。</summary>
        public static SpaceWeatherParams Default => new SpaceWeatherParams(0.02f, 0.18f, 0.5f, 0.5f, 0.8f, 0.6f, 10f);
    }

    /// <summary>
    /// 宙域気象（恒星嵐・重力波バースト・放射線帯活性化）の純ロジック。回廊を一時封鎖し通信を途絶させる
    /// **時限の環境イベント**＝恒常の宙域特性（星雲・小惑星帯など）を担う <see cref="TerrainRules"/> とは別物。
    /// 嵐は山なりの寿命（立ち上がり→ピーク→減衰）を持ち、封鎖度が閾値を超えた回廊はFTL通行不可、
    /// 通信途絶度は <see cref="CommunicationsRules"/> の jamming 入力へ加算して使える（合算側で 0..1 に丸める）。
    /// 宇宙の天気は**中立**＝封鎖も途絶も両軍に等しく降りかかる。差がつくのは備えだけ：
    /// シールド（<see cref="TransitDamageRisk"/>）・観測網（<see cref="ForecastAccuracy"/>）・
    /// 作戦準備（<see cref="WeatherWindowValue"/>）を持つ側だけが嵐を味方にできる。
    /// 乱数なし・決定論（roll は引数で受ける）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SpaceWeatherRules
    {
        /// <summary>気象イベントの発生率＝基礎＋荒れやすさ sectorVolatility(0..1) ×上乗せ。1tickあたりの確率。</summary>
        public static float OnsetChance(float sectorVolatility, SpaceWeatherParams p)
        {
            return Mathf.Clamp01(p.baseOnsetChance + p.volatilityOnsetBonus * Mathf.Clamp01(sectorVolatility));
        }

        public static float OnsetChance(float sectorVolatility) => OnsetChance(sectorVolatility, SpaceWeatherParams.Default);

        /// <summary>発生判定＝roll(0..1) が発生率未満なら嵐が立つ（決定論＝rollは呼び出し側が渡す）。</summary>
        public static bool Strikes(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 嵐の1tick後の強度（0..1）。寿命進行度 phase(0..1) の前半（&lt;0.5）は立ち上がり、
        /// 後半（&gt;0.5）は減衰＝傾き stormRampRate×(1−2×phase) で山なりの強度曲線を描く。
        /// </summary>
        public static float StormIntensityTick(float intensity, float phase, float dt, SpaceWeatherParams p)
        {
            float slope = p.stormRampRate * (1f - 2f * Mathf.Clamp01(phase));
            return Mathf.Clamp01(Mathf.Clamp01(intensity) + slope * Mathf.Max(0f, dt));
        }

        public static float StormIntensityTick(float intensity, float phase, float dt)
            => StormIntensityTick(intensity, phase, dt, SpaceWeatherParams.Default);

        /// <summary>回廊封鎖度（0..1）＝強度の二乗。弱い嵐はほぼ妨げにならず、強い嵐は急速に回廊を閉ざす。</summary>
        public static float CorridorClosure(float intensity)
        {
            float i = Mathf.Clamp01(intensity);
            return i * i;
        }

        /// <summary>FTL通行可か＝封鎖度が閾値未満（閾値以上で回廊は一時的に閉じる＝両軍とも通れない）。</summary>
        public static bool IsPassable(float intensity, SpaceWeatherParams p)
        {
            return CorridorClosure(intensity) < p.closureThreshold;
        }

        public static bool IsPassable(float intensity) => IsPassable(intensity, SpaceWeatherParams.Default);

        /// <summary>
        /// 通信途絶度（0..1）＝強度×最大途絶。<see cref="CommunicationsRules"/> の jamming 入力へ
        /// 電子戦と並列に**加算**できる（嵐は天然のジャミング＝敵味方の区別なく塞ぐ）。
        /// </summary>
        public static float CommsDegradation(float intensity, SpaceWeatherParams p)
        {
            return p.maxCommsDegradation * Mathf.Clamp01(intensity);
        }

        public static float CommsDegradation(float intensity) => CommsDegradation(intensity, SpaceWeatherParams.Default);

        /// <summary>
        /// 強行突破の損害率（0..1）＝最大損害×強度×(1−シールド)。封鎖中でも通れはするが対価を払う＝
        /// shieldRating(0..1) の備えがある側だけが安く抜けられる。
        /// </summary>
        public static float TransitDamageRisk(float intensity, float shieldRating, SpaceWeatherParams p)
        {
            return p.maxTransitDamage * Mathf.Clamp01(intensity) * (1f - Mathf.Clamp01(shieldRating));
        }

        public static float TransitDamageRisk(float intensity, float shieldRating)
            => TransitDamageRisk(intensity, shieldRating, SpaceWeatherParams.Default);

        /// <summary>
        /// 予報精度（0..1）＝観測網 observatoryLevel(0..1) を基礎に、先読み時間 leadTime が
        /// forecastHalfLife ごとに半減期カーブで割り引く＝観測網を持つ側だけが嵐を読んで動ける。
        /// </summary>
        public static float ForecastAccuracy(float observatoryLevel, float leadTime, SpaceWeatherParams p)
        {
            return Mathf.Clamp01(observatoryLevel) / (1f + Mathf.Max(0f, leadTime) / p.forecastHalfLife);
        }

        public static float ForecastAccuracy(float observatoryLevel, float leadTime)
            => ForecastAccuracy(observatoryLevel, leadTime, SpaceWeatherParams.Default);

        /// <summary>
        /// 気象の軍事利用価値（0..1）＝作戦準備 militaryOpportunity(0..1)×封鎖度の山なり 4c(1−c)。
        /// 半端な嵐（closure≈0.5）が最高の隠れ蓑＝敵の眼は塞がるがまだ動ける。完全封鎖は此方も動けず、
        /// 晴天は隠れられない。準備ゼロなら価値ゼロ＝**天気は中立、備えた側だけが味方にできる**。
        /// </summary>
        public static float WeatherWindowValue(float closure, float militaryOpportunity)
        {
            float c = Mathf.Clamp01(closure);
            return Mathf.Clamp01(militaryOpportunity) * 4f * c * (1f - c);
        }
    }
}
