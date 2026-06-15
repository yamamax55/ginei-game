using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 倍率（修正子）を積で合成する軽量スタック（#106）。基準1.0から各係数を順に掛けるだけの値型＝
    /// ヒープ確保なし（毎フレーム多数の艦が呼ぶ移動/戦闘の合成に使える）。各所に散在していた
    /// <c>factor *= ...</c> を「基準1.0 → 係数を積む → 結果（必要なら下限クランプ）」という一定の形に揃える。
    /// 係数公式そのものは <see cref="CombatModifiers"/> が持つ（合成と公式を分離）。test-first。
    /// </summary>
    public struct ModifierStack
    {
        /// <summary>現在の合成値（基準1.0からの積）。</summary>
        public float Value;

        /// <summary>基準1.0で開始したスタックを返す。</summary>
        public static ModifierStack Start() => new ModifierStack { Value = 1f };

        /// <summary>係数を1つ掛ける（基準値非破壊＝合成値のみ更新）。</summary>
        public void Mul(float factor)
        {
            Value *= factor;
        }

        /// <summary>下限でクランプした合成値（完全停止防止などに使う）。</summary>
        public float ClampMin(float min) => Mathf.Max(min, Value);
    }
}
