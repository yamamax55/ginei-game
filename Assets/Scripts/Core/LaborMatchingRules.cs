using UnityEngine;

namespace Ginei
{
    /// <summary>失業の種類（POPLAB-3・#2026）。摩擦的＝転職中の常時少量／構造的＝職業ミスマッチ／循環的＝総需要不足（景気）。</summary>
    public enum UnemploymentType { 摩擦的, 構造的, 循環的 }

    /// <summary>
    /// 労働需給マッチングと失業の構造化（POPLAB-3・#2026・純ロジック）。
    /// 職業別に求人（需要）×求職（供給）を突き合わせ、就業・失業・人手不足を出し、失業を摩擦的/構造的/循環的に分解する。
    /// 惑星の失業は国マクロ（<see cref="LaborRules"/> #1957）の下位供給源。集約・後方互換。test-first。
    /// </summary>
    public static class LaborMatchingRules
    {
        /// <summary>就業者数＝min(求職, 求人)。</summary>
        public static float Employed(float jobSeekers, float jobOpenings)
            => Mathf.Min(Mathf.Max(0f, jobSeekers), Mathf.Max(0f, jobOpenings));

        /// <summary>失業者数＝max(0, 求職−求人)（超過供給）。</summary>
        public static float Unemployed(float jobSeekers, float jobOpenings)
            => Mathf.Max(0f, Mathf.Max(0f, jobSeekers) - Mathf.Max(0f, jobOpenings));

        /// <summary>人手不足＝max(0, 求人−求職)（超過需要）。</summary>
        public static float Shortage(float jobSeekers, float jobOpenings)
            => Mathf.Max(0f, Mathf.Max(0f, jobOpenings) - Mathf.Max(0f, jobSeekers));

        /// <summary>失業率＝失業者/労働力。労働力0以下は0。</summary>
        public static float UnemploymentRate(float unemployed, float laborForce)
            => laborForce <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, unemployed) / laborForce);

        /// <summary>摩擦的失業＝労働力×摩擦率（転職中＝常時少量）。</summary>
        public static float FrictionalUnemployment(float laborForce, float frictionalRate)
            => Mathf.Max(0f, laborForce) * Mathf.Clamp01(frictionalRate);

        /// <summary>循環的失業＝max(0, 総失業−摩擦的−構造的)（残り＝総需要不足）。</summary>
        public static float CyclicalUnemployment(float totalUnemployed, float frictional, float structural)
            => Mathf.Max(0f, Mathf.Max(0f, totalUnemployed) - Mathf.Max(0f, frictional) - Mathf.Max(0f, structural));

        /// <summary>失業の3分解＝摩擦/構造を引き、残りを循環的に（合計＝総失業）。</summary>
        public static float Decompose(float totalUnemployed, float frictional, float structural, UnemploymentType type)
        {
            switch (type)
            {
                case UnemploymentType.摩擦的: return Mathf.Min(Mathf.Max(0f, frictional), Mathf.Max(0f, totalUnemployed));
                case UnemploymentType.構造的: return Mathf.Min(Mathf.Max(0f, structural), Mathf.Max(0f, totalUnemployed - Mathf.Max(0f, frictional)));
                default:                       return CyclicalUnemployment(totalUnemployed, frictional, structural);
            }
        }
    }
}
