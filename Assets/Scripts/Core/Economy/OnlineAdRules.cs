using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ネット広告のロジック（業種細分化・情報通信 #2024 のデジタル広告サブ業種・#2025・純ロジック・唯一の窓口）：インプレッション＝到達×頻度（AD-1）／
    /// クリック＝表示×CTR（AD-2）／広告収益＝クリック×CPC（AD-3）／運用型オークション＝セカンドプライス（AD-4）。
    /// マス広告（化粧品#2025等の広告宣伝費）と違い効果が数値で測れる運用型＝枠を秒単位オークションで売る。情報通信#2024の特化。マクロ近似。test-first。
    /// </summary>
    public static class OnlineAdRules
    {
        /// <summary>インプレッション（表示回数）＝到達ユーザー×表示頻度。</summary>
        public static float Impressions(float reach, float frequency)
            => Mathf.Max(0f, reach) * Mathf.Max(0f, frequency);

        /// <summary>クリック数＝表示回数×CTR（クリック率）。</summary>
        public static float Clicks(float impressions, float ctr)
            => Mathf.Max(0f, impressions) * Mathf.Clamp01(ctr);

        /// <summary>広告収益＝クリック数×CPC（クリック単価＝成果課金）。</summary>
        public static float AdRevenue(float clicks, float cpc)
            => Mathf.Max(0f, clicks) * Mathf.Max(0f, cpc);

        /// <summary>オークション約定価格＝2位入札+刻み（セカンドプライス＝2位の額＋最小刻みで落札＝正直入札が最適）。</summary>
        public static float AuctionClearingPrice(float secondHighestBid, float increment)
            => Mathf.Max(0f, secondHighestBid) + Mathf.Max(0f, increment);
    }
}
