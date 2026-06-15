using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// AI艦隊の陣形ドクトリンの純ロジック（#72/#104 拡張・会戦改善）。
    /// 戦況（自/敵の兵力比・敗走）と提督の得意陣形から、有利な陣形を推奨する。AI が自動切替に使う。
    /// 陣形の定義は `Formation`（#496 Core）。test-first。
    /// </summary>
    public static class FormationDoctrineRules
    {
        public const float OutnumberedRatio = 0.6f;  // 自/敵 がこれ以下＝劣勢
        public const float OutnumberingRatio = 1.5f; // 自/敵 がこれ以上＝優勢

        /// <summary>
        /// 推奨陣形：敗走＝円陣（全周防御で逃げる）／劣勢＝方陣（堅く守る）／優勢＝鶴翼陣（包囲）／
        /// それ以外は提督の得意陣形（あれば）／既定は紡錘陣。
        /// </summary>
        public static Formation RecommendFormation(float ownStrength, float enemyStrength, bool isRouted, AdmiralData admiral)
        {
            if (isRouted) return Formation.円陣;
            float ratio = Mathf.Max(0f, ownStrength) / Mathf.Max(1f, enemyStrength);
            if (ratio <= OutnumberedRatio) return Formation.方陣;
            if (ratio >= OutnumberingRatio) return Formation.鶴翼陣;
            if (admiral != null && admiral.hasPreferredFormation) return admiral.preferredFormation;
            return Formation.紡錘陣;
        }
    }
}
