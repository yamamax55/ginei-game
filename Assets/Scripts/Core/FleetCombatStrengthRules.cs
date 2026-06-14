using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給レディネス×技術×軍の質→「実効戦力倍率」を返す薄い橋渡し（純ロジック・read-only・test-first・唯一の窓口）。
    /// 戦略会戦の戦力（<see cref="StrategicFleet.strength"/>＝raw な頭数）に1つ掛けるだけで、補給切れ・技術差・部隊の質が効く設計点。
    /// <b>数式は二重実装せず既存窓口へ委譲する</b>：
    ///   補給＝<see cref="MilitaryReadinessRules.FirepowerFactor"/>（0.1〜1.0・弾切れで火力ほぼ0／満補給で1.0）。
    ///   技術＝<see cref="TechEffectRules.CombatStrengthFactor"/>（1.0〜上限・techLevel=0 で1.0＝後方互換）。
    ///   質  ＝呼び出し側が <see cref="ForceQualityFactorRules.QualityFactor"/>（または <see cref="ForceQualityRules.CombatMultiplier"/>）で
    ///         算出した倍率をそのまま渡す（中立=1.0）。
    /// 合成は3倍率の積を <see cref="MinFactor"/>〜<see cref="MaxFactor"/> でクランプ（bounded＝0倍や無限を防ぐ）。
    /// 全中立入力（<paramref name="supplyReadiness"/>=1・<paramref name="techLevel"/>=0・<paramref name="qualityFactor"/>=1）で 1.0（後方互換）。
    /// 乱数なし・決定論・個体粒度へ降りない（集約のみ＝スケーラビリティ規律）。
    /// </summary>
    public static class FleetCombatStrengthRules
    {
        /// <summary>実効戦力倍率の下限（質崩壊×補給切れでも全滅的0倍を防ぐ）。</summary>
        public const float MinFactor = 0.1f;
        /// <summary>実効戦力倍率の上限（満補給×高技術×精鋭の頭打ち＝バランス規律）。</summary>
        public const float MaxFactor = 3.0f;

        /// <summary>
        /// 実効戦力倍率＝補給火力係数 × 技術戦力係数 × 軍の質倍率（<see cref="MinFactor"/>〜<see cref="MaxFactor"/> でクランプ）。
        /// </summary>
        /// <param name="supplyReadiness">補給レディネス（0..1・<see cref="StrategicFleet.supply"/>＝満補給1.0）。</param>
        /// <param name="techLevel">技術水準の段（0で中立＝1.0倍・負は0扱い）。</param>
        /// <param name="qualityFactor">軍の質倍率（<see cref="ForceQualityFactorRules.QualityFactor"/> 等の出力・中立1.0・負は0クランプ）。</param>
        public static float EffectiveCombatFactor(float supplyReadiness, float techLevel, float qualityFactor)
        {
            float supplyF = MilitaryReadinessRules.FirepowerFactor(supplyReadiness); // 0.1〜1.0
            float techF = TechEffectRules.CombatStrengthFactor(techLevel);           // 1.0〜上限
            float qualityF = Mathf.Max(0f, qualityFactor);                            // 中立1.0
            return Mathf.Clamp(supplyF * techF * qualityF, MinFactor, MaxFactor);
        }

        /// <summary>
        /// 実効戦力＝raw 戦力 × <see cref="EffectiveCombatFactor"/>。負の raw は0扱い（兵力は非負）。
        /// 戦略会戦の戦力比較・損耗計算に渡す「実際に戦う重み」を返す。
        /// </summary>
        public static float EffectiveStrength(float rawStrength, float supplyReadiness, float techLevel, float qualityFactor)
            => Mathf.Max(0f, rawStrength) * EffectiveCombatFactor(supplyReadiness, techLevel, qualityFactor);
    }
}
