using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// SaaS（サブスク型ソフト）のロジック（業種細分化・情報通信 #2024 のクラウドサブ業種・#2025・純ロジック・唯一の窓口）：MRR＝月次経常収益（SAAS-1）／
    /// ARR＝年換算（SAAS-2）／解約による失収（チャーン・SAAS-3）／NRR＝既存顧客の純拡大（SAAS-4）。
    /// 売り切りでなく積み上がる経常収益＝チャーンを抑えNRRが100%超なら何もせず成長する。情報通信#2024の事業モデル特化。マクロ近似。test-first。
    /// </summary>
    public static class SaasRules
    {
        /// <summary>MRR（月次経常収益）＝契約者数×1社あたり月額（ARPA）。</summary>
        public static float MonthlyRecurringRevenue(int subscribers, float arpa)
            => Mathf.Max(0, subscribers) * Mathf.Max(0f, arpa);

        /// <summary>ARR（年次経常収益）＝MRR×12（積み上がる収益の年換算）。</summary>
        public static float AnnualRecurringRevenue(float mrr)
            => Mathf.Max(0f, mrr) * 12f;

        /// <summary>解約失収＝MRR×解約率（チャーン＝穴の開いたバケツ）。</summary>
        public static float ChurnedRevenue(float mrr, float churnRate)
            => Mathf.Max(0f, mrr) * Mathf.Clamp01(churnRate);

        /// <summary>NRR（売上維持率）＝(期初+アップセル−解約)/期初（100%超なら新規ゼロでも成長＝SaaSの理想）。期初0以下は0。</summary>
        public static float NetRevenueRetention(float startMrr, float expansion, float churn)
            => startMrr <= 0f ? 0f : (startMrr + Mathf.Max(0f, expansion) - Mathf.Max(0f, churn)) / startMrr;
    }
}
