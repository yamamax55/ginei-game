using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 宇宙葬・軌道埋葬のロジック（業種細分化・葬祭 #2025 の宇宙設定派生サブ業種・#2025・純ロジック・唯一の窓口）：宇宙葬の施行収入（SBUR-1）／
    /// 打ち上げコストの相乗り按分（SBUR-2＝1基のロケットを多数で分担）／追悼サブスク（SBUR-3＝軌道の遺骨を追跡し続ける継続課金）／利益（SBUR-4）。
    /// 葬祭（#2025）の宇宙版＝遺骨カプセルを軌道へ打ち上げる。打ち上げ費（建艦#884/輸送と同系統）を相乗りで割り、追悼サブスクで継続収入を得る。マクロ近似。test-first。
    /// </summary>
    public static class SpaceBurialRules
    {
        /// <summary>宇宙葬施行収入＝施行件数×1件あたり価格（高単価・低頻度の儀礼）。</summary>
        public static float BurialServiceRevenue(int burials, float pricePerBurial)
            => Mathf.Max(0, burials) * Mathf.Max(0f, pricePerBurial);

        /// <summary>カプセル1基あたり打ち上げコスト＝打ち上げ総額/相乗りカプセル数（1基のロケットを多数で分担して安くする）。数0以下は打ち上げ総額。</summary>
        public static float LaunchCostPerCapsule(float launchCost, int capsulesPerLaunch)
            => capsulesPerLaunch <= 0 ? Mathf.Max(0f, launchCost) : Mathf.Max(0f, launchCost) / capsulesPerLaunch;

        /// <summary>追悼サブスク収入＝加入者数×年会費（軌道上の遺骨位置を追跡し続ける継続課金）。</summary>
        public static float MemorialSubscription(int subscribers, float annualFee)
            => Mathf.Max(0, subscribers) * Mathf.Max(0f, annualFee);

        /// <summary>宇宙葬利益＝施行収入+追悼サブスク−打ち上げコスト−固定費（打ち上げ費が最大の原価）。</summary>
        public static float SpaceBurialProfit(float serviceRevenue, float memorialRevenue, float launchCost, float fixedCost)
            => serviceRevenue + Mathf.Max(0f, memorialRevenue) - Mathf.Max(0f, launchCost) - Mathf.Max(0f, fixedCost);
    }
}
