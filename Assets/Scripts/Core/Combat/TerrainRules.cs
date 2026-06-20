using UnityEngine;

namespace Ginei
{
    /// <summary>戦場の地形（#2181）。小惑星帯＝航行障害＋遮蔽、星雲＝索敵/射程低下。</summary>
    public enum TerrainType { 小惑星帯, 星雲 }

    /// <summary>
    /// 戦場の地形効果（#2181）の純ロジック。小惑星帯は減速＋射程低下（瓦礫の遮蔽）、星雲は射程/索敵低下（視界）。
    /// 位置取りの判断を増やす。倍率を返すだけ（実効値パターン・基準値非破壊）。test-first。
    /// </summary>
    public static class TerrainRules
    {
        public const float AsteroidSpeed = 0.6f;  // 小惑星帯の減速
        public const float AsteroidRange = 0.8f;  // 小惑星帯の射程低下（遮蔽）
        public const float NebulaRange = 0.6f;     // 星雲の射程/索敵低下

        /// <summary>その地形での機動倍率（小惑星帯のみ減速・星雲は不変）。</summary>
        public static float SpeedFactor(TerrainType t)
            => t == TerrainType.小惑星帯 ? AsteroidSpeed : 1f;

        /// <summary>その地形での射程/索敵倍率（小惑星帯=遮蔽・星雲=視界低下）。</summary>
        public static float RangeFactor(TerrainType t)
            => t == TerrainType.小惑星帯 ? AsteroidRange : NebulaRange;
    }
}
