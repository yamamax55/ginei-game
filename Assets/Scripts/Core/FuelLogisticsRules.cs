using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 燃料兵站の調整値（#燃料兵站）＝艦隊の<b>行動半径と燃料切れ</b>を決めるパラメータ。
    /// 速度・大艦隊ほど燃料消費が増す係数、帰還ぶんの安全マージン、節約モードの閾値と下限、補給艦延伸の上限。
    /// すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct FuelLogisticsParams
    {
        /// <summary>基本燃料消費率（距離あたり・静止/単艦の基準＝速度0・規模0でこの値）。</summary>
        public readonly float baseConsumption;
        /// <summary>速度に対する消費の感度（速いほど燃料を食う＝速度×係数を 1 に上乗せ）。</summary>
        public readonly float speedFactor;
        /// <summary>艦隊規模に対する消費の感度（大艦隊ほど燃料を食う＝規模×係数を 1 に上乗せ）。</summary>
        public readonly float sizeFactor;
        /// <summary>帰還ぶんに残す安全マージン（0..1・0.4＝行動圏の4割を帰り道に確保）。</summary>
        public readonly float returnMargin;
        /// <summary>節約モードに入り始める燃料残量比の閾値（0..1・これ未満で速度/戦闘力を抑える）。</summary>
        public readonly float lowFuelThreshold;
        /// <summary>節約モードの最大抑制（燃料0で能力がこの倍率まで落ちる＝0.5＝半減が下限）。</summary>
        public readonly float minPenaltyFactor;
        /// <summary>補給艦による行動圏延伸の上限倍率（タンカーが十分でも延伸はこの倍までで頭打ち）。</summary>
        public readonly float maxTankerExtension;

        public FuelLogisticsParams(float baseConsumption, float speedFactor, float sizeFactor,
            float returnMargin, float lowFuelThreshold, float minPenaltyFactor, float maxTankerExtension)
        {
            this.baseConsumption = Mathf.Clamp(baseConsumption, 0.0001f, 100f);
            this.speedFactor = Mathf.Clamp(speedFactor, 0f, 10f);
            this.sizeFactor = Mathf.Clamp(sizeFactor, 0f, 10f);
            this.returnMargin = Mathf.Clamp01(returnMargin);
            this.lowFuelThreshold = Mathf.Clamp01(lowFuelThreshold);
            this.minPenaltyFactor = Mathf.Clamp(minPenaltyFactor, 0f, 1f);
            this.maxTankerExtension = Mathf.Clamp(maxTankerExtension, 1f, 10f);
        }

        /// <summary>既定：基本消費1.0・速度感度0.5・規模感度0.1・帰還マージン0.4・節約閾値0.3・抑制下限0.5・タンカー延伸上限2.0。</summary>
        public static FuelLogisticsParams Default => new FuelLogisticsParams(
            DefaultBaseConsumption, DefaultSpeedFactor, DefaultSizeFactor,
            DefaultReturnMargin, DefaultLowFuelThreshold, DefaultMinPenaltyFactor, DefaultMaxTankerExtension);

        public const float DefaultBaseConsumption = 1.0f;
        public const float DefaultSpeedFactor = 0.5f;
        public const float DefaultSizeFactor = 0.1f;
        public const float DefaultReturnMargin = 0.4f;
        public const float DefaultLowFuelThreshold = 0.3f;
        public const float DefaultMinPenaltyFactor = 0.5f;
        public const float DefaultMaxTankerExtension = 2.0f;
    }

    /// <summary>
    /// 燃料兵站の純ロジック（#燃料兵站・唯一の窓口）＝艦隊は<b>燃料（推進剤）</b>を消費して移動・戦闘し、尽きると動けなくなる。
    /// <b>行動半径</b>は燃料積載と消費率で決まり（<see cref="OperatingRadius"/>）、帰還ぶんを残した安全行動圏（<see cref="RangeFromBase"/>）の中で動く。
    /// 燃料が乏しいと節約モード（<see cref="LowFuelPenalty"/>）で速度/戦闘力を抑え、補給艦（<see cref="TankerSupport"/>）が行動圏を延ばす。
    /// 補給に届かないまま燃料が尽きれば立ち往生する（<see cref="Stranded"/>）。
    /// <b>分担</b>：一般の補給・補給線負荷（<see cref="SupplyRules"/>／LogisticsBurdenRules）とは別＝こちらは<b>燃料に特化した行動半径</b>。
    /// 燃料という<b>資源そのものの産出/備蓄</b>（<see cref="StrategicResourceRules"/>）とは別＝こちらは<b>艦隊運用の燃料消費</b>。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・各メソッドに Params 明示版＋Default 委譲版・test-first。
    /// </summary>
    public static class FuelLogisticsRules
    {
        /// <summary>ゼロ割回避の微小値。</summary>
        public const float Epsilon = 0.0001f;

        // ── 燃料消費率 ────────────────────────────────────────────

        /// <summary>距離あたり燃料消費率＝速いほど・大艦隊ほど多く食う。</summary>
        public static float FuelConsumption(float speed, float fleetSize)
            => FuelConsumption(speed, fleetSize, FuelLogisticsParams.Default);

        /// <summary>燃料消費率＝base×(1+speed×speedFactor)×(1+fleetSize×sizeFactor)。負入力は0扱い・非負。</summary>
        public static float FuelConsumption(float speed, float fleetSize, FuelLogisticsParams p)
        {
            float s = Mathf.Max(0f, speed);
            float n = Mathf.Max(0f, fleetSize);
            return p.baseConsumption * (1f + s * p.speedFactor) * (1f + n * p.sizeFactor);
        }

        // ── 行動半径 ──────────────────────────────────────────────

        /// <summary>燃料で行ける行動半径＝燃料残量÷消費率（消費率が小さいほど遠くへ）。</summary>
        public static float OperatingRadius(float fuelReserve, float fuelConsumption)
        {
            float fuel = Mathf.Max(0f, fuelReserve);
            float c = Mathf.Max(Epsilon, fuelConsumption);
            return fuel / c;
        }

        /// <summary>帰還ぶんを残した安全行動圏＝行動半径×(1−帰還マージン)。</summary>
        public static float RangeFromBase(float operatingRadius, float returnMargin)
        {
            float r = Mathf.Max(0f, operatingRadius);
            float m = Mathf.Clamp01(returnMargin);
            return r * (1f - m);
        }

        // ── 燃料残量比 ────────────────────────────────────────────

        /// <summary>燃料残量比（0..1）＝燃料残量÷燃料容量。容量0は満タン扱い1.0。</summary>
        public static float FuelState(float fuelReserve, float fuelCapacity)
        {
            if (fuelCapacity <= Epsilon) return 1f;
            return Mathf.Clamp01(Mathf.Max(0f, fuelReserve) / fuelCapacity);
        }

        // ── 節約モード ────────────────────────────────────────────

        /// <summary>燃料が乏しいときの能力抑制倍率（0..1）＝節約モード。十分なら1.0。</summary>
        public static float LowFuelPenalty(float fuelState)
            => LowFuelPenalty(fuelState, FuelLogisticsParams.Default);

        /// <summary>
        /// 能力抑制倍率（minPenaltyFactor..1）。閾値以上は1.0（無抑制）、閾値未満は
        /// 残量0で minPenaltyFactor、閾値で1.0 へ線形補間（燃料が乏しいほど速度/戦闘力を抑える）。
        /// </summary>
        public static float LowFuelPenalty(float fuelState, FuelLogisticsParams p)
        {
            float fs = Mathf.Clamp01(fuelState);
            if (fs >= p.lowFuelThreshold) return 1f;
            if (p.lowFuelThreshold <= Epsilon) return 1f;
            float t = fs / p.lowFuelThreshold; // 0..1
            return Mathf.Lerp(p.minPenaltyFactor, 1f, t);
        }

        // ── 補給艦による延伸 ──────────────────────────────────────

        /// <summary>補給艦（洋上補給）による行動圏の延伸倍率（1..maxTankerExtension）。</summary>
        public static float TankerSupport(float tankerCapacity, float fleetDemand)
            => TankerSupport(tankerCapacity, fleetDemand, FuelLogisticsParams.Default);

        /// <summary>
        /// 延伸倍率＝1＋clamp(tankerCapacity÷fleetDemand, 0, maxTankerExtension−1)。需要0は延伸なし1.0、
        /// 補給艦が需要の (maxTankerExtension−1) 倍ぶんあれば上限に達する。
        /// </summary>
        public static float TankerSupport(float tankerCapacity, float fleetDemand, FuelLogisticsParams p)
        {
            if (fleetDemand <= Epsilon) return 1f;
            float ratio = Mathf.Max(0f, tankerCapacity) / fleetDemand;
            float extra = Mathf.Clamp(ratio, 0f, p.maxTankerExtension - 1f);
            return 1f + extra;
        }

        // ── 立ち往生 ──────────────────────────────────────────────

        /// <summary>燃料切れで立ち往生か＝補給までの距離を賄える燃料（=行ける距離）が残っていない（bool）。</summary>
        public static bool Stranded(float fuelReserve, float distanceToSupply)
        {
            float fuel = Mathf.Max(0f, fuelReserve);
            float d = Mathf.Max(0f, distanceToSupply);
            return fuel < d;
        }

        // ── 燃料危機 ──────────────────────────────────────────────

        /// <summary>燃料危機か＝残量比が閾値以下（bool）。</summary>
        public static bool IsFuelCritical(float fuelState, float threshold)
            => Mathf.Clamp01(fuelState) <= Mathf.Clamp01(threshold);
    }
}
