using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 倉庫・運輸関連会社のロジック（東証33業種「倉庫・運輸関連業」・#2024・純ロジック・唯一の窓口）：保管料収益（WHS-1）／
    /// 倉庫稼働率（WHS-2）／空きスペースの機会損失（WHS-3）／利益（WHS-4）。物流（陸運 #2024/海運 #2024）・各業種の在庫を保管。
    /// マクロ近似。test-first。
    /// </summary>
    public static class WarehouseRules
    {
        /// <summary>保管料収益＝保管量×保管単価（預かったぶんだけ稼ぐ）。</summary>
        public static float StorageRevenue(float storedVolume, float ratePerVolume)
            => Mathf.Max(0f, storedVolume) * Mathf.Max(0f, ratePerVolume);

        /// <summary>倉庫稼働率＝保管量/保管能力（空きを減らすほど効率的）。能力0以下は0。</summary>
        public static float WarehouseUtilization(float stored, float capacity)
            => capacity <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, stored) / capacity);

        /// <summary>空きスペースの機会損失＝(能力−保管量)×保管単価（埋まらなかったぶんの逸失収益）。非負。</summary>
        public static float VacancyLoss(float capacity, float stored, float ratePerVolume)
            => Mathf.Max(0f, Mathf.Max(0f, capacity) - Mathf.Max(0f, stored)) * Mathf.Max(0f, ratePerVolume);

        /// <summary>倉庫利益＝保管料収益−運営費。</summary>
        public static float WarehouseProfit(float revenue, float operatingCost)
            => revenue - Mathf.Max(0f, operatingCost);
    }
}
