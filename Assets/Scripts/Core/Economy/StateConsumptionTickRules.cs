using System.Collections.Generic;

namespace Ginei
{
    /// <summary>国家の行政物資消費の結果（STATEDEM-6・#2077）。資源別の充足/不足＋総合充足。</summary>
    public struct StateConsumptionResult
    {
        public float overall;            // 総合充足（最小律）
        public float shortageSupplies;   // 物資の不足
        public float shortageAmmo;       // 弾薬の不足
        public float shortageFuel;       // 燃料の不足

        public bool HasShortage => shortageSupplies > 0f || shortageAmmo > 0f || shortageFuel > 0f;
    }

    /// <summary>
    /// 国家・惑星の行政物資消費の暦境界オーケストレータ（STATEDEM-6・#2077 配線・純ロジック）。
    /// 国家の総需要（<see cref="StateMaterialDemandRules"/>）を在庫から消費し（<see cref="StateConsumptionFulfillmentRules"/>）、
    /// 総合充足と資源別不足を返す。効果（安定#109/支持#113/産出#93 ペナルティ）は呼び側が <see cref="StateConsumptionEffectRules"/> で適用。
    /// <b>薄い窓口</b>＝判定は各ルールへ委譲。test-first。
    /// </summary>
    public static class StateConsumptionTickRules
    {
        /// <summary>
        /// 1tick：国家の行政物資需要を在庫から消費し結果を返す（消費前の在庫で総合充足を測る）。
        /// </summary>
        public static StateConsumptionResult TickState(IReadOnlyList<Province> provinces, int systemCount, ResourceStockpile stock)
        {
            float dS = StateMaterialDemandRules.TotalStateDemand(provinces, systemCount, ResourceType.物資);
            float dA = StateMaterialDemandRules.TotalStateDemand(provinces, systemCount, ResourceType.弾薬);
            float dF = StateMaterialDemandRules.TotalStateDemand(provinces, systemCount, ResourceType.燃料);

            var result = new StateConsumptionResult
            {
                overall = StateConsumptionFulfillmentRules.OverallFulfillment(stock, dS, dA, dF)
            };

            if (stock != null)
            {
                result.shortageSupplies = StateConsumptionFulfillmentRules.Consume(stock, ResourceType.物資, dS);
                result.shortageAmmo = StateConsumptionFulfillmentRules.Consume(stock, ResourceType.弾薬, dA);
                result.shortageFuel = StateConsumptionFulfillmentRules.Consume(stock, ResourceType.燃料, dF);
            }
            else
            {
                result.shortageSupplies = dS;
                result.shortageAmmo = dA;
                result.shortageFuel = dF;
            }
            return result;
        }
    }
}
