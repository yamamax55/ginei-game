using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通信会社のロジック（東証33業種「情報・通信業」・#2024・純ロジック・唯一の窓口）：加入者×ARPU の安定収益（TEL-1）／解約
    /// （チャーン）と純増（TEL-2）／顧客生涯価値（TEL-3）／ネットワーク規模の優位（TEL-4）。サブスク型の recurring 収益＝設備投資
    /// （基地局）を回収しながら加入者を維持・拡大。家計（#1969）・消費（#1951）へ接続。マクロ近似。test-first。
    /// </summary>
    public static class TelecomRules
    {
        /// <summary>サービス収益＝加入者×ARPU（毎月入る安定収益）。</summary>
        public static float ServiceRevenue(float subscribers, float arpu)
            => Mathf.Max(0f, subscribers) * Mathf.Max(0f, arpu);

        /// <summary>純加入者＝既存＋新規獲得−解約（既存×解約率）（チャーンを上回る獲得で純増）。非負。</summary>
        public static float NetSubscribers(float baseSubscribers, float grossAdds, float churnRate)
            => Mathf.Max(0f, Mathf.Max(0f, baseSubscribers) + Mathf.Max(0f, grossAdds) - Mathf.Max(0f, baseSubscribers) * Mathf.Clamp01(churnRate));

        /// <summary>解約による逸失収益＝既存×解約率×ARPU（チャーンが利益を蝕む）。</summary>
        public static float ChurnLoss(float baseSubscribers, float churnRate, float arpu)
            => Mathf.Max(0f, baseSubscribers) * Mathf.Clamp01(churnRate) * Mathf.Max(0f, arpu);

        /// <summary>顧客生涯価値（LTV）＝ARPU/解約率（解約が低いほど1人の顧客が長く稼ぐ）。解約率0以下は超大。</summary>
        public static float CustomerLifetimeValue(float arpu, float churnRate)
            => churnRate <= 0f ? 999999f : Mathf.Max(0f, arpu) / churnRate;
    }
}
