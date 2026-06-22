using UnityEngine;

namespace Ginei
{
    /// <summary>隠し特性の露見判定の調整値（PER-PROPS C10）。</summary>
    public readonly struct HiddenTraitParams
    {
        /// <summary>情報50・秘匿50 での基準露見確率。</summary>
        public readonly float baseReveal;
        /// <summary>観測者の情報能力の寄与。</summary>
        public readonly float intelWeight;
        /// <summary>秘匿度の抑制。</summary>
        public readonly float concealWeight;

        public HiddenTraitParams(float baseReveal, float intelWeight, float concealWeight)
        {
            this.baseReveal = baseReveal;
            this.intelWeight = intelWeight;
            this.concealWeight = concealWeight;
        }

        /// <summary>既定＝基準0.5／情報寄与0.6／秘匿抑制0.6。</summary>
        public static HiddenTraitParams Default => new HiddenTraitParams(0.5f, 0.6f, 0.6f);
    }

    /// <summary>
    /// 隠し特性（<see cref="HiddenTrait"/>）の露見判定（PER-PROPS C10）。観測者の情報能力が高く秘匿度が低いほど露見しやすい。
    /// 諜報（intelligence・現状ほぼ未使用）に役割を与え、戦略レイヤーに霧と裏切りの妙味を足す。
    /// roll は外から受ける（決定論）・test-first。
    /// </summary>
    public static class HiddenTraitRules
    {
        /// <summary>露見確率 0..1＝基準＋情報能力(0..100)×weight − 秘匿度(0..100)×weight。</summary>
        public static float RevealChance(int observerIntelligence, int concealment, HiddenTraitParams p)
        {
            float intel = Mathf.Clamp01(observerIntelligence / 100f) * p.intelWeight;
            float hide = Mathf.Clamp01(concealment / 100f) * p.concealWeight;
            return Mathf.Clamp01(p.baseReveal + intel - hide);
        }

        public static float RevealChance(int observerIntelligence, int concealment)
            => RevealChance(observerIntelligence, concealment, HiddenTraitParams.Default);

        /// <summary>roll(0..1) が露見確率未満なら露見（決定論）。</summary>
        public static bool IsRevealed(int observerIntelligence, int concealment, float roll, HiddenTraitParams p)
            => roll < RevealChance(observerIntelligence, concealment, p);

        public static bool IsRevealed(int observerIntelligence, int concealment, float roll)
            => IsRevealed(observerIntelligence, concealment, roll, HiddenTraitParams.Default);
    }
}
