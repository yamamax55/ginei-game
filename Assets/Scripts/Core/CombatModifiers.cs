using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦闘・移動の係数（修正子）公式を一元管理する純ロジック（#106）。提督能力→倍率、提督防御→被ダメージ軽減、
    /// 側背面倍率の各公式をここに集約し、<c>FleetMovement</c>（機動）/<c>ShipCombat</c>（攻撃）/<c>FleetStrength</c>（防御）に
    /// 散在・重複していた計算を排する。倍率の積み上げ合成は <see cref="ModifierStack"/> が担う（基準値非破壊＝実効値パターン）。
    /// 公式はここで EditMode テストにより固定し、各消費側は同一公式を呼ぶだけ＝挙動不変を保証する。test-first。
    /// </summary>
    public static class CombatModifiers
    {
        /// <summary>能力線形係数の基準値（この能力値で倍率1.0）。</summary>
        public const float AbilityBaseline = 50f;
        /// <summary>能力差→倍率のスケール（基準から±この値の差で±1.0倍ぶん動く）。</summary>
        public const float AbilityScale = 100f;
        /// <summary>提督防御→被ダメージ軽減の除数（防御この値で最大軽減）。</summary>
        public const float DefenseDivisor = 200f;
        /// <summary>被ダメージ軽減の上限（最大90%カット）。</summary>
        public const float MaxDefenseReduction = 0.9f;
        /// <summary>側背面ヒット判定の閾値（この倍率以上で側背面）。</summary>
        public const float FlankHitThreshold = 1.3f;

        /// <summary>
        /// 提督能力（攻撃/機動）→倍率。能力50で1.0倍、100で1.5倍、0で0.5倍（クランプ無し＝従来挙動を厳密維持）。
        /// </summary>
        public static float AbilityFactor(float effectiveStat)
            => 1f + (effectiveStat - AbilityBaseline) / AbilityScale;

        /// <summary>
        /// 提督防御→被ダメージ倍率（1.0=軽減なし）。防御200以上で0.1（90%カット）。基準ダメージは変えない（実効値パターン）。
        /// </summary>
        public static float DefenseDamageFactor(float effectiveDefense)
            => 1f - Mathf.Clamp(effectiveDefense / DefenseDivisor, 0f, MaxDefenseReduction);

        /// <summary>
        /// 側背面倍率。<paramref name="dot"/>＝被弾側正面(up)と攻撃方向の内積(-1..1)。真後ろ(dot=-1)で
        /// <paramref name="flankMin"/>、正面(dot=1)で1.0へ線形補間。<paramref name="isFlank"/> は結果が
        /// <see cref="FlankHitThreshold"/> 以上か（側背面ヒット演出の判定）。
        /// </summary>
        public static float FlankFactor(float dot, float flankMin, out bool isFlank)
        {
            float m = Mathf.Lerp(flankMin, 1f, (dot + 1f) / 2f);
            isFlank = m >= FlankHitThreshold;
            return m;
        }
    }
}
