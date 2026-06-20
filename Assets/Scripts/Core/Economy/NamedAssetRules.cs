using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ネームド資産の評価・収益の純ロジック（NASSET-3・#2063・実効値パターン・基準非破壊）。
    /// 領地/企業は収益を生み、邸宅/宮殿/旗艦は維持費がかかる＝純収益は赤字もありうる（見栄の維持費）。
    /// 美術品/財宝は値上がり（暴落#185 で負も）。固有資産の威信は支持#113/正統性へ。test-first。
    /// </summary>
    public static class NamedAssetRules
    {
        /// <summary>粗収益＝時価×年間収益率。</summary>
        public static float GrossYield(NamedAsset a)
            => a == null ? 0f : Mathf.Max(0f, a.value) * a.yieldRate;

        /// <summary>維持費＝時価×維持費率。</summary>
        public static float Upkeep(NamedAsset a)
            => a == null ? 0f : Mathf.Max(0f, a.value) * a.upkeepRate;

        /// <summary>年間純収益＝粗収益−維持費（赤字もありうる＝維持費の重い宮殿）。</summary>
        public static float NetAnnualIncome(NamedAsset a)
            => GrossYield(a) - Upkeep(a);

        /// <summary>1年後の時価＝max(0, 時価×(1+値上がり率))。美術品/財宝は上がり、暴落#185 で負率なら下がる。</summary>
        public static float ValueAfterYear(float value, float appreciationRate)
            => Mathf.Max(0f, value * (1f + appreciationRate));

        /// <summary>威信寄与＝固有資産の格（支持#113/正統性へ）。負値は0でクランプ。</summary>
        public static float PrestigeContribution(NamedAsset a)
            => a == null ? 0f : Mathf.Max(0f, a.prestige);
    }
}
