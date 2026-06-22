using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術要塞（#76/#77・Battle シーンの固定拠点）の調整値。コアのシールド（稼働砲台依存の被ダメ軽減）まわり。
    /// 実効値パターン＝基準ダメージは非破壊で、受け手側の倍率として効かせる。
    /// ※回廊の戦略要塞（<see cref="FortressParams"/>＝守備隊/シールド/難攻不落）とは別系統（名前衝突回避）。
    /// </summary>
    public readonly struct FortressBatteryParams
    {
        /// <summary>全砲台稼働時のコア被ダメ倍率（小さいほど固い）。</summary>
        public readonly float shieldedCoreDamageFactor;

        /// <summary>稼働砲台比がこの値以下でシールド完全消失（コア被ダメ倍率→1.0）。</summary>
        public readonly float exposedTurretRatio;

        public FortressBatteryParams(float shieldedCoreDamageFactor, float exposedTurretRatio)
        {
            this.shieldedCoreDamageFactor = shieldedCoreDamageFactor;
            this.exposedTurretRatio = exposedTurretRatio;
        }

        /// <summary>既定＝全砲台稼働で0.25倍（固い）、稼働比0.3以下でシールド消失。</summary>
        public static FortressBatteryParams Default => new FortressBatteryParams(0.25f, 0.3f);
    }

    /// <summary>
    /// 戦術要塞（イゼルローン型・Battle シーン）の純ロジック（#76 本体＋砲台群／#77 主砲）。
    /// - 稼働砲台が揃うほどコアはシールドで固く、削れるほど被ダメが増える（段階攻略）。
    /// - 全砲台沈黙＝制圧（攻略のもう一つの達成条件）。
    /// - 主砲は最小射程あり＝懐に潜れば撃てない（攻城側のリズム）。
    /// MonoBehaviour（FortressUnit/FortressTurret/FortressMainCannon）はこの窓口を読むだけ。test-first。
    /// </summary>
    public static class FortressBatteryRules
    {
        /// <summary>稼働砲台比→コア被ダメ倍率（既定パラメータ）。</summary>
        public static float CoreDamageMultiplier(int activeTurrets, int totalTurrets)
            => CoreDamageMultiplier(activeTurrets, totalTurrets, FortressBatteryParams.Default);

        /// <summary>
        /// 稼働砲台比に応じたコア被ダメ倍率（シールド低下）。
        /// 全砲台稼働で <see cref="FortressBatteryParams.shieldedCoreDamageFactor"/> 倍（固い）、
        /// 稼働比が <see cref="FortressBatteryParams.exposedTurretRatio"/> 以下でシールド消失＝等倍。
        /// </summary>
        public static float CoreDamageMultiplier(int activeTurrets, int totalTurrets, FortressBatteryParams p)
        {
            if (totalTurrets <= 0) return 1f; // 砲台のない要塞は素のコア被ダメ（0除算回避）
            float ratio = Mathf.Clamp01((float)activeTurrets / totalTurrets);
            float denom = Mathf.Max(1e-4f, 1f - p.exposedTurretRatio);
            float shield = Mathf.Clamp01((ratio - p.exposedTurretRatio) / denom); // 0=露出/1=満
            return Mathf.Lerp(1f, p.shieldedCoreDamageFactor, shield);
        }

        /// <summary>全砲台が沈黙したか（＝制圧。攻略勝利のもう一つの達成条件）。</summary>
        public static bool IsSilenced(int activeTurrets) => activeTurrets <= 0;

        /// <summary>外周砲台の配置角（度・等間隔）。index 0 から 360/count 刻み。</summary>
        public static float TurretAngleDeg(int index, int count)
        {
            if (count <= 0) return 0f;
            return 360f / count * index;
        }

        /// <summary>
        /// 主砲の射界帯にいるか（#77）。最小射程未満＝懐＝撃てない、最大射程超＝射程外。
        /// minRange ≤ dist ≤ maxRange のときだけ true。
        /// </summary>
        public static bool InFiringBand(float dist, float minRange, float maxRange)
            => dist >= minRange && dist <= maxRange;
    }
}
