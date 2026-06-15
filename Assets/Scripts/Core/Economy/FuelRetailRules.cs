using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 燃料小売（給油所）のロジック（業種細分化・小売 #2017 ／石油精製 #2024 の燃料小売サブ業種・#2025・純ロジック・唯一の窓口）：燃料の店頭マージン（FUEL-1）／
    /// 燃料売上利益（FUEL-2）／油外収益（FUEL-3＝洗車・併売店で稼ぐ）／利益（FUEL-4）。
    /// 燃料そのものは薄利（石油精製#2024から仕入れ）＝洗車・整備・併売店の油外収益で稼ぐ。EV化・宇宙燃料転換で構造変化を受ける川下。マクロ近似。test-first。
    /// </summary>
    public static class FuelRetailRules
    {
        /// <summary>燃料店頭マージン＝店頭価格−卸仕入値（薄利＝市況で逆ザヤにもなる）。</summary>
        public static float FuelGrossMargin(float retailPrice, float wholesaleCost)
            => retailPrice - wholesaleCost;

        /// <summary>燃料売上利益＝販売量×単位マージン。</summary>
        public static float FuelSalesProfit(float volume, float marginPerUnit)
            => Mathf.Max(0f, volume) * marginPerUnit;

        /// <summary>油外収益＝洗車件数×洗車単価+併売店売上（燃料の薄利を補う第二の柱）。</summary>
        public static float AncillaryRevenue(int carWashCount, float washPrice, float shopSales)
            => Mathf.Max(0, carWashCount) * Mathf.Max(0f, washPrice) + Mathf.Max(0f, shopSales);

        /// <summary>給油所利益＝燃料利益+油外利益−固定費。</summary>
        public static float StationProfit(float fuelProfit, float ancillaryProfit, float fixedCost)
            => fuelProfit + ancillaryProfit - Mathf.Max(0f, fixedCost);
    }
}
