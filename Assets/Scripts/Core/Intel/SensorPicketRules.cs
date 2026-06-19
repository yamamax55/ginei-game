using UnityEngine;

namespace Ginei
{
    /// <summary>哨戒線・早期警戒の調整係数（前方展開で敵接近を早く知る）。</summary>
    public readonly struct SensorPicketParams
    {
        /// <summary>警戒範囲の基礎倍率（哨戒間隔×隻数^指数 に掛ける）。</summary>
        public readonly float coverageScale;
        /// <summary>隻数に対する警戒範囲の逓減指数（0.5＝広く薄く張ると平方根的にしか伸びない）。</summary>
        public readonly float coverageExponent;
        /// <summary>本隊が態勢を半分整えるのに要する猶予（leadTime のハーフタイム）。</summary>
        public readonly float leadHalfTime;
        /// <summary>哨戒艦の脆さが頭打ちになる基準間隔（spacing/(spacing+spacingRef)）。</summary>
        public readonly float spacingRef;
        /// <summary>隙のすり抜けが頭打ちになる基準間隔（spacing/(spacing+gapRef)）。</summary>
        public readonly float gapRef;
        /// <summary>脆さの基礎倍率。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>誤警報におけるノイズ（星雲等）の寄与重み 0..1（残りはセンサ品質の低さ由来）。</summary>
        public readonly float clutterWeight;

        public SensorPicketParams(float coverageScale, float coverageExponent, float leadHalfTime,
            float spacingRef, float gapRef, float vulnerabilityScale, float clutterWeight)
        {
            this.coverageScale = Mathf.Max(0f, coverageScale);
            this.coverageExponent = Mathf.Clamp(coverageExponent, 0f, 1f);
            this.leadHalfTime = Mathf.Max(0.0001f, leadHalfTime);
            this.spacingRef = Mathf.Max(0.0001f, spacingRef);
            this.gapRef = Mathf.Max(0.0001f, gapRef);
            this.vulnerabilityScale = Mathf.Max(0f, vulnerabilityScale);
            this.clutterWeight = Mathf.Clamp01(clutterWeight);
        }

        /// <summary>既定＝倍率1・指数0.5・猶予ハーフ5・基準間隔10/10・脆さ倍率1・ノイズ重み0.5。</summary>
        public static SensorPicketParams Default =>
            new SensorPicketParams(1f, 0.5f, 5f, 10f, 10f, 1f, 0.5f);
    }

    /// <summary>
    /// 哨戒線・早期警戒の純ロジック。本隊の前方・周囲に哨戒艦を展開し、敵の接近を早期に探知して警報を出す。
    /// 広く薄く張ると警戒範囲は広いが各哨戒艦は脆く隙を突かれやすい。密に張ると堅いが一部しか覆えない。
    /// 盤面非依存の plain 引数のみ（座標・コンポーネント非依存）。乱数を持たず、必要なら外から roll を与える。
    /// 実効値パターン（与えた基準値は非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// 分担：<see cref="ReconRules"/>（敵戦力の推定・霧）とは別＝防御的な早期警戒に特化。
    /// 偵察幕（本隊を隠す ScreeningRules・将来追加）とも別＝こちらは敵を早期探知する警戒網。
    /// </summary>
    public static class SensorPicketRules
    {
        /// <summary>
        /// 哨戒線が覆う警戒範囲＝coverageScale × 哨戒間隔 × 隻数^coverageExponent。
        /// 間隔を広げ隻数を増やすほど広がるが、隻数は指数<1 で逓減（広く薄い線は平方根的にしか伸びない）。
        /// </summary>
        public static float WarningCoverage(int picketCount, float picketSpacing, SensorPicketParams p)
        {
            int count = Mathf.Max(0, picketCount);
            float spacing = Mathf.Max(0f, picketSpacing);
            return p.coverageScale * spacing * Mathf.Pow(count, p.coverageExponent);
        }

        public static float WarningCoverage(int picketCount, float picketSpacing)
            => WarningCoverage(picketCount, picketSpacing, SensorPicketParams.Default);

        /// <summary>
        /// 敵接近を察知してから本隊に到達するまでの猶予＝警戒範囲 ÷ 敵接近速度。
        /// 速度が極小でも 0 除算しない（下限クランプ）。
        /// </summary>
        public static float DetectionLeadTime(float warningCoverage, float enemyApproachSpeed)
        {
            float coverage = Mathf.Max(0f, warningCoverage);
            float speed = Mathf.Max(0.0001f, enemyApproachSpeed);
            return coverage / speed;
        }

        /// <summary>
        /// 猶予で本隊が態勢を整えられる度合い 0..1。初期態勢 readiness を、猶予のハーフタイム関数
        /// leadTime/(leadTime+leadHalfTime) で 1 に向けて底上げ（猶予が長いほど整う）。
        /// </summary>
        public static float ReactionPreparation(float detectionLeadTime, float mainBodyReadiness, SensorPicketParams p)
        {
            float lead = Mathf.Max(0f, detectionLeadTime);
            float ready = Mathf.Clamp01(mainBodyReadiness);
            float gain = lead / (lead + p.leadHalfTime);
            return Mathf.Clamp01(ready + (1f - ready) * gain);
        }

        public static float ReactionPreparation(float detectionLeadTime, float mainBodyReadiness)
            => ReactionPreparation(detectionLeadTime, mainBodyReadiness, SensorPicketParams.Default);

        /// <summary>
        /// 薄く張った哨戒艦の脆さ 0..1＝vulnerabilityScale × spacing/(spacing+spacingRef) × 敵戦力。
        /// 間隔が広いほど互いに援護できず脆く、敵戦力が強いほど割を食う。
        /// </summary>
        public static float PicketVulnerability(float picketSpacing, float enemyStrength, SensorPicketParams p)
        {
            float spacing = Mathf.Max(0f, picketSpacing);
            float thin = spacing / (spacing + p.spacingRef);
            return Mathf.Clamp01(p.vulnerabilityScale * thin * Mathf.Clamp01(enemyStrength));
        }

        public static float PicketVulnerability(float picketSpacing, float enemyStrength)
            => PicketVulnerability(picketSpacing, enemyStrength, SensorPicketParams.Default);

        /// <summary>
        /// 哨戒線の隙を敵がすり抜ける度合い 0..1＝spacing/(spacing+gapRef) × 敵ステルス。
        /// 間隔が広いほど隙だらけ、敵の隠密性が高いほど抜けやすい。
        /// </summary>
        public static float GapPenetration(float picketSpacing, float enemyStealth, SensorPicketParams p)
        {
            float spacing = Mathf.Max(0f, picketSpacing);
            float gap = spacing / (spacing + p.gapRef);
            return Mathf.Clamp01(gap * Mathf.Clamp01(enemyStealth));
        }

        public static float GapPenetration(float picketSpacing, float enemyStealth)
            => GapPenetration(picketSpacing, enemyStealth, SensorPicketParams.Default);

        /// <summary>
        /// 誤警報率 0..1（星雲等のノイズ）＝(1-センサ品質) × (clutterWeight×ノイズ + (1-clutterWeight))。
        /// センサ品質が高いほど、環境ノイズが低いほど誤報は減る。
        /// </summary>
        public static float FalseAlarmRate(float sensorQuality, float environmentClutter, SensorPicketParams p)
        {
            float quality = Mathf.Clamp01(sensorQuality);
            float clutter = Mathf.Clamp01(environmentClutter);
            float noise = p.clutterWeight * clutter + (1f - p.clutterWeight);
            return Mathf.Clamp01((1f - quality) * noise);
        }

        public static float FalseAlarmRate(float sensorQuality, float environmentClutter)
            => FalseAlarmRate(sensorQuality, environmentClutter, SensorPicketParams.Default);

        /// <summary>
        /// 早期警戒の正味価値＝猶予 × (1-誤警報率)。誤報が多いほど猶予の価値が割り引かれる（狼少年効果）。
        /// </summary>
        public static float EarlyWarningValue(float detectionLeadTime, float falseAlarmRate)
        {
            float lead = Mathf.Max(0f, detectionLeadTime);
            return lead * (1f - Mathf.Clamp01(falseAlarmRate));
        }

        /// <summary>哨戒線を突破されたか＝隙のすり抜け度が閾値以上で true。</summary>
        public static bool IsScreenPenetrated(float gapPenetration, float threshold)
        {
            return Mathf.Clamp01(gapPenetration) >= threshold;
        }
    }
}
