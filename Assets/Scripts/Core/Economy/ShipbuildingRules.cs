using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 造船会社のロジック（業種細分化・輸送用機器 #2024 の重工サブ業種・#2025・純ロジック・唯一の窓口）：受注額＝載貨重量トン×トン単価（SHIP-1）／
    /// 為替感応（輸出はドル建て・SHIP-2）／受注残（長納期＝数年の手持ち工事・SHIP-3）／鋼材コストの採算（SHIP-4）。
    /// 鋼材は鉄鋼#2024、顧客は海運#2024・受注時と建造時で為替/鋼材価格が動き採算が読めない長納期の請負産業。マクロ近似。test-first。
    /// </summary>
    public static class ShipbuildingRules
    {
        /// <summary>受注額＝載貨重量トン（DWT）×トン単価（船は重量で値が決まる）。</summary>
        public static float ContractValue(float deadweightTons, float pricePerTon)
            => Mathf.Max(0f, deadweightTons) * Mathf.Max(0f, pricePerTon);

        /// <summary>為替調整後収益＝外貨建て受注額×為替レート（輸出はドル建て＝円安で収益増・円高で目減り）。</summary>
        public static float ForexAdjustedRevenue(float contractValueForeign, float exchangeRate)
            => Mathf.Max(0f, contractValueForeign) * Mathf.Max(0f, exchangeRate);

        /// <summary>受注残＝受注残+新規受注−引渡し（数年分の手持ち工事＝長納期ゆえ受注残が業況指標）。非負。</summary>
        public static float BacklogAfterOrders(float backlog, float newOrders, float delivered)
            => Mathf.Max(0f, Mathf.Max(0f, backlog) + Mathf.Max(0f, newOrders) - Mathf.Max(0f, delivered));

        /// <summary>造船利益＝収益−鋼材費−固定費（受注時より鋼材高/円高なら赤字工事）。</summary>
        public static float ShipbuildingProfit(float revenue, float steelCost, float fixedCost)
            => revenue - Mathf.Max(0f, steelCost) - Mathf.Max(0f, fixedCost);
    }
}
