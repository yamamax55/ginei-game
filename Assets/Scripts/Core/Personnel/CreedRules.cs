using UnityEngine;

namespace Ginei
{
    /// <summary>信条の緊張係数（PER-PROPS A2）。</summary>
    public readonly struct CreedParams
    {
        /// <summary>真っ向対立（共和↔帝政・門閥↔能力）の緊張。</summary>
        public readonly float opposed;
        /// <summary>異なるが非対立の緊張。</summary>
        public readonly float divergent;
        /// <summary>無関心が絡む側の緊張（低い）。</summary>
        public readonly float apathetic;
        /// <summary>中道が絡む側の緊張（低め）。</summary>
        public readonly float moderate;

        public CreedParams(float opposed, float divergent, float apathetic, float moderate)
        {
            this.opposed = opposed;
            this.divergent = divergent;
            this.apathetic = apathetic;
            this.moderate = moderate;
        }

        /// <summary>既定＝対立1.0／相違0.4／無関心0.1／中道0.2。</summary>
        public static CreedParams Default => new CreedParams(1f, 0.4f, 0.1f, 0.2f);
    }

    /// <summary>
    /// 個人の政治信条（<see cref="Creed"/>）と所属勢力イデオロギーのズレ＝政治的緊張の純ロジック（PER-PROPS A2）。
    /// 緊張が高いほど離反・クーデターの火種。数値の離反力学は既存の <see cref="AllegianceDriftRules"/>/
    /// <see cref="AmbitionRules"/> へ橋渡しする（ここは緊張係数 0..1 を返すだけ＝再実装しない）。決定論・test-first。
    /// </summary>
    public static class CreedRules
    {
        /// <summary>勢力イデオロギー文字列を Creed へ（既知語のみ・空/未知は 無関心）。FactionData.ideology 用。</summary>
        public static Creed Parse(string ideology)
        {
            if (string.IsNullOrEmpty(ideology)) return Creed.無関心;
            if (ideology.Contains("共和") || ideology.Contains("民主")) return Creed.共和主義;
            if (ideology.Contains("帝政") || ideology.Contains("君主")) return Creed.帝政擁護;
            if (ideology.Contains("門閥") || ideology.Contains("貴族")) return Creed.門閥主義;
            if (ideology.Contains("能力") || ideology.Contains("実力")) return Creed.能力主義;
            if (ideology.Contains("中道")) return Creed.中道;
            return Creed.無関心;
        }

        /// <summary>真っ向対立する信条ペアか（共和↔帝政、門閥↔能力）。</summary>
        public static bool IsOpposed(Creed a, Creed b)
        {
            return (a == Creed.共和主義 && b == Creed.帝政擁護) || (a == Creed.帝政擁護 && b == Creed.共和主義)
                || (a == Creed.門閥主義 && b == Creed.能力主義) || (a == Creed.能力主義 && b == Creed.門閥主義);
        }

        /// <summary>個人信条 personal と勢力信条 factionCreed のズレ＝緊張 0..1（既定パラメータ）。</summary>
        public static float Tension(Creed personal, Creed factionCreed)
            => Tension(personal, factionCreed, CreedParams.Default);

        /// <summary>緊張 0..1。一致=0／無関心側=apathetic／中道側=moderate／対立=opposed／その他=divergent。</summary>
        public static float Tension(Creed personal, Creed factionCreed, CreedParams p)
        {
            if (personal == factionCreed) return 0f;
            if (personal == Creed.無関心 || factionCreed == Creed.無関心) return Mathf.Clamp01(p.apathetic);
            if (personal == Creed.中道 || factionCreed == Creed.中道) return Mathf.Clamp01(p.moderate);
            if (IsOpposed(personal, factionCreed)) return Mathf.Clamp01(p.opposed);
            return Mathf.Clamp01(p.divergent);
        }

        /// <summary>緊張×scale を忠誠から引くための減算量（AllegianceDriftRules の忠誠目標へ反映する補助）。</summary>
        public static float LoyaltyPenalty(float tension, float scale)
            => Mathf.Clamp01(tension) * Mathf.Max(0f, scale);
    }
}
