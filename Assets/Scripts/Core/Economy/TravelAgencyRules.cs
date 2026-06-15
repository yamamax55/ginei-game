using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙旅行代理店のロジック（業種細分化・サービス #2024 の旅行仲介サブ業種・#2025・純ロジック・唯一の窓口）：送客手数料（TRVL-1）／
    /// ダイナミックパッケージのマージン（TRVL-2）／パッケージ収益（TRVL-3）／利益（TRVL-4）。
    /// 宇宙鉄道（#2025）・空運（#2024）・ホテル（#2025）を仕入れて旅程に組み売る仲介＝在庫を持たず送客手数料＋自社パッケージのマージンで稼ぐ。マクロ近似。test-first。
    /// </summary>
    public static class TravelAgencyRules
    {
        /// <summary>送客手数料＝取扱額×手数料率（他社の宇宙鉄道#2025/宿#2025を売り、送客した分の口銭を得る）。</summary>
        public static float BookingCommission(float bookingValue, float commissionRate)
            => Mathf.Max(0f, bookingValue) * Mathf.Clamp01(commissionRate);

        /// <summary>ダイナミックパッケージのマージン＝(パッケージ価格−素材原価)/価格（自前で交通+宿を組み高付加価値で売る）。価格0以下は0。</summary>
        public static float DynamicPackageMargin(float packagePrice, float componentCost)
            => packagePrice <= 0f ? 0f : (packagePrice - Mathf.Max(0f, componentCost)) / packagePrice;

        /// <summary>パッケージ収益＝販売数×パッケージ価格。</summary>
        public static float PackageRevenue(int packagesSold, float packagePrice)
            => Mathf.Max(0, packagesSold) * Mathf.Max(0f, packagePrice);

        /// <summary>旅行代理店利益＝送客手数料+パッケージ粗利−固定費（在庫を持たない身軽な仲介）。</summary>
        public static float TravelAgencyProfit(float commissionRevenue, float packageGrossProfit, float fixedCost)
            => commissionRevenue + packageGrossProfit - Mathf.Max(0f, fixedCost);
    }
}
