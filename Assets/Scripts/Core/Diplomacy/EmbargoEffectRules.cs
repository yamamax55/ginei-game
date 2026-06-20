using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通商禁止（エンバーゴ・経済制裁）の経済的打撃を解く純ロジック。
    /// 標的への打撃は「制裁陣営が標的の貿易のどれだけを握っているか（tradeShare）」に比例し、
    /// 標的の自給度（self-sufficiency）が高いほど目減りする＝外との取引に依存しない国には効きにくい。
    /// 同時に制裁する側も貿易を失う反作用（Blowback）を負う＝制裁は両刃の剣。
    /// DiplomacyRules（外交状態の遷移）/SanctionsRules とは別系統＝こちらは「制裁の経済的な噛みつき」だけを解く。
    /// 乱数なし・決定論・後方互換。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EmbargoEffectRules
    {
        /// <summary>禁輸効果の調整係数。</summary>
        public readonly struct Params
        {
            /// <summary>標的経済への最大打撃（0..1。貿易を完全に握り自給ゼロのときの上限）。</summary>
            public readonly float maxBite;
            /// <summary>打撃のうち制裁する側へ跳ね返る反作用の割合（0..1）。</summary>
            public readonly float blowbackRatio;

            public Params(float maxBite, float blowbackRatio)
            {
                this.maxBite = Mathf.Max(0f, maxBite);
                this.blowbackRatio = Mathf.Clamp01(blowbackRatio);
            }

            /// <summary>既定＝最大打撃0.6・反作用割合0.3。</summary>
            public static Params Default => new Params(0.6f, 0.3f);
        }

        /// <summary>
        /// 標的経済への打撃（0..maxBite）＝maxBite×貿易支配率(0..1)×(1−自給度(0..1))。
        /// 貿易を多く握るほど大きく、自給度が高いほど小さい。自給度=1なら打撃ゼロ。
        /// </summary>
        public static float EconomicBite(float tradeShare01, float targetSelfSufficiency01, Params p)
        {
            float bite = p.maxBite * Mathf.Clamp01(tradeShare01) * (1f - Mathf.Clamp01(targetSelfSufficiency01));
            return Mathf.Clamp(bite, 0f, p.maxBite);
        }

        public static float EconomicBite(float tradeShare01, float targetSelfSufficiency01)
            => EconomicBite(tradeShare01, targetSelfSufficiency01, Params.Default);

        /// <summary>
        /// 反作用コスト＝打撃×反作用割合。制裁する側も失った貿易ぶんの痛みを負う。
        /// </summary>
        public static float Blowback(float bite, Params p)
        {
            return Mathf.Max(0f, bite) * p.blowbackRatio;
        }

        public static float Blowback(float bite)
            => Blowback(bite, Params.Default);

        /// <summary>
        /// 正味の圧力＝標的への打撃−自分への反作用。負なら制裁する側の損が勝る（割に合わない）。
        /// </summary>
        public static float NetPressure(float tradeShare01, float targetSelfSufficiency01, Params p)
        {
            float bite = EconomicBite(tradeShare01, targetSelfSufficiency01, p);
            return bite - Blowback(bite, p);
        }

        public static float NetPressure(float tradeShare01, float targetSelfSufficiency01)
            => NetPressure(tradeShare01, targetSelfSufficiency01, Params.Default);
    }
}
