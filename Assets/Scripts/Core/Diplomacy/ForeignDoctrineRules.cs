using UnityEngine;

namespace Ginei
{
    /// <summary>国家の対外主義（ALM-5 #1059）。政体や人物の信条とは別の、外交関係が自然に寄る方向。</summary>
    public enum ForeignDoctrine
    {
        保守,
        改革,
        中道,
        孤立,
        中立
    }

    /// <summary>
    /// 対外主義どうしの親和を既存 <see cref="DiplomacyRules.OpinionFactors.ideologyAffinity"/> へ渡す橋。
    /// 保守↔中道↔改革は一本の軸、孤立は他国との恒常的な距離、中立は主義による増減なしとして扱う。
    /// </summary>
    public static class ForeignDoctrineRules
    {
        /// <summary>主義だけから生じる関係親和（-1..1、対称）。</summary>
        public static float Affinity(ForeignDoctrine a, ForeignDoctrine b)
        {
            if (a == ForeignDoctrine.中立 || b == ForeignDoctrine.中立) return 0f;
            if (a == ForeignDoctrine.孤立 || b == ForeignDoctrine.孤立)
                return a == b ? 0.25f : -0.75f;

            int distance = Mathf.Abs(Axis(a) - Axis(b));
            switch (distance)
            {
                case 0: return 1f;
                case 1: return 0.35f;
                default: return -1f;
            }
        }

        /// <summary>
        /// 既存の思想親和と主義親和を混合する。doctrineShare=0なら従来値、1なら主義だけ。
        /// 戻り値を OpinionFactors の ideologyAffinity に渡すことで、既存のドリフト入口を再利用できる。
        /// </summary>
        public static float BlendWithIdeology(float ideologyAffinity,
            ForeignDoctrine a, ForeignDoctrine b, float doctrineShare = 0.5f)
        {
            float share = Mathf.Clamp01(doctrineShare);
            return Mathf.Clamp(Mathf.Lerp(Mathf.Clamp(ideologyAffinity, -1f, 1f), Affinity(a, b), share), -1f, 1f);
        }

        private static int Axis(ForeignDoctrine doctrine)
        {
            switch (doctrine)
            {
                case ForeignDoctrine.保守: return -1;
                case ForeignDoctrine.改革: return 1;
                default: return 0;
            }
        }
    }
}
