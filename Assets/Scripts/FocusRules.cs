using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 三密＝身・口・意の同期による超越の純ロジック（空海 #872・KUKAI-2／謙信の無我 #810 の精密化）。
    /// 印（身）・真言（口）・観想（意）の3チャンネルを揃えると、自分がそのまま仏になる＝一時的に
    /// 最大出力。一つでも欠ければ崩れる（積＝最弱チャンネルが効く）。実効値パターンの精神版（基準非破壊）。
    /// 戦闘の一時バフ（極限集中）にも転用可。純ロジック・test-first。
    /// </summary>
    public static class FocusRules
    {
        /// <summary>三密の同期度 0..1＝身×口×意（一つでも低いと全体が崩れる＝全身全霊の同期）。</summary>
        public static float Sync(float body, float speech, float mind)
            => Mathf.Clamp01(body) * Mathf.Clamp01(speech) * Mathf.Clamp01(mind);

        /// <summary>
        /// 三密同期による実効出力倍率＝baseMult + bonus×同期度。3つ揃うほど(同期度→1)出力が伸びる。
        /// 基準値は変えず、ローカルに倍率を計算する（実効値パターン）。
        /// </summary>
        public static float OutputMultiplier(float body, float speech, float mind, float baseMult, float bonus)
            => baseMult + bonus * Sync(body, speech, mind);

        /// <summary>既定（基準1.0・最大同期で+0.5＝1.5倍）。</summary>
        public static float OutputMultiplier(float body, float speech, float mind)
            => OutputMultiplier(body, speech, mind, 1f, 0.5f);
    }
}
