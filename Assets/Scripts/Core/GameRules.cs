using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ゲーム会社（デジタルコンテンツ）のロジック（業種細分化・情報通信 #2024 のコンテンツサブ業種・#2025・純ロジック・唯一の窓口）：課金収益＝
    /// アクティブ×課金率×ARPPU（GAME-1）／ヒット依存の博打＝期待値（GAME-2）／ライブサービス減衰（GAME-3）／LTV（GAME-4）。
    /// 開発費を先に投じヒットすれば数十倍＝VC（#2025）型のヒット依存・運営型は課金が時間で減衰。マクロ近似。test-first。
    /// </summary>
    public static class GameRules
    {
        /// <summary>課金収益＝アクティブユーザー×課金率×ARPPU（課金ユーザー1人当たり売上）。基本無料＋一部課金のモデル。</summary>
        public static float InAppRevenue(float activeUsers, float payingUserRate, float arppu)
            => Mathf.Max(0f, activeUsers) * Mathf.Clamp01(payingUserRate) * Mathf.Max(0f, arppu);

        /// <summary>ヒット博打の期待値＝ヒット確率×ヒット時収益−開発費（外れれば開発費が丸損）。</summary>
        public static float ExpectedGameValue(float hitProbability, float hitRevenue, float devCost)
            => Mathf.Clamp01(hitProbability) * Mathf.Max(0f, hitRevenue) - Mathf.Max(0f, devCost);

        /// <summary>ライブサービス収益＝ローンチ収益×(1−減衰率)^経過月、ただし下限割合で底打ち（運営で寿命を延ばす）。</summary>
        public static float LiveServiceRevenue(float launchRevenue, int monthsSinceLaunch, float decayRate, float floorFraction)
        {
            float launch = Mathf.Max(0f, launchRevenue);
            float decayed = launch * Mathf.Pow(1f - Mathf.Clamp01(decayRate), Mathf.Max(0, monthsSinceLaunch));
            float floor = launch * Mathf.Clamp01(floorFraction);
            return Mathf.Max(decayed, floor);
        }

        /// <summary>顧客生涯価値（LTV）＝ARPPU×継続月数（獲得コストと比較する指標）。</summary>
        public static float LifetimeValue(float arppu, float retentionMonths)
            => Mathf.Max(0f, arppu) * Mathf.Max(0f, retentionMonths);
    }
}
