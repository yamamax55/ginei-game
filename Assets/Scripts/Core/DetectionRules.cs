using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 索敵と不意打ち（fog of war・#2180）の純ロジック。未使用だった `intelligence`（情報）に役割を与える。
    /// 情報が高いほど索敵範囲が広く、敵の接近を早く捉える＝不意打ちを許さない。索敵範囲外から接敵する
    /// 「発見されていない」攻撃側は不意打ちボーナス（先制の利）を得る。倍率を返すだけ（実効値パターン）・test-first。
    /// </summary>
    public static class DetectionRules
    {
        /// <summary>索敵範囲の基準（情報50で等倍）。武器射程より広めに採り、通常は接敵前に発見する。</summary>
        public const float BaseDetectionRange = 18f;
        /// <summary>発見されていない攻撃側の不意打ち倍率（先制の利）。</summary>
        public const float AmbushDamageFactor = 1.3f;

        /// <summary>情報能力(0..100)から索敵範囲。情報50で基準、100で1.5倍、0で0.5倍。</summary>
        public static float DetectionRange(float intelligence, float baseRange = BaseDetectionRange)
        {
            float t = Mathf.Clamp(intelligence, 0f, 100f) / 100f; // 0..1
            return Mathf.Max(0f, baseRange * (0.5f + t));         // 0.5×..1.5×
        }

        /// <summary>その距離が索敵範囲内か（発見できているか）。</summary>
        public static bool IsDetected(float distance, float detectionRange)
            => distance <= detectionRange;

        /// <summary>発見されているかどうかから攻撃側の与ダメ倍率（未発見＝不意打ち、発見済み＝等倍）。</summary>
        public static float AttackFactor(bool concealed)
            => concealed ? AmbushDamageFactor : 1f;
    }
}
