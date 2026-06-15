using UnityEngine;

namespace Ginei
{
    /// <summary>陽動・欺瞞の調整係数。</summary>
    public readonly struct FeintParams
    {
        /// <summary>信憑性最大の陽動が敵戦力を引き付ける最大割合。</summary>
        public readonly float maxDrawRatio;
        /// <summary>敵の情報力が見破りに効く強さ。</summary>
        public readonly float intelPenetration;
        /// <summary>見破られた陽動部隊が孤立して受ける損害率。</summary>
        public readonly float exposedCasualtyRatio;

        public FeintParams(float maxDrawRatio, float intelPenetration, float exposedCasualtyRatio)
        {
            this.maxDrawRatio = Mathf.Clamp01(maxDrawRatio);
            this.intelPenetration = Mathf.Clamp01(intelPenetration);
            this.exposedCasualtyRatio = Mathf.Clamp01(exposedCasualtyRatio);
        }

        /// <summary>既定＝最大吸引50%・見破り係数0.8・露見損害30%。</summary>
        public static FeintParams Default => new FeintParams(0.5f, 0.8f, 0.3f);
    }

    /// <summary>
    /// 陽動・欺瞞の純ロジック（軍事的フェイント）。偽の主攻に見せた陽動が敵戦力を引き付け、本命の正面を
    /// 手薄にする。信憑性は陽動の規模に比例（小部隊の芝居は安いがバレやすい）し、敵の情報力が見破る。
    /// 見破られた陽動は無視されるどころか孤立した的になる＝欺瞞は張った分だけ自分も賭けている。
    /// 世論向けの欺瞞（<see cref="PropagandaRules"/>）・探知（<see cref="ReconRules"/>）とは別系統。
    /// 乱数は roll で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FeintRules
    {
        /// <summary>
        /// 陽動の信憑性（0..1）＝陽動規模比 feintScale(0..1＝主力に対する見かけの割合)×（1−敵情報力×係数）。
        /// 大きく見せるほど信じられ、敵の目が良いほど割り引かれる。
        /// </summary>
        public static float Credibility(float feintScale, float enemyIntel, FeintParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(feintScale) * (1f - Mathf.Clamp01(enemyIntel) * p.intelPenetration));
        }

        public static float Credibility(float feintScale, float enemyIntel)
            => Credibility(feintScale, enemyIntel, FeintParams.Default);

        /// <summary>見破り判定。roll∈[0,1) が信憑性以上なら見破られた＝true（信憑性が低いほど見破られやすい）。</summary>
        public static bool SeenThrough(float feintScale, float enemyIntel, float roll, FeintParams p)
        {
            return roll >= Credibility(feintScale, enemyIntel, p);
        }

        public static bool SeenThrough(float feintScale, float enemyIntel, float roll)
            => SeenThrough(feintScale, enemyIntel, roll, FeintParams.Default);

        /// <summary>
        /// 引き付けた敵戦力＝敵総戦力×最大吸引率×信憑性。信じられた陽動ほど多くの敵が釣れる。
        /// 見破られた陽動（credibility を 0 で渡す）は何も釣れない。
        /// </summary>
        public static float DrawnForce(float enemyTotalStrength, float credibility, FeintParams p)
        {
            return Mathf.Max(0f, enemyTotalStrength) * p.maxDrawRatio * Mathf.Clamp01(credibility);
        }

        public static float DrawnForce(float enemyTotalStrength, float credibility)
            => DrawnForce(enemyTotalStrength, credibility, FeintParams.Default);

        /// <summary>本命正面の敵の手薄さ（0..maxDrawRatio）＝釣れた割合がそのまま薄くなる。</summary>
        public static float MainFrontWeakening(float enemyTotalStrength, float credibility, FeintParams p)
        {
            if (enemyTotalStrength <= 0f) return 0f;
            return DrawnForce(enemyTotalStrength, credibility, p) / enemyTotalStrength;
        }

        public static float MainFrontWeakening(float enemyTotalStrength, float credibility)
            => MainFrontWeakening(enemyTotalStrength, credibility, FeintParams.Default);

        /// <summary>見破られた陽動部隊の孤立損害＝陽動部隊戦力×露見損害率（張った芝居の代金）。</summary>
        public static float ExposedLosses(float feintForceStrength, FeintParams p)
        {
            return Mathf.Max(0f, feintForceStrength) * p.exposedCasualtyRatio;
        }

        public static float ExposedLosses(float feintForceStrength)
            => ExposedLosses(feintForceStrength, FeintParams.Default);
    }
}
