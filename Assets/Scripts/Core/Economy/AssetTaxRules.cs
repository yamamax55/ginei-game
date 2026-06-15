using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 財産税の純ロジック（#163/#917 連携・唯一の窓口）。資産→税→国庫の環を閉じる：<b>固定資産税</b>（保有する土地/資産の評価額に毎年課税）・
    /// <b>富裕税</b>（純資産#2056 が一定額を超えるぶんに毎年課税＝累進・格差#917 を縮める）・<b>譲渡益課税</b>（売却益に課税）・
    /// <b>相続税</b>（相続財産が控除を超えるぶんに課税・#152）。税額を算定するだけ＝徴収（owner の現金を引き国庫#163 へ）は呼び側。
    /// 評価/純資産は <see cref="NetWorthRules"/>・<see cref="RealEstateRules.CapitalGain"/> へ委譲し二重実装しない。集約で課税し個別申告へ降りない（タイクン化回避）。test-first。
    /// </summary>
    public static class AssetTaxRules
    {
        /// <summary>調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct AssetTaxParams
        {
            public readonly float propertyTaxRate;   // 固定資産税率（保有資産評価×これ・年）
            public readonly float wealthTaxRate;      // 富裕税率（控除超過の純資産×これ・年）
            public readonly float capitalGainsRate;   // 譲渡益課税率（売却益×これ）
            public readonly float inheritanceRate;    // 相続税率（控除超過の相続財産×これ）

            public AssetTaxParams(float propertyTaxRate, float wealthTaxRate, float capitalGainsRate, float inheritanceRate)
            {
                this.propertyTaxRate = Mathf.Clamp01(propertyTaxRate);
                this.wealthTaxRate = Mathf.Clamp01(wealthTaxRate);
                this.capitalGainsRate = Mathf.Clamp01(capitalGainsRate);
                this.inheritanceRate = Mathf.Clamp01(inheritanceRate);
            }

            /// <summary>既定＝固定資産税1%/富裕税1%/譲渡益20%/相続税30%（年）。</summary>
            public static AssetTaxParams Default => new AssetTaxParams(0.01f, 0.01f, 0.20f, 0.30f);
        }

        /// <summary>固定資産税＝保有資産の評価額×税率（年・非負）。</summary>
        public static float PropertyTax(float assessedValue, float rate)
            => Mathf.Max(0f, assessedValue) * Mathf.Clamp01(rate);

        /// <summary>富裕税＝(純資産−控除)×税率（控除超過ぶんのみ＝累進・年・非負）。</summary>
        public static float WealthTax(float netWorth, float exemption, float rate)
            => Mathf.Max(0f, netWorth - Mathf.Max(0f, exemption)) * Mathf.Clamp01(rate);

        /// <summary>譲渡益課税＝max(0, 売却益)×税率（益が出たときのみ）。</summary>
        public static float CapitalGainsTax(float gain, float rate)
            => Mathf.Max(0f, gain) * Mathf.Clamp01(rate);

        /// <summary>相続税＝(相続財産−控除)×税率（控除超過ぶんのみ＝累進・非負）。</summary>
        public static float InheritanceTax(float estateValue, float exemption, float rate)
            => Mathf.Max(0f, estateValue - Mathf.Max(0f, exemption)) * Mathf.Clamp01(rate);

        /// <summary>年次の財産税合計（固定資産税＋富裕税）。徴収は呼び側＝owner の現金を引き国庫へ。</summary>
        public static float AnnualWealthAndPropertyTax(float assessedValue, float netWorth, float wealthExemption, AssetTaxParams p)
            => PropertyTax(assessedValue, p.propertyTaxRate) + WealthTax(netWorth, wealthExemption, p.wealthTaxRate);
    }
}
