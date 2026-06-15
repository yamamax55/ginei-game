using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 船団護衛の調整値（#船団護衛）。護衛密度・傘の効き・損害局限・鈍重化・安全判定のパラメータ。
    /// 符号付きの分散↔集結指標は -1..1。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct ConvoyDefenseParams
    {
        /// <summary>船団自衛の底上げ（護衛0でも船団自身がわずかに撥ね返す＝密度0回避）。</summary>
        public readonly float selfDefenseFloor;
        /// <summary>傘の効きの指数（密度→被覆。0.5＝平方根で穏やか／1.0＝線形）。</summary>
        public readonly float coverageExponent;
        /// <summary>傘の被覆上限（完全には覆い切れない＝穴は残る）。</summary>
        public readonly float maxCoverage;
        /// <summary>固めた護衛による鈍重化の強さ（護衛比あたりの減速量）。</summary>
        public readonly float ponderousness;
        /// <summary>鈍重化の下限（これ以下には遅くならない）。</summary>
        public readonly float minSpeedFactor;

        public ConvoyDefenseParams(float selfDefenseFloor, float coverageExponent, float maxCoverage,
            float ponderousness, float minSpeedFactor)
        {
            this.selfDefenseFloor = Mathf.Clamp01(selfDefenseFloor);
            this.coverageExponent = Mathf.Clamp(coverageExponent, 0f, 4f);
            this.maxCoverage = Mathf.Clamp01(maxCoverage);
            this.ponderousness = Mathf.Clamp(ponderousness, 0f, 1f);
            this.minSpeedFactor = Mathf.Clamp01(minSpeedFactor);
        }

        /// <summary>既定：自衛底上げ0.1・被覆指数0.5（平方根）・被覆上限0.95・鈍重化0.4・速度下限0.4。</summary>
        public static ConvoyDefenseParams Default => new ConvoyDefenseParams(
            DefaultSelfDefenseFloor, DefaultCoverageExponent, DefaultMaxCoverage,
            DefaultPonderousness, DefaultMinSpeedFactor);

        public const float DefaultSelfDefenseFloor = 0.1f;
        public const float DefaultCoverageExponent = 0.5f;
        public const float DefaultMaxCoverage = 0.95f;
        public const float DefaultPonderousness = 0.4f;
        public const float DefaultMinSpeedFactor = 0.4f;
    }

    /// <summary>
    /// 船団護衛の純ロジック（#船団護衛・唯一の窓口）＝<b>輸送船団を護る側の防御陣形と損害局限</b>。
    /// 護衛艦の密度・配置（分散しすぎると穴）・船団規模で襲撃に対する防御力が決まる。護衛が薄いと各個に食われ、
    /// 固めすぎると鈍重になる（トレードオフ）。<see cref="CommerceRaidingRules"/>（通商破壊＝<b>襲う側</b> #95）とは別＝
    /// こちらは<b>護る側</b>の防御モデル。混成禁止 #883＝護衛は別部隊で随伴（<see cref="ShipRoleRules"/>）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・test-first。
    /// </summary>
    public static class ConvoyDefenseRules
    {
        /// <summary>船団規模あたりの護衛密度（護衛戦力÷船団規模）。船団0は密度0扱い。</summary>
        public static float EscortDensity(float escortStrength, float convoySize)
            => EscortDensity(escortStrength, convoySize, ConvoyDefenseParams.Default);

        /// <summary>
        /// 船団規模あたりの護衛密度＝escort/convoy ＋船団自衛の底上げ。
        /// 規模が大きいほど同じ護衛では密度が薄まる（各個撃破の温床）。
        /// </summary>
        public static float EscortDensity(float escortStrength, float convoySize, ConvoyDefenseParams p)
        {
            float escort = Mathf.Max(0f, escortStrength);
            float size = Mathf.Max(0f, convoySize);
            if (size <= 0f) return 0f;
            return escort / size + p.selfDefenseFloor;
        }

        /// <summary>既定パラメータで船団を覆う護衛の傘（被覆率0..maxCoverage）。</summary>
        public static float ScreenCoverage(float escortDensity, float convoyDispersion)
            => ScreenCoverage(escortDensity, convoyDispersion, ConvoyDefenseParams.Default);

        /// <summary>
        /// 船団を覆う護衛の傘（被覆率0..maxCoverage）。密度が高いほど厚いが、船団が分散（dispersion 大）するほど
        /// 同じ護衛では穴があく（薄まる）。被覆＝pow(密度,指数)÷(1+分散)。完全には覆い切れない（maxCoverage で頭打ち）。
        /// </summary>
        public static float ScreenCoverage(float escortDensity, float convoyDispersion, ConvoyDefenseParams p)
        {
            float density = Mathf.Max(0f, escortDensity);
            float dispersion = Mathf.Max(0f, convoyDispersion);
            float raw = Mathf.Pow(density, p.coverageExponent) / (1f + dispersion);
            return Mathf.Clamp(raw, 0f, p.maxCoverage);
        }

        /// <summary>
        /// 襲撃を撥ね返す度合い（0..1）。傘が厚いほど高く、襲撃側戦力が大きいほど低い。
        /// repulse＝coverage÷(coverage＋raider正規化)。傘1.0でも強大な襲撃は完全には防げない。
        /// </summary>
        public static float RaiderRepulse(float screenCoverage, float raiderStrength)
        {
            float coverage = Mathf.Clamp01(screenCoverage);
            float raider = Mathf.Max(0f, raiderStrength);
            float denom = coverage + raider;
            if (denom <= 0f) return 1f; // 襲撃も傘も無し＝損害無し＝撥ね返した扱い
            return Mathf.Clamp01(coverage / denom);
        }

        /// <summary>
        /// 襲撃を受けた時の船団損害（艦数相当・非負）。傘の穴（1-coverage）に襲撃側戦力が突き込む。
        /// 損害＝raider×(1-coverage)（船団規模で頭打ち＝壊滅以上は出さない）。傘が厚いほど損害局限。
        /// </summary>
        public static float LossesIfRaided(float raiderStrength, float screenCoverage, float convoySize)
        {
            float raider = Mathf.Max(0f, raiderStrength);
            float coverage = Mathf.Clamp01(screenCoverage);
            float size = Mathf.Max(0f, convoySize);
            float losses = raider * (1f - coverage);
            return Mathf.Clamp(losses, 0f, size);
        }

        /// <summary>既定パラメータで護衛を固めた時の船団速度倍率。</summary>
        public static float ConvoySpeed(float escortStrength, float convoyBaseSpeed)
            => ConvoySpeed(escortStrength, convoyBaseSpeed, ConvoyDefenseParams.Default);

        /// <summary>
        /// 護衛を固めると船団が鈍重になる＝実効速度（基準速度×減速倍率）。護衛比が大きいほど遅いが minSpeedFactor で下げ止まる。
        /// 倍率＝1/(1＋ponderousness×護衛比)（最低 minSpeedFactor）。基準速度は非破壊（実効値パターン）。
        /// </summary>
        public static float ConvoySpeed(float escortStrength, float convoyBaseSpeed, ConvoyDefenseParams p)
        {
            float escort = Mathf.Max(0f, escortStrength);
            float baseSpeed = Mathf.Max(0f, convoyBaseSpeed);
            float factor = 1f / (1f + p.ponderousness * escort);
            factor = Mathf.Max(p.minSpeedFactor, factor);
            return baseSpeed * factor;
        }

        /// <summary>
        /// 分散（被害局限）か集結（護衛集中）かの指標（-1分散／+1集結）。脅威が高いほど集結して護衛を集中、
        /// 低ければ分散して被害を局限（一度に全部は食われない）。船団が大きいほど集結の利が増す（傘を厚くしやすい）。
        /// </summary>
        public static float DispersalVsConcentration(float threatLevel, float convoySize)
        {
            float threat = Mathf.Clamp01(threatLevel);
            float size = Mathf.Max(0f, convoySize);
            // 規模が大きいほど集結寄りへ微補正（0..0.2）。
            float sizeBias = Mathf.Clamp01(size / (size + 10f)) * 0.2f;
            float value = (threat - 0.5f) * 2f + sizeBias;
            return Mathf.Clamp(value, -1f, 1f);
        }

        /// <summary>
        /// 護衛側の消耗（艦数相当・非負）。襲撃側戦力が大きいほど、護衛が薄いほど護衛が削られる。
        /// 消耗＝raider²/(raider＋escort)（護衛が殿として食い止めるほど自らも擦り減る）。護衛0でも崩壊上限は襲撃側戦力。
        /// </summary>
        public static float EscortAttrition(float raiderStrength, float escortStrength)
        {
            float raider = Mathf.Max(0f, raiderStrength);
            float escort = Mathf.Max(0f, escortStrength);
            float denom = raider + escort;
            if (denom <= 0f) return 0f;
            return raider * raider / denom;
        }

        /// <summary>
        /// 船団が安全か（bool）＝撥ね返し度合いがしきい値以上。傘が薄い／襲撃側が強大なら危険（false）。
        /// </summary>
        public static bool IsConvoySafe(float screenCoverage, float raiderStrength, float threshold = 0.5f)
        {
            float repulse = RaiderRepulse(screenCoverage, raiderStrength);
            return repulse >= Mathf.Clamp01(threshold);
        }
    }
}
