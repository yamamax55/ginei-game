using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// コンビニ（フランチャイズ本部）のロジック（業種細分化・小売 #2017 のFCチェーンサブ業種・#2025・純ロジック・唯一の窓口）：FCロイヤリティ＝
    /// 加盟店粗利×ロイヤリティ率（CVS-1）／本部収益＝店舗数×店当たりロイヤリティ（CVS-2）／廃棄ロス（CVS-3）／ドミナント＝地域集中出店のシェア（CVS-4）。
    /// 小売（#2017）の本部・加盟店モデル＝本部は店舗数を増やすほど稼ぎ、ドミナント出店で物流効率と地域独占を握る。マクロ近似。test-first。
    /// </summary>
    public static class ConvenienceStoreRules
    {
        /// <summary>FCロイヤリティ＝加盟店粗利×ロイヤリティ率（本部の取り分＝加盟店が稼ぐほど本部も潤う）。</summary>
        public static float FranchiseRoyalty(float storeGrossProfit, float royaltyRate)
            => Mathf.Max(0f, storeGrossProfit) * Mathf.Clamp01(royaltyRate);

        /// <summary>本部収益＝店舗数×1店当たりロイヤリティ（規模＝店舗網がそのまま収益）。</summary>
        public static float HeadquartersRevenue(int storeCount, float royaltyPerStore)
            => Mathf.Max(0, storeCount) * Mathf.Max(0f, royaltyPerStore);

        /// <summary>廃棄ロス＝売れ残り点数×原価（鮮度商品の宿命＝発注精度が利益を左右）。</summary>
        public static float WasteLoss(int unsoldItems, float unitCost)
            => Mathf.Max(0, unsoldItems) * Mathf.Max(0f, unitCost);

        /// <summary>ドミナントシェア＝地域内自社店舗/地域内総店舗（集中出店で物流効率・地域独占）。総数0以下は0。</summary>
        public static float DominantShare(int ownStoresInArea, int totalStoresInArea)
            => totalStoresInArea <= 0 ? 0f : Mathf.Clamp01((float)Mathf.Max(0, ownStoresInArea) / totalStoresInArea);
    }
}
