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
        /// 為替クロスレート（#為替）＝<paramref name="baseC"/> 通貨1単位が <paramref name="quoteC"/> 通貨で何単位か。
        /// 各通貨の対基準為替係数（財政健全度由来）の比＝相対的な強さ。quote が無価値（≤0）は0、いずれか null は1。
        /// </summary>
        public static float CrossRate(CurrencyState baseC, CurrencyState quoteC)
        {
            if (baseC == null || quoteC == null) return 1f;
            float q = quoteC.exchangeRate;
            return q <= 0f ? 0f : Mathf.Max(0f, baseC.exchangeRate) / q;
        }

        /// <summary>
        /// 通貨を1年ぶん進める：赤字（歳出&gt;歳入）を貨幣化＝通貨増発（対経済規模）→物価上昇／インフレ率、
        /// 通貨供給は赤字ぶん増える、財政健全度→為替係数。黒字なら増発0＝物価は均衡へ収束する。
        /// <paramref name="marketPressure"/>（0..・市場の逼迫＝需要超）はコストプッシュとして増発相当に上乗せ（市場価格#179→物価）。
        /// </summary>
        public static void TickYear(CurrencyState c, FiscalState fs, float economy, float dt, float marketPressure = 0f)
        {
            if (c == null || fs == null || dt <= 0f) return;

            var ip = InflationParams.Default;
            var fp = FiscalRules.FiscalParams.Default;

            // 赤字を中央銀行が刷って埋める＝通貨増発（対経済規模 0..1）。黒字は0。市場の逼迫をコストプッシュで上乗せ。
            float deficit = Mathf.Max(0f, fs.baseExpenditure - fs.revenue);
            float printing = economy > 0f ? deficit / economy : 0f;
            printing = Mathf.Clamp01(printing + Mathf.Max(0f, marketPressure));

            c.inflationRate = InflationRules.InflationRate(printing, DefaultOutputGrowth, ip);
            c.priceLevel = InflationRules.PriceLevelTick(c.priceLevel, printing, DefaultOutputGrowth, dt, ip);
            c.moneySupply = Mathf.Max(0f, c.moneySupply + deficit * dt);
            c.exchangeRate = economy > 0f ? FiscalRules.ExchangeRateFactor(fs, economy, fp) : 1f;
        }

        /// <summary>
        /// 基軸通貨（#ReserveCurrencyRules 配線）：交易シェア・軍事覇権・発行体信認から基軸度を更新し、
        /// 世界交易量に応じた発行益（不労所得＝法外な特権）を返す（行き先＝国庫は呼び出し側）。
        /// </summary>
        public static float TickReserve(CurrencyState c, float tradeShare, float militaryHegemony, float trustInIssuer, float globalTradeVolume, float dt)
        {
            if (c == null || dt <= 0f) return 0f;
            c.reserveStatus = Mathf.Clamp01(ReserveCurrencyRules.ReserveStatusTick(
                c.reserveStatus, tradeShare, militaryHegemony, trustInIssuer, dt));
            c.seigniorageIncome = ReserveCurrencyRules.SeigniorageIncome(c.reserveStatus, Mathf.Max(0f, globalTradeVolume));
            return c.seigniorageIncome;
        }

        /// <summary>
        /// 通貨改鋳（#CoinageRules 配線）：品位（銀含有）を <paramref name="targetSilverContent"/> へ寄せ、期待(純=1)を
        /// 下回ると信認が崩れる／戻すと回復する。品位を下げたぶんの改鋳益（シニョリッジ）を返す（行き先＝国庫は呼び出し側）。
        /// </summary>
        public static float TickCoinage(CurrencyState c, float targetSilverContent, float dt)
        {
            if (c == null || dt <= 0f) return 0f;
            var p = CoinageParams.Default;
            float prevSilver = c.silverContent;
            c.silverContent = Mathf.Clamp01(Mathf.MoveTowards(c.silverContent, Mathf.Clamp01(targetSilverContent), dt));
            // 品位が期待(1.0)を下回ると信認が崩れ、戻すと回復する（露見の力学）。
            c.publicTrust = Mathf.Clamp01(CoinageRules.DebasementTick(c.publicTrust, c.silverContent, 1f, dt, p));
            // 改鋳益＝このtickで減らした品位ぶん×通貨供給（貴金属を抜いたぶんが発行益）。
            float minted = Mathf.Max(0f, prevSilver - c.silverContent) * Mathf.Max(0f, c.moneySupply);
            return minted;
        }

        /// <summary>信認による実質購買力（#CoinageRules）＝1未満なら通貨が額面通りに通らない（グレシャム＝悪貨が良貨を駆逐）。</summary>
        public static float RealValueFactor(CurrencyState c)
            => c == null ? 1f : CoinageRules.RealPurchasingPower(1f, c.publicTrust);
    }
}
