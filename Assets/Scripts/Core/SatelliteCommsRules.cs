using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 衛星通信のロジック（業種細分化・情報通信 #2024 の衛星サブ業種・#2025・純ロジック・唯一の窓口）：中継器のリース収入（SAT-1）／
    /// コンステレーションのカバレッジ（SAT-2＝衛星数で覆域が広がる）／データ通信サービス収入（SAT-3）／利益（SAT-4）。
    /// 中継器（トランスポンダ）の貸し出しと、多数衛星のコンステレーションによる通信サービス＝打ち上げ（建艦#884/輸送と同系統）の償却が重い高固定費。星系間通信のインフラ。マクロ近似。test-first。
    /// </summary>
    public static class SatelliteCommsRules
    {
        /// <summary>中継器リース収入＝中継器数×月額リース料（放送#2025・通信事業者へ帯域を貸す）。</summary>
        public static float TransponderLeaseRevenue(int transponders, float monthlyLease)
            => Mathf.Max(0, transponders) * Mathf.Max(0f, monthlyLease);

        /// <summary>コンステレーション覆域＝衛星数×1基あたり覆域（上限cap・多数衛星で全域を覆う）。</summary>
        public static float ConstellationCoverage(int satellites, float coveragePerSat, float cap)
            => Mathf.Min(Mathf.Max(0f, cap), Mathf.Max(0, satellites) * Mathf.Max(0f, coveragePerSat));

        /// <summary>データ通信サービス収入＝加入者数×ARPU（コンステレーション経由のデータ通信）。</summary>
        public static float DataServiceRevenue(int subscribers, float arpu)
            => Mathf.Max(0, subscribers) * Mathf.Max(0f, arpu);

        /// <summary>衛星通信利益＝リース収入+サービス収入−打ち上げ償却−固定費（打ち上げ償却が最大の費目）。</summary>
        public static float SatelliteProfit(float leaseRevenue, float serviceRevenue, float launchAmortization, float fixedCost)
            => leaseRevenue + Mathf.Max(0f, serviceRevenue) - Mathf.Max(0f, launchAmortization) - Mathf.Max(0f, fixedCost);
    }
}
