using UnityEngine;

namespace Ginei
{
    /// <summary>星間地形（宙域の種類）。戦闘の探知・移動・命中・遮蔽に影響する。</summary>
    public enum SpaceTerrain
    {
        通常宙域,   // 何も影響しない基準
        星雲,       // ガス星雲＝センサー・命中が落ち、潜む側に有利
        小惑星帯,   // 機動が落ち、遮蔽が増す
        重力圏,     // ブラックホール/恒星近傍＝機動が大きく落ちる
        恒星近傍    // 強光・放射＝命中が落ち遮蔽は少ない
    }

    /// <summary>星間地形の調整係数（各地形の探知/機動/命中/遮蔽の倍率を保持）。</summary>
    public readonly struct TerrainParams
    {
        public readonly float detection; // 探知倍率（1=基準）
        public readonly float movement;  // 機動倍率
        public readonly float accuracy;  // 命中倍率
        public readonly float cover;     // 遮蔽による被ダメ軽減 0..1

        public TerrainParams(float detection, float movement, float accuracy, float cover)
        {
            this.detection = Mathf.Max(0f, detection);
            this.movement = Mathf.Max(0f, movement);
            this.accuracy = Mathf.Max(0f, accuracy);
            this.cover = Mathf.Clamp01(cover);
        }
    }

    /// <summary>
    /// 星間地形の純ロジック（宙域特性）。地形ごとに探知・機動・命中の倍率と遮蔽量を返す＝星雲では探り合いになり、
    /// 重力圏では身動きが取れない、を表す。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 既定の地形プリセットは const に集約し、必要なら <see cref="TerrainParams"/> で上書きできる。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TerrainRules
    {
        /// <summary>地形ごとの既定パラメータ（探知/機動/命中/遮蔽）。</summary>
        public static TerrainParams Profile(SpaceTerrain terrain)
        {
            switch (terrain)
            {
                case SpaceTerrain.星雲:     return new TerrainParams(0.4f, 0.9f, 0.6f, 0.2f);
                case SpaceTerrain.小惑星帯: return new TerrainParams(0.9f, 0.6f, 0.9f, 0.35f);
                case SpaceTerrain.重力圏:   return new TerrainParams(0.8f, 0.4f, 0.85f, 0.0f);
                case SpaceTerrain.恒星近傍: return new TerrainParams(0.7f, 0.85f, 0.7f, 0.0f);
                case SpaceTerrain.通常宙域:
                default:                    return new TerrainParams(1f, 1f, 1f, 0f);
            }
        }

        /// <summary>探知倍率（センサー範囲・探知率に掛ける）。星雲で最も落ちる。</summary>
        public static float DetectionFactor(SpaceTerrain terrain) => Profile(terrain).detection;

        /// <summary>機動倍率（移動・回頭速度に掛ける）。重力圏で最も落ちる。</summary>
        public static float MovementFactor(SpaceTerrain terrain) => Profile(terrain).movement;

        /// <summary>命中倍率（ダメージ・命中に掛ける）。星雲・恒星近傍で落ちる。</summary>
        public static float AccuracyFactor(SpaceTerrain terrain) => Profile(terrain).accuracy;

        /// <summary>遮蔽による被ダメージ軽減（0..1）。小惑星帯で最大。</summary>
        public static float CoverBonus(SpaceTerrain terrain) => Profile(terrain).cover;

        /// <summary>遮蔽を反映した被ダメージ倍率（1−cover）。基準ダメージに掛けて使う。</summary>
        public static float DamageTakenFactor(SpaceTerrain terrain) => 1f - CoverBonus(terrain);

        /// <summary>
        /// 地形が攻撃側／守備側どちらに有利かの目安（&gt;0 で守備有利、&lt;0 で攻撃有利、0 で中立）。
        /// 命中低下と遮蔽は守備有利、機動低下は接近を阻むため弱く攻撃寄りとみなす単純指標。
        /// </summary>
        public static float DefensiveBias(SpaceTerrain terrain)
        {
            TerrainParams t = Profile(terrain);
            return (1f - t.accuracy) + t.cover - (1f - t.movement) * 0.5f;
        }
    }
}
