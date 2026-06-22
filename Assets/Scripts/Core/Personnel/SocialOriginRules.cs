using UnityEngine;

namespace Ginei
{
    /// <summary>出自が登用・忠誠に与える係数の調整値（PER-PROPS A4）。</summary>
    public readonly struct SocialOriginParams
    {
        /// <summary>出自と勢力信条の階級摩擦の最大（0..1）。</summary>
        public readonly float classFriction;
        /// <summary>門閥勢力での貴族出の引き立て（0..1）。</summary>
        public readonly float pedigreeFavor;
        /// <summary>能力主義勢力での平民・植民星出の機会（0..1）。</summary>
        public readonly float meritFavorMax;

        public SocialOriginParams(float classFriction, float pedigreeFavor, float meritFavorMax)
        {
            this.classFriction = classFriction;
            this.pedigreeFavor = pedigreeFavor;
            this.meritFavorMax = meritFavorMax;
        }

        /// <summary>既定＝摩擦0.3／門閥引立0.2／能力機会0.25。</summary>
        public static SocialOriginParams Default => new SocialOriginParams(0.3f, 0.2f, 0.25f);
    }

    /// <summary>
    /// 社会階層・出自（<see cref="SocialOrigin"/>）が登用・忠誠に与える係数（PER-PROPS A4）。門閥勢力では貴族出が引き立てられ、
    /// 能力主義勢力では平民・植民星出に機会が開く。階級と勢力信条の不一致は忠誠摩擦を生む。
    /// 数値の忠誠力学は <see cref="AllegianceDriftRules"/> へ橋渡し（係数を返すだけ＝再実装しない）。決定論・test-first。
    /// </summary>
    public static class SocialOriginRules
    {
        /// <summary>貴族の出か（門閥貴族・下級貴族）。</summary>
        public static bool IsAristocratic(SocialOrigin o) => o == SocialOrigin.門閥貴族 || o == SocialOrigin.下級貴族;

        /// <summary>庶民の出か（平民・植民星）。</summary>
        public static bool IsCommoner(SocialOrigin o) => o == SocialOrigin.平民 || o == SocialOrigin.植民星;

        /// <summary>登用での引き立て係数（-1..+1・正＝有利）。勢力信条 factionCreed と出自の相性。</summary>
        public static float RecruitmentFavor(SocialOrigin origin, Creed factionCreed)
            => RecruitmentFavor(origin, factionCreed, SocialOriginParams.Default);

        /// <summary>登用での引き立て係数（-1..+1）。門閥主義＝貴族優遇/庶民冷遇、能力主義＝庶民機会/貴族やや不利。</summary>
        public static float RecruitmentFavor(SocialOrigin origin, Creed factionCreed, SocialOriginParams p)
        {
            if (factionCreed == Creed.門閥主義)
            {
                if (origin == SocialOrigin.門閥貴族) return Mathf.Clamp(p.pedigreeFavor, -1f, 1f);
                if (IsCommoner(origin)) return -Mathf.Clamp(p.classFriction, 0f, 1f);
            }
            else if (factionCreed == Creed.能力主義)
            {
                if (IsCommoner(origin)) return Mathf.Clamp(p.meritFavorMax, -1f, 1f);
                if (origin == SocialOrigin.門閥貴族) return -Mathf.Clamp(p.classFriction, 0f, 1f) * 0.5f;
            }
            return 0f;
        }

        /// <summary>出自と勢力信条の階級摩擦＝忠誠を引く 0..1（庶民出×門閥主義／門閥出×能力主義 で最大）。</summary>
        public static float LoyaltyFriction(SocialOrigin origin, Creed factionCreed)
            => LoyaltyFriction(origin, factionCreed, SocialOriginParams.Default);

        /// <summary>階級摩擦 0..1。該当ペアで classFriction、それ以外は0。</summary>
        public static float LoyaltyFriction(SocialOrigin origin, Creed factionCreed, SocialOriginParams p)
        {
            float f = Mathf.Clamp01(p.classFriction);
            if (factionCreed == Creed.門閥主義 && IsCommoner(origin)) return f;
            if (factionCreed == Creed.能力主義 && origin == SocialOrigin.門閥貴族) return f;
            return 0f;
        }
    }
}
