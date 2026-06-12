using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 市場・物流の需要駆動（POPDEM-5・#2042・#179/#94/#95 連携・純ロジック）。
    /// POP の要求物資を市場#179 の需要側として接続し、需給で価格が動く（不足で価格↑）。補給線#94/通商#95 が不足を埋める（または断たれて飢える）。
    /// 既存 <see cref="MarketRules.ClearingPrice"/>/補給を窓口に駆動（並行システムを作らない）。物流は集約（船団マイクロ無し）。test-first。
    /// </summary>
    public static class ConsumptionMarketRules
    {
        /// <summary>POP需要が動かす市場価格＝既存 <see cref="MarketRules.ClearingPrice"/>（需給均衡・不足で高騰）。</summary>
        public static float DemandDrivenPrice(float supply, float demand, float basePrice)
            => MarketRules.ClearingPrice(supply, demand, basePrice, MarketRules.MarketParams.Default);

        /// <summary>通商破壊#95/封鎖後の供給＝基礎供給×(1−封鎖率)（補給線#94 が断たれると前線惑星が飢える）。</summary>
        public static float SuppliedAfterBlockade(float baseSupply, float blockadeFraction)
            => Mathf.Max(0f, baseSupply) * (1f - Mathf.Clamp01(blockadeFraction));

        /// <summary>交易による流入＝min(地域の不足, 他星系の余剰, 交易容量)（余剰から不足へ流れ不足を埋める・#94/#95）。</summary>
        public static float TradeInflow(float localDeficit, float availableSurplus, float tradeCapacity)
            => Mathf.Max(0f, Mathf.Min(Mathf.Min(Mathf.Max(0f, localDeficit), Mathf.Max(0f, availableSurplus)), Mathf.Max(0f, tradeCapacity)));
    }
}
