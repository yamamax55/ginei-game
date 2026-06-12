using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建機メーカー（建設機械）のロジック（#2022・純ロジック・唯一の窓口）。建設・鉱山・インフラ向け資本財の business：資本財
    /// 需要と加速度原理＝景気敏感（CON-1）／新車とアフターサービス＝稼働台数の安定収益（CON-2）／レンタル・中古・残価（CON-3）／
    /// グローバル需要・地域分散（CON-4）／景気循環と利益＝アフターが下支え（CON-5）。汎用製造は <see cref="ManufacturerRules"/>(#2016)、
    /// 顧客は鉱山(#2018)/不動産(#2019)/インフラ(#2021)、残価は <see cref="LeasingRules"/>(#1989) と同型。マクロ近似。test-first。
    /// </summary>
    public static class ConstructionMachineryRules
    {
        /// <summary>加速度原理の既定増幅率（建設投資の伸びに対する建機需要の感応度）。</summary>
        public const float DefaultAccelerationFactor = 3f;

        // ===== CON-1 資本財需要と加速度原理 =====

        /// <summary>
        /// 加速度原理の需要＝基準需要×(1＋建設活動の伸び率×増幅率)。投資財ゆえ活動の<b>変化</b>に増幅して反応＝好況で急増・
        /// 不況で急減（景気に超敏感）。非負。
        /// </summary>
        public static float AcceleratedDemand(float baseDemand, float activityGrowthRate, float accelerationFactor)
            => Mathf.Max(0f, Mathf.Max(0f, baseDemand) * (1f + activityGrowthRate * Mathf.Max(0f, accelerationFactor)));

        // ===== CON-2 新車とアフターサービス =====

        /// <summary>新車販売収益＝販売台数×単価。</summary>
        public static float NewSalesRevenue(float units, float unitPrice)
            => Mathf.Max(0f, units) * Mathf.Max(0f, unitPrice);

        /// <summary>アフターサービス収益＝稼働台数×1台あたり部品/整備単価（新車販売が落ちても稼働機があれば安定）。</summary>
        public static float AfterSalesRevenue(float installedBase, float ratePerUnit)
            => Mathf.Max(0f, installedBase) * Mathf.Max(0f, ratePerUnit);

        /// <summary>アフターサービス比率＝アフター収益/総収益（高いほど不況に強い安定収益基盤）。総収益0以下は0。</summary>
        public static float AfterSalesShare(float afterSales, float totalRevenue)
            => totalRevenue <= 0f ? 0f : Mathf.Max(0f, afterSales) / totalRevenue;

        // ===== CON-3 レンタル・中古・残価 =====

        /// <summary>レンタル収益＝レンタル台数×1台あたりレンタル料。</summary>
        public static float RentalIncome(float rentedUnits, float ratePerUnit)
            => Mathf.Max(0f, rentedUnits) * Mathf.Max(0f, ratePerUnit);

        /// <summary>残価＝新車価格×max(下限率, 1−経年×年あたり減価率)（中古の評価額。建機は耐久財で残価が高い）。</summary>
        public static float ResidualValue(float newPrice, float ageYears, float depreciationPerYear, float floorRate)
            => Mathf.Max(0f, newPrice) * Mathf.Max(Mathf.Clamp01(floorRate), 1f - Mathf.Max(0f, ageYears) * Mathf.Max(0f, depreciationPerYear));

        /// <summary>中古売却額＝中古台数×新車価値×残価率。</summary>
        public static float UsedSaleProceeds(float unitValue, float residualRate, float usedUnits)
            => Mathf.Max(0f, usedUnits) * Mathf.Max(0f, unitValue) * Mathf.Clamp01(residualRate);

        // ===== CON-4 グローバル需要 =====

        /// <summary>世界需要＝各地域需要の合計。</summary>
        public static float GlobalDemand(IReadOnlyList<float> regionalDemands)
        {
            if (regionalDemands == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < regionalDemands.Count; i++) sum += Mathf.Max(0f, regionalDemands[i]);
            return sum;
        }

        /// <summary>地域分散度（0..1）＝1−ハーフィンダル指数（地域別需要シェアの二乗和）。多くの市場に分散するほど高い＝特定地域の不況に強い。</summary>
        public static float GeographicDiversification(IReadOnlyList<float> regionalDemands)
        {
            float total = GlobalDemand(regionalDemands);
            if (total <= 0f) return 0f;
            float hhi = 0f;
            for (int i = 0; i < regionalDemands.Count; i++)
            {
                float share = Mathf.Max(0f, regionalDemands[i]) / total;
                hhi += share * share;
            }
            return Mathf.Clamp01(1f - hhi);
        }

        // ===== CON-5 景気循環と利益 =====

        /// <summary>
        /// 景気循環の利益＝新車利益×景気係数＋アフター利益（新車は景気で大きく揺れ、アフターは稼働機に紐づき安定＝下支え）。
        /// 景気係数1.0で平常、好況で>1、不況で<1。
        /// </summary>
        public static float CyclicalProfit(float newSalesProfit, float afterSalesProfit, float cycleFactor)
            => newSalesProfit * Mathf.Max(0f, cycleFactor) + Mathf.Max(0f, afterSalesProfit);

        /// <summary>不況耐性＝アフター利益/総利益（アフターが大きいほど不況でも利益が崩れにくい）。総利益0以下は0。</summary>
        public static float DownturnResilience(float afterSalesProfit, float totalProfit)
            => totalProfit <= 0f ? 0f : Mathf.Max(0f, afterSalesProfit) / totalProfit;
    }
}
