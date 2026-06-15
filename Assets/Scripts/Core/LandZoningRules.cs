using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 惑星の土地分割（ゾーニング）の純ロジック（#2019 惑星土地・唯一の窓口）。惑星の土地を<b>用途（地目）ごとの区画</b>に分け
    /// （住宅地/商業地/工業地/農地/鉱山）、惑星の大きさに応じて分割数（<see cref="MaxSubdivisions"/>）が決まる。各ゾーンの価値は
    /// 惑星の地価（<see cref="PlanetRealEstate.landValue"/>・#2019）に用途の比率（<see cref="ZoneWeight"/>）を掛けたもの。
    /// 区画ごとに持分（<see cref="PropertyDeed"/> の zoned 権利）で所有・取引する土台。経済類型 <see cref="SystemType"/> で各用途の比率が変わる。
    /// 個別区画 micro へは降りない（用途×集約＝タイクン化回避）。test-first。
    /// </summary>
    public static class LandZoningRules
    {
        /// <summary>用途の数（住宅地/商業地/工業地/農地/鉱山）。</summary>
        public static readonly LandUseType[] AllUses =
            { LandUseType.住宅地, LandUseType.商業地, LandUseType.工業地, LandUseType.農地, LandUseType.鉱山 };

        // 1区画あたりの人口規模（惑星の大きさ＝人口を区画数へ写す）／分割数の下限・上限（タイクン化回避＝有界）。
        public const float PopulationPerParcel = 50f;
        public const int MinSubdivisions = 1;
        public const int MaxSubdivisionsCap = 20;

        /// <summary>
        /// 経済類型ごとの用途比率（0..1・各類型で合計1）。住みよい類型は住宅/商業が厚く、鉱業は鉱山が厚い…＝惑星の地目の偏り。
        /// </summary>
        public static float ZoneWeight(SystemType type, LandUseType use)
        {
            switch (type)
            {
                case SystemType.農業:
                    switch (use) { case LandUseType.住宅地: return 0.20f; case LandUseType.商業地: return 0.10f; case LandUseType.工業地: return 0.05f; case LandUseType.農地: return 0.60f; default: return 0.05f; }
                case SystemType.工業:
                    switch (use) { case LandUseType.住宅地: return 0.20f; case LandUseType.商業地: return 0.15f; case LandUseType.工業地: return 0.50f; case LandUseType.農地: return 0.05f; default: return 0.10f; }
                case SystemType.鉱業:
                    switch (use) { case LandUseType.住宅地: return 0.15f; case LandUseType.商業地: return 0.10f; case LandUseType.工業地: return 0.15f; case LandUseType.農地: return 0.05f; default: return 0.55f; }
                default: // 居住
                    switch (use) { case LandUseType.住宅地: return 0.50f; case LandUseType.商業地: return 0.25f; case LandUseType.工業地: return 0.10f; case LandUseType.農地: return 0.10f; default: return 0.05f; }
            }
        }

        /// <summary>用途比率（null Province は居住既定）。</summary>
        public static float ZoneWeight(Province prov, LandUseType use)
            => ZoneWeight(prov != null ? prov.systemType : SystemType.居住, use);

        /// <summary>惑星の大きさ（区画化の素＝人口を大きさの代理に）。非負。</summary>
        public static float PlanetSize(Province prov) => prov == null ? 0f : Mathf.Max(0f, prov.population);

        /// <summary>惑星の大きさに応じた最大分割数（大きい惑星ほど多くの区画に分けられる・上下限でクランプ＝タイクン化回避）。</summary>
        public static int MaxSubdivisions(Province prov)
        {
            float size = PlanetSize(prov);
            int n = MinSubdivisions + Mathf.FloorToInt(size / PopulationPerParcel);
            return Mathf.Clamp(n, MinSubdivisions, MaxSubdivisionsCap);
        }

        /// <summary>用途ゾーンの価値＝惑星の地価×用途比率（惑星の不動産基盤 #2019 を地目で按分）。</summary>
        public static float ZoneValue(float planetLandValue, SystemType type, LandUseType use)
            => Mathf.Max(0f, planetLandValue) * ZoneWeight(type, use);

        /// <summary>用途ゾーンの価値（Province 版＝地価は <see cref="PlanetRealEstate.landValue"/> を読む）。</summary>
        public static float ZoneValue(Province prov, LandUseType use)
        {
            if (prov == null) return 0f;
            float landValue = prov.realEstate != null ? prov.realEstate.landValue : 0f;
            return ZoneValue(landValue, prov.systemType, use);
        }

        /// <summary>権利証が表す土地の全惑星換算の持分＝zoned ならゾーン比率×持分、whole なら持分そのまま。</summary>
        public static float WholePlanetEquivalentShare(PropertyDeed d, SystemType type)
        {
            if (d == null) return 0f;
            float s = Mathf.Max(0f, d.share);
            return d.zoned ? s * ZoneWeight(type, d.landUse) : s;
        }
    }
}
