using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 賃金・労働分配の需給連動（POPLAB-4・#2026・#1969/#181/#113 連携・純ロジック）。
    /// 職業別賃金を労働需給で動かす（人手不足＝賃金↑・過剰＝↓）→実質賃金→生活水準#181/支持#113。
    /// 既存 <see cref="WageRules"/>（#1969）の基準賃金に需給係数を掛ける（実効値パターン・基準非破壊）。係数で背景的に（タイクン回避）。test-first。
    /// </summary>
    public static class LaborWageRules
    {
        public const float DefaultWageElasticity = 0.5f; // 需給→賃金の感応度

        /// <summary>需給係数＝1+(求人/求職−1)×感応度（人手不足で>1・過剰で<1）。求職0以下は人手不足扱いで上限へ。</summary>
        public static float WageDemandFactor(float jobOpenings, float jobSeekers, float elasticity)
        {
            if (jobSeekers <= 0f) return 1f + Mathf.Max(0f, elasticity); // 求職ゼロ＝極端な人手不足
            float ratio = Mathf.Max(0f, jobOpenings) / jobSeekers;
            return Mathf.Max(0f, 1f + (ratio - 1f) * Mathf.Max(0f, elasticity));
        }

        /// <summary>職業別賃金＝基準賃金#1969×需給係数。</summary>
        public static float OccupationWage(float baseWage, float demandFactor)
            => Mathf.Max(0f, baseWage) * Mathf.Max(0f, demandFactor);

        /// <summary>実質賃金＝名目賃金/物価指数（#1951 デフレータ）。物価0以下は名目。</summary>
        public static float RealWage(float nominalWage, float priceIndex)
            => priceIndex <= 0f ? Mathf.Max(0f, nominalWage) : Mathf.Max(0f, nominalWage) / priceIndex;

        /// <summary>賃金→支持の増減＝(実質賃金−生存水準)/生存水準×スケール（高賃金で支持↑・困窮で↓）。生存0以下は0。</summary>
        public static float WageSupportDelta(float realWage, float subsistence, float scale)
        {
            if (subsistence <= 0f) return 0f;
            return (Mathf.Max(0f, realWage) - subsistence) / subsistence * Mathf.Max(0f, scale);
        }
    }
}
