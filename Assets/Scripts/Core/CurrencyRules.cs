using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通貨の年次進行（#通貨・配線の唯一の窓口）。財政の赤字を中央銀行が刷って賄うと物価が上がり
    /// （<see cref="InflationRules"/>＝貨幣数量説）、財政の健全度が為替を動かす
    /// （<see cref="FiscalRules.ExchangeRateFactor"/>＝財政悪化→通貨安）。数式は既存の純ロジックへ委譲し
    /// 二重実装しない。乱数なし・決定論・test-first。
    /// </summary>
    public static class CurrencyRules
    {
        /// <summary>想定実質成長（インフレ計算の供給側・控えめ）。</summary>
        public const float DefaultOutputGrowth = 0.02f;

        /// <summary>通貨を勢力へ用意する（固有名を <see cref="CurrencyNames"/> から決定論で割り当て・冪等）。</summary>
        public static CurrencyState Ensure(CurrencyState existing, Faction faction)
        {
            CurrencyState c = existing ?? new CurrencyState();
            if (string.IsNullOrEmpty(c.currencyName)) c.currencyName = CurrencyNames.For(faction);
            return c;
        }

        /// <summary>
        /// 通貨を1年ぶん進める：赤字（歳出&gt;歳入）を貨幣化＝通貨増発（対経済規模）→物価上昇／インフレ率、
        /// 通貨供給は赤字ぶん増える、財政健全度→為替係数。黒字なら増発0＝物価は均衡へ収束する。
        /// </summary>
        public static void TickYear(CurrencyState c, FiscalState fs, float economy, float dt)
        {
            if (c == null || fs == null || dt <= 0f) return;

            var ip = InflationParams.Default;
            var fp = FiscalRules.FiscalParams.Default;

            // 赤字を中央銀行が刷って埋める＝通貨増発（対経済規模 0..1）。黒字は0。
            float deficit = Mathf.Max(0f, fs.baseExpenditure - fs.revenue);
            float printing = economy > 0f ? Mathf.Clamp01(deficit / economy) : 0f;

            c.inflationRate = InflationRules.InflationRate(printing, DefaultOutputGrowth, ip);
            c.priceLevel = InflationRules.PriceLevelTick(c.priceLevel, printing, DefaultOutputGrowth, dt, ip);
            c.moneySupply = Mathf.Max(0f, c.moneySupply + deficit * dt);
            c.exchangeRate = economy > 0f ? FiscalRules.ExchangeRateFactor(fs, economy, fp) : 1f;
        }
    }
}
