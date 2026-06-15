using UnityEngine;

namespace Ginei
{
    /// <summary>偵察幕（スクリーニング）の調整係数＝前衛が本隊を隠し敵索敵を遅らせるレイヤー。</summary>
    public readonly struct ScreeningParams
    {
        /// <summary>隠蔽度の本隊サイズ重み（大きいほど大部隊が隠しにくくなる）。</summary>
        public readonly float sizeWeight;
        /// <summary>消耗率（敵接触圧 1 あたりに幕が失う割合の係数）。</summary>
        public readonly float attritionRate;
        /// <summary>早期警戒時間のスケール（幕の濃さ÷接近速度に掛ける）。</summary>
        public readonly float warningScale;
        /// <summary>偵察完了を遅らせる時間の上限（幕が濃いほどここへ漸近）。</summary>
        public readonly float maxDelay;
        /// <summary>遅延が半分になる幕の濃さ（delay の半減点）。</summary>
        public readonly float delayHalf;

        public ScreeningParams(float sizeWeight, float attritionRate, float warningScale, float maxDelay, float delayHalf)
        {
            this.sizeWeight = Mathf.Max(0f, sizeWeight);
            this.attritionRate = Mathf.Max(0f, attritionRate);
            this.warningScale = Mathf.Max(0f, warningScale);
            this.maxDelay = Mathf.Max(0f, maxDelay);
            this.delayHalf = Mathf.Max(0.01f, delayHalf);
        }

        /// <summary>既定＝サイズ重み1・消耗率0.5・警戒スケール1・最大遅延10・遅延半減点20。</summary>
        public static ScreeningParams Default => new ScreeningParams(1f, 0.5f, 1f, 10f, 20f);
    }

    /// <summary>
    /// 偵察幕（スクリーニング）の純ロジック＝前衛の軽快な部隊が敵偵察を妨げ本隊の規模・配置を隠す。
    /// 偵察戦は「幕を張る側」と「貫く側」のせめぎ合い＝幕が濃いほど本隊は過小に見え敵の索敵は遅れるが、
    /// 軽快ゆえ接触で消耗して破れ、薄れれば本隊が露呈する。乱数は持たず決定論。実効値パターン（真値非破壊）。
    /// 偵察そのものの推定誤差を扱う <see cref="ReconRules"/> とは別＝こちらは幕を張る側の能動的な防諜（遮蔽）レイヤー。
    /// 会戦の物理的な見え方を扱う <see cref="FogOfWarRules"/> とも別＝こちらは前衛による能動的遮蔽に特化。
    /// 盤面非依存の plain 引数・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ScreeningRules
    {
        /// <summary>
        /// 偵察幕の濃さ＝軽快な部隊数 screenUnits × 機動補正。機動 mobility=50 で等倍、100 で1.5倍、0 で0.5倍。
        /// 軽快（高機動）な部隊ほど薄い頭数でも濃い幕を張れる。負にはならない。
        /// </summary>
        public static float ScreenStrength(float screenUnits, float mobility, ScreeningParams p)
        {
            float units = Mathf.Max(0f, screenUnits);
            float mobFactor = 1f + (Mathf.Clamp(mobility, 0f, 100f) - 50f) / 100f; // 0.5..1.5
            return Mathf.Max(0f, units * mobFactor);
        }

        public static float ScreenStrength(float screenUnits, float mobility)
            => ScreenStrength(screenUnits, mobility, ScreeningParams.Default);

        /// <summary>
        /// 本隊を隠す度合い 0..1＝幕の濃さ /（幕の濃さ＋本隊サイズ×sizeWeight）。
        /// 大部隊ほど隠しにくく（分母が増える）、幕が濃いほど隠せる。幕ゼロで0。
        /// </summary>
        public static float Concealment(float screenStrength, float mainForceSize, ScreeningParams p)
        {
            float screen = Mathf.Max(0f, screenStrength);
            float main = Mathf.Max(0f, mainForceSize) * p.sizeWeight;
            float denom = screen + main;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(screen / denom);
        }

        public static float Concealment(float screenStrength, float mainForceSize)
            => Concealment(screenStrength, mainForceSize, ScreeningParams.Default);

        /// <summary>
        /// 敵偵察が幕を貫く確率 0..1＝敵偵察力 /（敵偵察力＋幕の濃さ）。
        /// 敵偵察が強いほど貫き、幕が濃いほど防ぐ。拮抗で0.5、両方ゼロで0。
        /// </summary>
        public static float PenetrationChance(float enemyReconStrength, float screenStrength)
        {
            float recon = Mathf.Max(0f, enemyReconStrength);
            float screen = Mathf.Max(0f, screenStrength);
            float denom = recon + screen;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(recon / denom);
        }

        /// <summary>
        /// 相手に見える本隊規模＝隠蔽で過小に・貫通で正確に近づく。
        /// 貫通 1 で真値どおり、貫通 0 で trueSize×(1-concealment)（隠蔽ぶん過小評価）。負にはならない。
        /// </summary>
        public static float RevealedSize(float trueSize, float concealment, float penetration)
        {
            float size = Mathf.Max(0f, trueSize);
            float conceal = Mathf.Clamp01(concealment);
            float pen = Mathf.Clamp01(penetration);
            float factor = Mathf.Lerp(1f - conceal, 1f, pen); // 過小(1-conceal)→正確(1)
            return Mathf.Max(0f, size * factor);
        }

        /// <summary>
        /// 幕の消耗量＝接触圧 enemyPressure に比例して失う濃さ。軽快ゆえ脆い（高い消耗率）。
        /// 一度に幕の濃さを超えて失うことはない（残量でキャップ）。
        /// </summary>
        public static float ScreenAttrition(float screenStrength, float enemyPressure, ScreeningParams p)
        {
            float screen = Mathf.Max(0f, screenStrength);
            float pressure = Mathf.Max(0f, enemyPressure);
            float loss = screen * Mathf.Clamp01(pressure * p.attritionRate);
            return Mathf.Clamp(loss, 0f, screen);
        }

        public static float ScreenAttrition(float screenStrength, float enemyPressure)
            => ScreenAttrition(screenStrength, enemyPressure, ScreeningParams.Default);

        /// <summary>
        /// 幕が敵接近を早期警戒する時間＝幕の濃さ ÷ 敵接近速度 × warningScale。
        /// 濃い幕は遠くで敵を捉え（警戒時間↑）、速い接近は警戒時間を縮める。
        /// </summary>
        public static float EarlyWarning(float screenStrength, float enemyApproachSpeed, ScreeningParams p)
        {
            float screen = Mathf.Max(0f, screenStrength);
            float speed = Mathf.Max(0.01f, enemyApproachSpeed);
            return screen / speed * p.warningScale;
        }

        public static float EarlyWarning(float screenStrength, float enemyApproachSpeed)
            => EarlyWarning(screenStrength, enemyApproachSpeed, ScreeningParams.Default);

        /// <summary>
        /// 敵の偵察完了を遅らせる時間 0..maxDelay＝幕の濃さ /（濃さ＋delayHalf）×maxDelay。
        /// 濃さ delayHalf で半分の遅延、濃いほど maxDelay へ漸近、幕ゼロで0。
        /// </summary>
        public static float DelayImposed(float screenStrength, ScreeningParams p)
        {
            float screen = Mathf.Max(0f, screenStrength);
            return p.maxDelay * (screen / (screen + p.delayHalf));
        }

        public static float DelayImposed(float screenStrength) => DelayImposed(screenStrength, ScreeningParams.Default);

        /// <summary>幕が破れた判定＝幕の濃さが閾値未満なら true（本隊が露呈する）。</summary>
        public static bool IsScreenBroken(float screenStrength, float threshold)
        {
            return Mathf.Max(0f, screenStrength) < threshold;
        }
    }
}
