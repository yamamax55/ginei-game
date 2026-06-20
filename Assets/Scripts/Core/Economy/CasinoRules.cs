using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// カジノ・ゲーミングのロジック（業種細分化・サービス #2024 の遊興サブ業種・#2025・純ロジック・唯一の窓口）：ゲーミング収入（CASI-1＝賭け金×控除率）／
    /// VIPへの無償提供コスト（CASI-2＝コンプ）／VIP信用の貸倒れ（CASI-3）／利益（CASI-4）。
    /// 控除率（ハウスエッジ）で賭け金総額から一定割合を確実に得る＝大数の法則（保険#1982と同型）。VIPへのコンプ・信用供与の焦げ付きがコスト。マクロ近似。test-first。
    /// </summary>
    public static class CasinoRules
    {
        /// <summary>ゲーミング収入＝賭け金総額（ハンドル）×控除率（ハウスエッジ＝確実に得る取り分）。</summary>
        public static float GamingRevenue(float handle, float holdRate)
            => Mathf.Max(0f, handle) * Mathf.Clamp01(holdRate);

        /// <summary>VIPコンプコスト＝VIPの理論上勝ち金×コンプ率（宿泊・飲食を無償提供して呼ぶ販促）。</summary>
        public static float VipComplimentaryCost(float vipTheoretical, float compRate)
            => Mathf.Max(0f, vipTheoretical) * Mathf.Clamp01(compRate);

        /// <summary>VIP信用の貸倒れ＝供与した信用×焦げ付き率（高額VIPへの貸しが回収不能になるリスク）。</summary>
        public static float CreditChargeOff(float creditExtended, float defaultRate)
            => Mathf.Max(0f, creditExtended) * Mathf.Clamp01(defaultRate);

        /// <summary>カジノ利益＝ゲーミング収入+非ゲーミング収入−コンプコスト−固定費。</summary>
        public static float CasinoProfit(float gamingRevenue, float nonGamingRevenue, float compCost, float fixedCost)
            => gamingRevenue + Mathf.Max(0f, nonGamingRevenue) - Mathf.Max(0f, compCost) - Mathf.Max(0f, fixedCost);
    }
}
