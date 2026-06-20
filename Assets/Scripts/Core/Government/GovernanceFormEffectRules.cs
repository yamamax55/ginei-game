using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政体特性→実効値の橋渡し（#政体・唯一の窓口・test-first）。<see cref="GovernanceFormCatalog"/> の差別化特性を
    /// <b>既存窓口へ繋ぐ係数</b>へ翻訳する＝合意/統制の崩壊は <see cref="ConsentRules"/>・統治安定は
    /// <see cref="GovernanceRules"/>・軍政の型は <see cref="CivilianControlRules"/>（<see cref="GovernmentFormRules.ControlTypeOf"/>）へ
    /// 委譲し、本ルールは「形態がそれらをどれだけ強め/弱めるか」の倍率だけを出す。数式は二重実装しない。
    /// 戻り値はすべて倍率/0..1スケールで、基準値を破壊しない（実効値パターン）。乱数なし・決定論・null/範囲安全。
    /// </summary>
    public static class GovernanceFormEffectRules
    {
        /// <summary>
        /// 統治安定への形態係数（中立=1.0）。正統性源・腐敗耐性・継承安定の合成＝<see cref="GovernanceRules.EquilibriumStability"/>
        /// の ideologyMod へ掛ける形態ボーナス。確立した正統性と制度的歯止めを持つ形態ほど 1.0 を上回る。
        /// </summary>
        public static float StabilityFactor(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            float quality = (p.legitimacySource + p.corruptionResistance + p.successionStability) / 3f; // 0..1
            return Mathf.Lerp(0.6f, 1.4f, quality); // 質0で0.6倍・質1で1.4倍
        }

        /// <summary>
        /// 形態が抱える合意崩壊リスク（0..1）。民衆同意依存が高く正統性源が低いほど合意を失いやすい
        /// ＝<see cref="ConsentRules.ControlStrength"/>／<see cref="ConsentRules.IsUngovernable"/> の発火しやすさの起点。
        /// 抑圧能力は崩壊を表面上は遅らせる（力で抑える）ため軽減する。
        /// </summary>
        public static float ConsentFragility(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            float exposed = p.popularConsentDependence * (1f - p.legitimacySource); // 同意依存×正統性の薄さ
            float suppressed = exposed * (1f - 0.5f * p.repressionCapacity);        // 抑圧で半分まで遅らせる
            return Mathf.Clamp01(suppressed);
        }

        /// <summary>形態→軍政の型（既存窓口へ委譲＝クーデターリスク/任免の起点）。並行定義を作らない。</summary>
        public static CivilianControlType ControlTypeOf(GovernanceArchetype a)
            => GovernmentFormRules.ControlTypeOf(GovernanceFormCatalog.ToGovernmentForm(a));

        /// <summary>
        /// 文民統制の強さ（0..1）＝<see cref="CivilianControlRules.WouldCoup"/> の controlStrength へ渡す代理。
        /// 正統性源・腐敗耐性・継承安定が高い＝制度が軍を抑え込めるほど高い。抑圧能力は両刃だが軍掌握にも効くため少し足す。
        /// </summary>
        public static float MilitaryControlStrength(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            float institutional = (p.legitimacySource + p.corruptionResistance + p.successionStability) / 3f;
            float v = 0.75f * institutional + 0.25f * p.repressionCapacity;
            return Mathf.Clamp01(v);
        }

        /// <summary>
        /// 政策決定の機動力（中立=1.0）。決定速度が高い形態（独裁/共産/君主）は即断で 1.0 超、
        /// 合議形態（民主/共和）は 1.0 未満＝速さと熟議のトレードオフ。動員/弾圧の即応性の起点。
        /// </summary>
        public static float DecisionAgilityFactor(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            return Mathf.Lerp(0.7f, 1.3f, p.decisionSpeed);
        }

        /// <summary>
        /// 弾圧（<see cref="GovernancePolicy.弾圧"/>）の実効度合い（中立=1.0）。抑圧能力が高い形態ほど弾圧が効く。
        /// <see cref="GovernanceRules.PolicyIntegrationMultiplier"/> 等と併用する形態側の倍率。
        /// </summary>
        public static float RepressionEffectiveness(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            return Mathf.Lerp(0.7f, 1.3f, p.repressionCapacity);
        }

        /// <summary>
        /// 利潤の集権度（0..1）。共産が最高＝国有で利潤が国庫へ集まる。<see cref="PropertyRules"/> の所有判定
        /// （<see cref="GovernmentFormRules.OwnershipOf"/>）と整合する経済集権スカラ。
        /// </summary>
        public static float EconomicCentralization(GovernanceArchetype a)
            => GovernanceFormCatalog.Profile(a).economicCentralization;

        /// <summary>
        /// 腐敗の進みやすさ（0..1）＝腐敗耐性の裏返し。<see cref="GovernmentPrincipleRules.PrincipleCorruption"/> 等の
        /// 形態側の増幅倍率の起点（耐性が低い形態ほど腐敗が速い）。
        /// </summary>
        public static float CorruptionSusceptibility(GovernanceArchetype a)
            => Mathf.Clamp01(1f - GovernanceFormCatalog.Profile(a).corruptionResistance);

        /// <summary>形態→アナキュクローシスの制度的歯止め（0..1）＝<see cref="AnacyclosisRules.FormStability"/> へ渡す
        /// institutionalCheck の代理。腐敗耐性と正統性源が高い形態ほど循環のブレーキが効く（混合政体に近い）。</summary>
        public static float InstitutionalCheck(GovernanceArchetype a)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(a);
            return Mathf.Clamp01(0.6f * p.corruptionResistance + 0.4f * p.legitimacySource);
        }
    }
}
