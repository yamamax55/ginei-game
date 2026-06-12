using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 音楽レーベルのロジック（業種細分化・情報通信 #2024 のコンテンツサブ業種・#2025・純ロジック・唯一の窓口）：ストリーミング印税（MUS-1）／
    /// 楽曲カタログの資産価値（MUS-2＝旧譜が稼ぎ続ける）／アーティスト前払金の未回収（MUS-3）／利益（MUS-4）。
    /// 1再生あたり極小だが回数で積み上がるストリーミング収入＋過去曲カタログという資産＝前払金（アドバンス）を印税で回収するヒット依存（ゲーム#2025/玩具#2025と同型）。マクロ近似。test-first。
    /// </summary>
    public static class MusicLabelRules
    {
        /// <summary>ストリーミング印税＝再生回数×1再生あたり単価（1回は極小・回数で積む）。</summary>
        public static float StreamingRoyalty(float streams, float perStreamRate)
            => Mathf.Max(0f, streams) * Mathf.Max(0f, perStreamRate);

        /// <summary>カタログ資産価値＝年間印税×倍率（過去曲が稼ぎ続ける＝旧譜は買収対象の資産）。</summary>
        public static float CatalogValue(float annualRoyalty, float multiple)
            => Mathf.Max(0f, annualRoyalty) * Mathf.Max(0f, multiple);

        /// <summary>アーティスト前払金の未回収額＝max(0, 前払金−回収済印税)（売れないと回収できず損失化）。</summary>
        public static float ArtistAdvanceRecoupment(float advance, float royaltiesEarned)
            => Mathf.Max(0f, advance - Mathf.Max(0f, royaltiesEarned));

        /// <summary>音楽レーベル利益＝印税収入−アーティスト取り分−制作費−固定費。</summary>
        public static float MusicLabelProfit(float royaltyRevenue, float artistShare, float productionCost, float fixedCost)
            => royaltyRevenue - Mathf.Max(0f, artistShare) - Mathf.Max(0f, productionCost) - Mathf.Max(0f, fixedCost);
    }
}
