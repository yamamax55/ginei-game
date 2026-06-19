using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 商船団（交易船団）運航の調整値（#商船団）。船団効率の逓減・脅威の重み・損失率の鋭さ・
    /// 保険率・安全度のパラメータ。すべて ctor で安全側へクランプ。符号付き出力は -1..1。
    /// </summary>
    public readonly struct MerchantConvoyParams
    {
        /// <summary>運航効率の逓減指数（船数→効率。0.5＝平方根で大船団ほど効率逓減／1.0＝線形）。</summary>
        public readonly float efficiencyExponent;
        /// <summary>護衛比の上限（過剰護衛は手厚さ頭打ち＝1隻に無限の護衛は付かない）。</summary>
        public readonly float maxEscortRatio;
        /// <summary>航路露出の重み（露出が脅威をどれだけ増幅するか）。</summary>
        public readonly float exposureWeight;
        /// <summary>損失率の鋭さ（脆弱性→損失。1.0＝線形／大＝急峻）。</summary>
        public readonly float lossSharpness;
        /// <summary>損失率の上限（船団全損は避ける＝一部は逃げ延びる）。</summary>
        public readonly float maxLossRate;
        /// <summary>保険の基礎料率（損失0でも掛かる最低コスト＝積荷価値あたり）。</summary>
        public readonly float baseInsuranceRate;

        public MerchantConvoyParams(float efficiencyExponent, float maxEscortRatio, float exposureWeight,
            float lossSharpness, float maxLossRate, float baseInsuranceRate)
        {
            this.efficiencyExponent = Mathf.Clamp(efficiencyExponent, 0f, 4f);
            this.maxEscortRatio = Mathf.Clamp(maxEscortRatio, 0f, 10f);
            this.exposureWeight = Mathf.Clamp(exposureWeight, 0f, 4f);
            this.lossSharpness = Mathf.Clamp(lossSharpness, 0f, 4f);
            this.maxLossRate = Mathf.Clamp01(maxLossRate);
            this.baseInsuranceRate = Mathf.Clamp01(baseInsuranceRate);
        }

        /// <summary>既定：効率指数1.0（線形）・護衛比上限2.0・露出重み1.0・損失鋭さ1.0・損失上限0.9・基礎保険率0.02。</summary>
        public static MerchantConvoyParams Default => new MerchantConvoyParams(
            DefaultEfficiencyExponent, DefaultMaxEscortRatio, DefaultExposureWeight,
            DefaultLossSharpness, DefaultMaxLossRate, DefaultBaseInsuranceRate);

        public const float DefaultEfficiencyExponent = 1f;
        public const float DefaultMaxEscortRatio = 2f;
        public const float DefaultExposureWeight = 1f;
        public const float DefaultLossSharpness = 1f;
        public const float DefaultMaxLossRate = 0.9f;
        public const float DefaultBaseInsuranceRate = 0.02f;
    }

    /// <summary>
    /// 商船団（交易船団）運航の純ロジック（#商船団・唯一の窓口）＝<b>交易を担うコンボイの運航経済</b>。
    /// 船団規模と運航効率（大きすぎると統制負荷で非効率）・護衛の手厚さ・通商破壊への脆弱性・航路の安全度・
    /// 損失率と保険料・交易の純収益を扱う。<see cref="ConvoyDefenseRules"/>（船団護衛の<b>戦術</b>＝護る陣形）／
    /// <see cref="CommerceRaidingRules"/>（通商破壊＝<b>襲う側</b> #95）とは別＝こちらは<b>交易運航の経済</b>。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・決定論・test-first。
    /// </summary>
    public static class MerchantConvoyRules
    {
        /// <summary>既定パラメータで船団の運航効率。</summary>
        public static float ConvoyEfficiency(float shipCount, float coordinationOverhead)
            => ConvoyEfficiency(shipCount, coordinationOverhead, MerchantConvoyParams.Default);

        /// <summary>
        /// 船団の運航効率＝pow(船数,指数)÷(1+統制負荷)（非負）。船が増えるほど運ぶ量は増えるが、
        /// 統制負荷（隊列・速度合わせ・寄港調整）が大きいと逓減＝大きすぎる船団は非効率。
        /// </summary>
        public static float ConvoyEfficiency(float shipCount, float coordinationOverhead, MerchantConvoyParams p)
        {
            float ships = Mathf.Max(0f, shipCount);
            float overhead = Mathf.Max(0f, coordinationOverhead);
            float raw = Mathf.Pow(ships, p.efficiencyExponent) / (1f + overhead);
            return Mathf.Max(0f, raw);
        }

        /// <summary>既定パラメータで護衛の手厚さ。</summary>
        public static float EscortRatio(float escortStrength, float convoySize)
            => EscortRatio(escortStrength, convoySize, MerchantConvoyParams.Default);

        /// <summary>
        /// 護衛の手厚さ＝護衛戦力÷船団規模（0..maxEscortRatio で頭打ち）。船団規模0は護衛比0扱い。
        /// 規模が大きいほど同じ護衛では薄まる。過剰護衛は上限で頭打ち（無限には手厚くならない）。
        /// </summary>
        public static float EscortRatio(float escortStrength, float convoySize, MerchantConvoyParams p)
        {
            float escort = Mathf.Max(0f, escortStrength);
            float size = Mathf.Max(0f, convoySize);
            if (size <= 0f) return 0f;
            return Mathf.Clamp(escort / size, 0f, p.maxEscortRatio);
        }

        /// <summary>既定パラメータで襲撃の脅威。</summary>
        public static float RaiderThreat(float enemyRaiders, float routeExposure)
            => RaiderThreat(enemyRaiders, routeExposure, MerchantConvoyParams.Default);

        /// <summary>
        /// 襲撃の脅威＝敵通商破壊艦×(1＋露出重み×航路露出)（非負）。敵の通商破壊艦が多いほど、
        /// 航路が露出（前線・要衝・哨戒の隙）するほど脅威が増す。
        /// </summary>
        public static float RaiderThreat(float enemyRaiders, float routeExposure, MerchantConvoyParams p)
        {
            float raiders = Mathf.Max(0f, enemyRaiders);
            float exposure = Mathf.Clamp01(routeExposure);
            return raiders * (1f + p.exposureWeight * exposure);
        }

        /// <summary>既定パラメータで船団の脆弱性。</summary>
        public static float ConvoyVulnerability(float raiderThreat, float escortRatio)
            => ConvoyVulnerability(raiderThreat, escortRatio, MerchantConvoyParams.Default);

        /// <summary>
        /// 船団の脆弱性（0..1）＝脅威×(1-護衛被覆)を正規化。脅威が高く護衛が薄いほど脆い。
        /// 護衛被覆＝護衛比/(1+護衛比)（逓減）。脆弱＝threat×(1-cover)/(threat×(1-cover)+1)。
        /// </summary>
        public static float ConvoyVulnerability(float raiderThreat, float escortRatio, MerchantConvoyParams p)
        {
            float threat = Mathf.Max(0f, raiderThreat);
            float ratio = Mathf.Max(0f, escortRatio);
            float cover = ratio / (1f + ratio); // 0..1（護衛の被覆・逓減）
            float exposed = threat * (1f - cover);
            return Mathf.Clamp01(exposed / (exposed + 1f));
        }

        /// <summary>既定パラメータで損失率。</summary>
        public static float LossRate(float convoyVulnerability)
            => LossRate(convoyVulnerability, MerchantConvoyParams.Default);

        /// <summary>
        /// 船団の損失率（0..maxLossRate）＝pow(脆弱性,鋭さ)。脆弱性に応じて積荷/船が失われる割合。
        /// 鋭さ1.0は線形。全損は避ける（maxLossRate で頭打ち＝一部は逃げ延びる）。
        /// </summary>
        public static float LossRate(float convoyVulnerability, MerchantConvoyParams p)
        {
            float vuln = Mathf.Clamp01(convoyVulnerability);
            float raw = Mathf.Pow(vuln, p.lossSharpness);
            return Mathf.Clamp(raw, 0f, p.maxLossRate);
        }

        /// <summary>既定パラメータで保険料。</summary>
        public static float InsurancePremium(float lossRate, float cargoValue)
            => InsurancePremium(lossRate, cargoValue, MerchantConvoyParams.Default);

        /// <summary>
        /// 交易の保険料（非負）＝(基礎料率＋損失率)×積荷価値。損失率が高い危険航路ほど保険料が高騰、
        /// 無リスクでも基礎料率ぶんは掛かる（実費・事務）。
        /// </summary>
        public static float InsurancePremium(float lossRate, float cargoValue, MerchantConvoyParams p)
        {
            float loss = Mathf.Clamp01(lossRate);
            float value = Mathf.Max(0f, cargoValue);
            return (p.baseInsuranceRate + loss) * value;
        }

        /// <summary>既定パラメータで航路の安全度。</summary>
        public static float RouteSafety(float escortRatio, float raiderThreat, float navalControl)
            => RouteSafety(escortRatio, raiderThreat, navalControl, MerchantConvoyParams.Default);

        /// <summary>
        /// 航路の安全度（0..1）＝(護衛被覆×制宙権の傘)÷(1+脅威)。護衛が手厚く制宙権を握るほど安全、
        /// 脅威が高いほど危険。護衛被覆＝護衛比/(1+護衛比)・制宙権の傘＝(1+制宙権)で増幅。
        /// </summary>
        public static float RouteSafety(float escortRatio, float raiderThreat, float navalControl, MerchantConvoyParams p)
        {
            float ratio = Mathf.Max(0f, escortRatio);
            float threat = Mathf.Max(0f, raiderThreat);
            float control = Mathf.Clamp01(navalControl);
            float cover = ratio / (1f + ratio); // 0..1
            float guarded = cover * (1f + control); // 制宙権で底上げ
            return Mathf.Clamp01(guarded / (1f + threat));
        }

        /// <summary>既定パラメータで交易の純収益。</summary>
        public static float TradeRevenue(float convoyEfficiency, float cargoValue, float lossRate)
            => TradeRevenue(convoyEfficiency, cargoValue, lossRate, MerchantConvoyParams.Default);

        /// <summary>
        /// 交易の純収益（非負）＝運航効率×積荷価値×(1-損失率)。効率よく運び、損失が少ないほど儲かる。
        /// 損失率が高いと運んでも失う＝収益が削られる（保険料は <see cref="InsurancePremium"/> で別途差し引く想定）。
        /// </summary>
        public static float TradeRevenue(float convoyEfficiency, float cargoValue, float lossRate, MerchantConvoyParams p)
        {
            float eff = Mathf.Max(0f, convoyEfficiency);
            float value = Mathf.Max(0f, cargoValue);
            float loss = Mathf.Clamp01(lossRate);
            return Mathf.Max(0f, eff * value * (1f - loss));
        }
    }
}
