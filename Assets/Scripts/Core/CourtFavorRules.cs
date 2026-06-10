using UnityEngine;

namespace Ginei
{
    /// <summary>宮廷の寵愛・讒言の調整係数（宮廷寵愛システム）。</summary>
    public readonly struct CourtFavorParams
    {
        /// <summary>追従による寵の蓄積速度（per dt・近侍×追従は速い）。</summary>
        public readonly float flatteryGainRate;
        /// <summary>実績による寵の蓄積速度（per dt・追従より遅いが安定の源泉）。</summary>
        public readonly float meritGainRate;
        /// <summary>寵の自然減衰速度（per dt・「日は必ず動く」＝近侍を絶やせば寵は必ず冷める）。</summary>
        public readonly float favorDecayRate;
        /// <summary>寵臣専横の最大幅（寵の二乗×監督の緩みに掛かる）。</summary>
        public readonly float tyrannyScale;
        /// <summary>讒言による寵の失墜の最大幅（讒言の質×標的の寵×眼力の緩みに掛かる）。</summary>
        public readonly float slanderScale;
        /// <summary>追従で得た寵の不安定性（0..1・日なたの寵は一夜で消える）。</summary>
        public readonly float flatteryVolatility;
        /// <summary>実績で得た寵の不安定性（0..1・実績の寵は粘る）。</summary>
        public readonly float meritVolatility;
        /// <summary>陰謀度が半分に達する廷臣数（飽和曲線の半値・1以上）。</summary>
        public readonly float intrigueHalfCount;

        public CourtFavorParams(float flatteryGainRate, float meritGainRate, float favorDecayRate,
                                float tyrannyScale, float slanderScale,
                                float flatteryVolatility, float meritVolatility, float intrigueHalfCount)
        {
            this.flatteryGainRate = Mathf.Max(0f, flatteryGainRate);
            this.meritGainRate = Mathf.Max(0f, meritGainRate);
            this.favorDecayRate = Mathf.Max(0f, favorDecayRate);
            this.tyrannyScale = Mathf.Max(0f, tyrannyScale);
            this.slanderScale = Mathf.Max(0f, slanderScale);
            this.flatteryVolatility = Mathf.Clamp01(flatteryVolatility);
            this.meritVolatility = Mathf.Clamp01(meritVolatility);
            this.intrigueHalfCount = Mathf.Max(1f, intrigueHalfCount);
        }

        /// <summary>既定＝追従蓄積0.10/実績蓄積0.04/減衰0.02/専横0.8/讒言0.5/追従揺らぎ0.5/実績揺らぎ0.05/半値廷臣数4。</summary>
        public static CourtFavorParams Default => new CourtFavorParams(0.10f, 0.04f, 0.02f, 0.8f, 0.5f, 0.5f, 0.05f, 4f);
    }

    /// <summary>
    /// 宮廷の寵愛・讒言の純ロジック（寵臣の専横と失脚）。君主の寵を競う廷臣は近侍×追従で速く寵を得るが、
    /// 「寵は日なた＝当たれば暖かいが、日は必ず動く」＝寵は常に減衰し、君主のそばを離れれば必ず冷める。
    /// 追従で得た寵は一夜で消え、実績で得た寵は粘る（不安定性の差）。寵臣の専横は君主の監督が防ぎ、
    /// 讒言による失脚は君主の眼力が防波堤となる（眼力1なら讒言は無効）。
    /// <see cref="PowerRules"/>（実権構造＝傀儡/影の支配者の力学）とは別系統＝こちらは制度上の権力に依らない
    /// 「寵」という通貨を扱う。乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CourtFavorRules
    {
        /// <summary>
        /// 寵の時間発展（0..1）。蓄積＝近さ(proximity 0..1)×（追従(flattery 0..1)×flatteryGainRate＋
        /// 実績(merit 0..1)×meritGainRate）＝近侍×追従が速く・実績は遅い。同時に favorDecayRate で常に減衰
        /// ＝「日は必ず動く」：君主から遠ざかれば（proximity=0）寵は冷えるのみ。
        /// </summary>
        public static float FavorTick(float favor, float proximity, float flattery, float merit, float dt, CourtFavorParams p)
        {
            float f = Mathf.Clamp01(favor);
            float prox = Mathf.Clamp01(proximity);
            float gain = prox * (p.flatteryGainRate * Mathf.Clamp01(flattery) + p.meritGainRate * Mathf.Clamp01(merit));
            return Mathf.Clamp01(f + (gain - p.favorDecayRate) * Mathf.Max(0f, dt));
        }

        public static float FavorTick(float favor, float proximity, float flattery, float merit, float dt)
            => FavorTick(favor, proximity, flattery, merit, dt, CourtFavorParams.Default);

        /// <summary>
        /// 寵臣の専横（0..tyrannyScale）。寵の二乗×監督の緩み(1-oversight)＝深い寵ほど不釣り合いに増長し、
        /// 君主の監督(oversight 0..1)が行き届けば寵が深くても専横は抑えられる。
        /// </summary>
        public static float FavoriteTyranny(float favor, float oversight, CourtFavorParams p)
        {
            float f = Mathf.Clamp01(favor);
            float slack = 1f - Mathf.Clamp01(oversight);
            return f * f * slack * p.tyrannyScale;
        }

        public static float FavoriteTyranny(float favor, float oversight)
            => FavoriteTyranny(favor, oversight, CourtFavorParams.Default);

        /// <summary>
        /// 讒言の通り＝標的が失う寵（0..slanderScale）。讒言の質(slanderQuality 0..1)×標的の寵(targetFavor 0..1)
        /// ×眼力の緩み(1-monarchDiscernment)＝寵が高い者ほど落とせる落差が大きく讒言の的になる。
        /// 君主の眼力(monarchDiscernment 0..1)が防波堤＝眼力1なら讒言は無効（0）。
        /// </summary>
        public static float SlanderEffect(float slanderQuality, float targetFavor, float monarchDiscernment, CourtFavorParams p)
        {
            float q = Mathf.Clamp01(slanderQuality);
            float f = Mathf.Clamp01(targetFavor);
            float blind = 1f - Mathf.Clamp01(monarchDiscernment);
            return q * f * blind * p.slanderScale;
        }

        public static float SlanderEffect(float slanderQuality, float targetFavor, float monarchDiscernment)
            => SlanderEffect(slanderQuality, targetFavor, monarchDiscernment, CourtFavorParams.Default);

        /// <summary>
        /// 寵の不安定性（meritVolatility..flatteryVolatility）。寵の根拠が追従か実績か(basedOnFlattery 0..1)で
        /// 線形補間＝追従で得た寵は一夜で消え（高揺らぎ）、実績で得た寵は粘る（低揺らぎ）。
        /// </summary>
        public static float FavorVolatility(float basedOnFlattery, CourtFavorParams p)
            => Mathf.Lerp(p.meritVolatility, p.flatteryVolatility, Mathf.Clamp01(basedOnFlattery));

        public static float FavorVolatility(float basedOnFlattery)
            => FavorVolatility(basedOnFlattery, CourtFavorParams.Default);

        /// <summary>
        /// 宮廷の陰謀度（0..1）。廷臣数の飽和曲線 count/(count+intrigueHalfCount)×寵の一極集中
        /// (favorConcentration 0..1)＝日なたは一つ、競う者が多く寵が一人に集まるほど蹴落とし合いが激しい。
        /// 廷臣が居なければ（0人）陰謀も無い。
        /// </summary>
        public static float CourtIntrigueLevel(int courtierCount, float favorConcentration, CourtFavorParams p)
        {
            float count = Mathf.Max(0, courtierCount);
            float crowding = count / (count + p.intrigueHalfCount);
            return Mathf.Clamp01(crowding * Mathf.Clamp01(favorConcentration));
        }

        public static float CourtIntrigueLevel(int courtierCount, float favorConcentration)
            => CourtIntrigueLevel(courtierCount, favorConcentration, CourtFavorParams.Default);
    }
}
