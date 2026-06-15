using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 決済・キャッシュレス事業のロジック（業種細分化・信販 #1996 ／その他金融の決済サブ業種・#2025・純ロジック・唯一の窓口）：加盟店手数料（PAY-1）／
    /// ネットワーク手数料＝決済1件ごとの定額（PAY-2）／滞留資金の運用益（フロート・PAY-3）／利益（PAY-4）。
    /// 決済額に比例する加盟店手数料＋件数比例のネットワーク料＋チャージ残高のフロート運用で稼ぐ薄利多売＝取扱高（消費C#1951）が源。マクロ近似。test-first。
    /// </summary>
    public static class PaymentRules
    {
        /// <summary>加盟店手数料収入＝決済取扱高×手数料率（店舗が負担する決済額比例の手数料）。</summary>
        public static float MerchantFeeRevenue(float transactionVolume, float feeRate)
            => Mathf.Max(0f, transactionVolume) * Mathf.Clamp01(feeRate);

        /// <summary>ネットワーク手数料収入＝決済件数×1件あたり定額（取扱額に依らない件数課金）。</summary>
        public static float NetworkFeeRevenue(int transactionCount, float feePerTransaction)
            => Mathf.Max(0, transactionCount) * Mathf.Max(0f, feePerTransaction);

        /// <summary>フロート運用益＝チャージ残高（滞留資金）×運用利回り（前払い残高を運用する金融収益）。</summary>
        public static float FloatIncome(float averageBalance, float interestRate)
            => Mathf.Max(0f, averageBalance) * Mathf.Max(0f, interestRate);

        /// <summary>決済事業利益＝手数料収入−処理コスト−固定費（薄利ゆえ取扱高の規模が命）。</summary>
        public static float PaymentProfit(float feeRevenue, float processingCost, float fixedCost)
            => feeRevenue - Mathf.Max(0f, processingCost) - Mathf.Max(0f, fixedCost);
    }
}
