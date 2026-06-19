using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 長距離打撃（アウトレンジ）の調整係数。射程優位帯・遠距離命中減衰・カイティング・接近脅威・優位価値を束ねる。
    /// </summary>
    public readonly struct StandoffStrikeParams
    {
        /// <summary>射程優位のうち実際に一方的に撃てる距離帯へ変換する割合（0..1）。</summary>
        public readonly float zoneFraction;
        /// <summary>距離1あたりの命中減衰（遠いほど当たらない）。</summary>
        public readonly float accuracyFalloffPer;
        /// <summary>命中に対する射撃管制（fire control）の寄与下限（fc=0 でもこの倍率は残る）。</summary>
        public readonly float minFireControlFactor;
        /// <summary>カイティング有効度を 0.5 から±する機動差の正規化幅。</summary>
        public readonly float mobilityGap;
        /// <summary>接近猶予が短いほど優位価値を割り引く基準（grace と足して割る）。</summary>
        public readonly float closingDiscount;
        /// <summary>アウトレンジ成立とみなす射程優位の閾値。</summary>
        public readonly float outrangeThreshold;

        public StandoffStrikeParams(float zoneFraction, float accuracyFalloffPer, float minFireControlFactor,
                                    float mobilityGap, float closingDiscount, float outrangeThreshold)
        {
            this.zoneFraction = Mathf.Clamp01(zoneFraction);
            this.accuracyFalloffPer = Mathf.Max(0f, accuracyFalloffPer);
            this.minFireControlFactor = Mathf.Clamp01(minFireControlFactor);
            this.mobilityGap = Mathf.Max(1f, mobilityGap);
            this.closingDiscount = Mathf.Max(0.01f, closingDiscount);
            this.outrangeThreshold = Mathf.Max(0f, outrangeThreshold);
        }

        /// <summary>既定＝撃てる帯=優位の80%・命中減衰0.005/距離・FC下限0.5・機動幅100・接近割引1・優位閾値5。</summary>
        public static StandoffStrikeParams Default
            => new StandoffStrikeParams(0.8f, 0.005f, 0.5f, 100f, 1f, 5f);
    }

    /// <summary>
    /// 長距離打撃（アウトレンジ戦法）の純ロジック。自軍の射程が敵を上回るとき、
    /// 敵の射程外（standoff zone）から一方的に撃って損害を与える。だが命中は距離で落ち、
    /// 敵が距離を詰めると優位は消える＝接近脅威とカイティングで「撃ちながら下がる」運用を表す。
    /// 担当は「アウトレンジ戦法の運用」：射程優位帯・遠距離命中・一方的損害・接近猶予・カイティング・正味価値。
    /// 既存 `RangeBandRules`（射程帯＝近/中/遠の理想間合いと接近/後退）とは責務を分け、本クラスは
    /// 「射程外から一方的に叩く戦法の運用」に特化。`AccuracyRules`（命中＝攻撃精度−回避の核）とも別＝
    /// こちらは距離優位に伴う命中減衰の数値で、命中判定そのものは二重実装しない。
    /// 盤面非依存の plain 引数のみ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。実効値パターン（基準値非破壊）。
    /// </summary>
    public static class StandoffStrikeRules
    {
        /// <summary>射程の優位差＝自射程−敵射程（負なら相手が長射程＝こちらが不利）。</summary>
        public static float RangeAdvantage(float ownRange, float enemyRange, StandoffStrikeParams p)
        {
            return Mathf.Max(0f, ownRange) - Mathf.Max(0f, enemyRange);
        }

        public static float RangeAdvantage(float ownRange, float enemyRange)
            => RangeAdvantage(ownRange, enemyRange, StandoffStrikeParams.Default);

        /// <summary>
        /// 一方的に撃てる距離帯＝射程優位×zoneFraction（敵が撃ち返せない範囲の幅）。
        /// 優位が無い/負なら 0（アウトレンジできない）。
        /// </summary>
        public static float StandoffZone(float rangeAdvantage, StandoffStrikeParams p)
        {
            return Mathf.Max(0f, rangeAdvantage) * p.zoneFraction;
        }

        public static float StandoffZone(float rangeAdvantage)
            => StandoffZone(rangeAdvantage, StandoffStrikeParams.Default);

        /// <summary>
        /// 遠距離の命中精度（0..1）＝距離による減衰×射撃管制。
        /// 基礎＝clamp01(1−falloff×距離)、これに FC 倍率（minFireControlFactor〜1.0、fc=100 で 1.0）を乗算。
        /// 距離が遠いほど・FC が低いほど当たらない。
        /// </summary>
        public static float LongRangeAccuracy(float engagementRange, float ownFireControl, StandoffStrikeParams p)
        {
            float range = Mathf.Max(0f, engagementRange);
            float baseAcc = Mathf.Clamp01(1f - p.accuracyFalloffPer * range);
            float fc = Mathf.Clamp01(Mathf.Clamp(ownFireControl, 0f, 100f) / 100f);
            float fcFactor = Mathf.Lerp(p.minFireControlFactor, 1f, fc);
            return Mathf.Clamp01(baseAcc * fcFactor);
        }

        public static float LongRangeAccuracy(float engagementRange, float ownFireControl)
            => LongRangeAccuracy(engagementRange, ownFireControl, StandoffStrikeParams.Default);

        /// <summary>
        /// アウトレンジで一方的に与える損害＝火力×命中×帯飽和（zone/(zone+1)）。
        /// 撃てる帯（standoffZone）が無ければ 0＝そもそも一方的に撃てない。
        /// </summary>
        public static float OneSidedDamage(float standoffZone, float longRangeAccuracy, float ownFirepower, StandoffStrikeParams p)
        {
            float zone = Mathf.Max(0f, standoffZone);
            if (zone <= 0f) return 0f;
            float acc = Mathf.Clamp01(longRangeAccuracy);
            float fire = Mathf.Max(0f, ownFirepower);
            float zoneFactor = zone / (zone + 1f);
            return fire * acc * zoneFactor;
        }

        public static float OneSidedDamage(float standoffZone, float longRangeAccuracy, float ownFirepower)
            => OneSidedDamage(standoffZone, longRangeAccuracy, ownFirepower, StandoffStrikeParams.Default);

        /// <summary>
        /// 敵が距離を詰めて優位を消すまでの猶予（time-to-close）＝撃てる帯÷敵接近速度。
        /// 速度0なら無限に近い猶予（大きな値）を返す。大きいほど長く一方的に撃てる。
        /// </summary>
        public static float ClosingThreat(float enemyClosingSpeed, float standoffZone, StandoffStrikeParams p)
        {
            float zone = Mathf.Max(0f, standoffZone);
            float speed = Mathf.Max(0f, enemyClosingSpeed);
            if (speed <= 0f) return zone > 0f ? float.MaxValue : 0f;
            return zone / speed;
        }

        public static float ClosingThreat(float enemyClosingSpeed, float standoffZone)
            => ClosingThreat(enemyClosingSpeed, standoffZone, StandoffStrikeParams.Default);

        /// <summary>
        /// 撃ちながら下がって距離を保つカイティング有効度（0..1）。
        /// 機動が拮抗で 0.5、自分が速いほど 1.0 へ、遅いほど 0 へ（差を mobilityGap で正規化）。
        /// </summary>
        public static float KitingEffectiveness(float ownMobility, float enemyMobility, StandoffStrikeParams p)
        {
            float own = Mathf.Max(0f, ownMobility);
            float enemy = Mathf.Max(0f, enemyMobility);
            float gap = (own - enemy) / p.mobilityGap;
            return Mathf.Clamp01(0.5f + 0.5f * gap);
        }

        public static float KitingEffectiveness(float ownMobility, float enemyMobility)
            => KitingEffectiveness(ownMobility, enemyMobility, StandoffStrikeParams.Default);

        /// <summary>
        /// 射程優位の正味価値＝一方的損害×（接近猶予の持続割引）。
        /// 猶予が短い（すぐ詰められる）ほど grace/(grace+discount) で割り引く＝距離を詰められるリスクを差し引く。
        /// </summary>
        public static float RangeSupremacyValue(float oneSidedDamage, float closingThreat, StandoffStrikeParams p)
        {
            float dmg = Mathf.Max(0f, oneSidedDamage);
            float grace = Mathf.Max(0f, closingThreat);
            if (float.IsInfinity(grace) || grace >= float.MaxValue) return dmg; // 詰められない＝割引なし
            float sustain = grace / (grace + p.closingDiscount);
            return dmg * sustain;
        }

        public static float RangeSupremacyValue(float oneSidedDamage, float closingThreat)
            => RangeSupremacyValue(oneSidedDamage, closingThreat, StandoffStrikeParams.Default);

        /// <summary>アウトレンジできるか＝射程優位が閾値を超える（敵の射程外から一方的に撃てる）。</summary>
        public static bool CanOutrange(float rangeAdvantage, float threshold)
        {
            return rangeAdvantage > Mathf.Max(0f, threshold);
        }
    }
}
