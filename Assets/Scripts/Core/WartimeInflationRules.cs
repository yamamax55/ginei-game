using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦時インフレ（戦費調達による通貨価値下落）の調整値。戦費赤字の通貨増発比率・増発がインフレへ転じる感応度・
    /// 国債吸収の効き・ハイパーインフレの危険度ゲイン。すべて ctor で安全側へクランプ。
    /// </summary>
    public readonly struct WartimeInflationParams
    {
        /// <summary>戦費赤字（税収で賄えない不足）のうち通貨増発で賄う割合（残りは国債・借入）。</summary>
        public readonly float printShare;
        /// <summary>通貨増発がインフレ率へ転じる感応度（増発／実質生産の比に掛ける）。</summary>
        public readonly float inflationSensitivity;
        /// <summary>国債が過剰流動性を吸収する効率（信認1・最大吸収のときの係数）。</summary>
        public readonly float bondAbsorbFactor;
        /// <summary>財政信認が崩れたときハイパーインフレへ振れる危険度ゲイン。</summary>
        public readonly float hyperRiskGain;

        public WartimeInflationParams(float printShare, float inflationSensitivity,
            float bondAbsorbFactor, float hyperRiskGain)
        {
            this.printShare = Mathf.Clamp01(printShare);
            this.inflationSensitivity = Mathf.Clamp(inflationSensitivity, 0f, 2f);
            this.bondAbsorbFactor = Mathf.Clamp01(bondAbsorbFactor);
            this.hyperRiskGain = Mathf.Clamp(hyperRiskGain, 0f, 5f);
        }

        /// <summary>既定：増発割合0.6・感応度0.5・国債吸収0.8・ハイパー危険ゲイン2.0。</summary>
        public static WartimeInflationParams Default => new WartimeInflationParams(
            DefaultPrintShare, DefaultInflationSensitivity, DefaultBondAbsorbFactor, DefaultHyperRiskGain);

        public const float DefaultPrintShare = 0.6f;
        public const float DefaultInflationSensitivity = 0.5f;
        public const float DefaultBondAbsorbFactor = 0.8f;
        public const float DefaultHyperRiskGain = 2f;
    }

    /// <summary>
    /// 戦時インフレの純ロジック（唯一の窓口）＝<b>戦争を財政赤字・通貨増発で賄うと通貨価値が下落しインフレが進む</b>。
    /// 物価高騰・購買力低下が国民生活を圧迫し、過度な戦費は実質戦費を膨らませて経済を蝕む。国債・配給で過剰流動性を吸収し、
    /// 財政信認が崩れればハイパーインフレへ陥る。
    /// <b>分担</b>：<see cref="FiscalRules"/>／<c>CurrencyRules</c>（財政・通貨・既存）とは別＝こちらは<b>戦時の通貨増発インフレ</b>。
    /// <see cref="WarEconomyRules"/>（軍需転換＝生産能力の振り向け）とは別＝こちらは<b>財政面</b>（戦費調達と通貨価値）。
    /// 盤面非依存の plain 引数・実効値パターン（基準値非破壊）・乱数なし・test-first。
    /// </summary>
    public static class WartimeInflationRules
    {
        /// <summary>既定パラメータで戦費の通貨増発ぶん。</summary>
        public static float MoneyPrinting(float warDeficit, float taxRevenue)
            => MoneyPrinting(warDeficit, taxRevenue, WartimeInflationParams.Default);

        /// <summary>
        /// 戦費の通貨増発ぶん。printing＝max(0, 戦費赤字−税収)×増発割合。
        /// 税収で賄えない不足ぶんの一部を中央銀行が刷って埋める＝戦費の貨幣化。
        /// </summary>
        public static float MoneyPrinting(float warDeficit, float taxRevenue, WartimeInflationParams p)
        {
            float deficit = Mathf.Max(0f, warDeficit);
            float tax = Mathf.Max(0f, taxRevenue);
            float gap = Mathf.Max(0f, deficit - tax);
            return gap * p.printShare;
        }

        /// <summary>既定パラメータでインフレ率。</summary>
        public static float InflationRate(float moneyPrinting, float realOutput)
            => InflationRate(moneyPrinting, realOutput, WartimeInflationParams.Default);

        /// <summary>
        /// インフレ率（0..1）。rate＝clamp01(増発／実質生産×感応度)。
        /// 増発しても実質生産（財・サービス）が支えれば物価は緩和される＝財が増発に追いつけば吸収される。
        /// </summary>
        public static float InflationRate(float moneyPrinting, float realOutput, WartimeInflationParams p)
        {
            float printing = Mathf.Max(0f, moneyPrinting);
            float output = Mathf.Max(0.0001f, realOutput);
            return Mathf.Clamp01(printing / output * p.inflationSensitivity);
        }

        /// <summary>
        /// 物価水準の上昇。next＝現在物価×(1＋インフレ率×dt)。
        /// インフレが続くほど物価が積み上がる（フレーム非依存＝dt 比例）。物価は下限0。
        /// </summary>
        public static float PriceLevel(float currentPriceLevel, float inflationRate, float dt)
        {
            float price = Mathf.Max(0f, currentPriceLevel);
            float rate = Mathf.Clamp01(inflationRate);
            float step = Mathf.Max(0f, dt);
            return price * (1f + rate * step);
        }

        /// <summary>
        /// 通貨の購買力（0..1）＝1／(1＋インフレ率)。
        /// インフレが進むほど同じ通貨で買える量が減る＝通貨価値の下落。インフレ0なら1（不変）。
        /// </summary>
        public static float PurchasingPowerErosion(float inflationRate)
        {
            float rate = Mathf.Clamp01(inflationRate);
            return 1f / (1f + rate);
        }

        /// <summary>既定パラメータで国債による過剰流動性の吸収。</summary>
        public static float WarBondAbsorption(float bondIssuance, float publicConfidence)
            => WarBondAbsorption(bondIssuance, publicConfidence, WartimeInflationParams.Default);

        /// <summary>
        /// 国債で吸収する過剰流動性。absorbed＝国債発行×公的信認×吸収効率。
        /// 信認があれば国民が国債を買い余剰通貨が回収される（インフレ緩和）。信認0なら吸収されない（売れ残り）。
        /// </summary>
        public static float WarBondAbsorption(float bondIssuance, float publicConfidence, WartimeInflationParams p)
        {
            float bond = Mathf.Max(0f, bondIssuance);
            float confidence = Mathf.Clamp01(publicConfidence);
            return bond * confidence * p.bondAbsorbFactor;
        }

        /// <summary>
        /// インフレで膨らむ実質戦費。real＝名目戦費×(1＋インフレ率)。
        /// 物価高騰で同じ作戦の調達費が嵩む＝戦争が長引くほど実質的な負担が重くなる。
        /// </summary>
        public static float RealWarCost(float nominalWarCost, float inflationRate)
        {
            float nominal = Mathf.Max(0f, nominalWarCost);
            float rate = Mathf.Clamp01(inflationRate);
            return nominal * (1f + rate);
        }

        /// <summary>既定パラメータでハイパーインフレの危険度。</summary>
        public static float HyperinflationRisk(float inflationRate, float fiscalCredibility)
            => HyperinflationRisk(inflationRate, fiscalCredibility, WartimeInflationParams.Default);

        /// <summary>
        /// ハイパーインフレの危険度（0..1）。risk＝clamp01(インフレ率×(1−財政信認)×危険ゲイン)。
        /// 財政信認が崩れるほど（通貨が信じられなくなるほど）インフレが自己加速し制御不能へ向かう。信認1なら危険0。
        /// </summary>
        public static float HyperinflationRisk(float inflationRate, float fiscalCredibility, WartimeInflationParams p)
        {
            float rate = Mathf.Clamp01(inflationRate);
            float credibility = Mathf.Clamp01(fiscalCredibility);
            return Mathf.Clamp01(rate * (1f - credibility) * p.hyperRiskGain);
        }

        /// <summary>
        /// インフレが制御不能（スパイラル）か（bool）＝インフレ率がしきい値を超えている。
        /// しきい値を超えると物価と賃金が追いかけ合い止まらなくなる＝早期に抑えるべき境界。
        /// </summary>
        public static bool IsInflationSpiraling(float inflationRate, float threshold = 0.5f)
        {
            float rate = Mathf.Clamp01(inflationRate);
            float limit = Mathf.Clamp01(threshold);
            return rate > limit;
        }
    }
}
