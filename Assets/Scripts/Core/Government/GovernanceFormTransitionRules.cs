using UnityEngine;

namespace Ginei
{
    /// <summary>政体移行を駆動する社会的圧力（すべて 0..1）。形態の遷移傾向の入力。</summary>
    public readonly struct GovernanceFormPressures
    {
        /// <summary>不安定（高＝統治が揺れる＝循環/政変の燃料）。</summary>
        public readonly float instability;
        /// <summary>格差（高＝寡頭化/革命圧力＝少数私利支配 or 収奪への反発）。</summary>
        public readonly float inequality;
        /// <summary>戦時圧（高＝権力集中・独裁化を促す）。</summary>
        public readonly float war;
        /// <summary>後継危機（高＝世襲/個人支配が揺らぐ）。</summary>
        public readonly float successionCrisis;
        /// <summary>民衆同意（高＝開かれた形態を支え・低＝革命/独裁化の燃料）。</summary>
        public readonly float popularConsent;

        public GovernanceFormPressures(float instability, float inequality, float war,
            float successionCrisis, float popularConsent)
        {
            this.instability = Mathf.Clamp01(instability);
            this.inequality = Mathf.Clamp01(inequality);
            this.war = Mathf.Clamp01(war);
            this.successionCrisis = Mathf.Clamp01(successionCrisis);
            this.popularConsent = Mathf.Clamp01(popularConsent);
        }
    }

    /// <summary>政体移行の推奨（次形態・遷移傾向0..1・循環方向か）。</summary>
    public readonly struct GovernanceTransition
    {
        /// <summary>移行先のアーキタイプ（傾向が低ければ現状維持＝<paramref name="from"/> と同じ）。</summary>
        public readonly GovernanceArchetype to;
        /// <summary>遷移傾向（0..1・高いほど移りやすい）。</summary>
        public readonly float propensity;
        /// <summary>アナキュクローシス（政体循環）方向の遷移か（true＝循環論に沿う・false＝危機による飛び）。</summary>
        public readonly bool cyclical;

        public GovernanceTransition(GovernanceArchetype to, float propensity, bool cyclical)
        {
            this.to = to;
            this.propensity = Mathf.Clamp01(propensity);
            this.cyclical = cyclical;
        }
    }

    /// <summary>
    /// 政体移り変わりのオーケストレータ（#政体・政治形態の移り変わり・test-first）。<b>循環の核は再実装せず</b>
    /// 既存 <see cref="AnacyclosisRules"/>（六政体循環＝王政→僭主政→…）へ委譲し、危機による形態転換は
    /// <see cref="PoliticalUpheavalRules"/>（軍政#145→政変#215）へ委譲する。本ルールは7アーキタイプ
    /// （<see cref="GovernanceArchetype"/>）と六政体（<see cref="RegimeForm"/>）の<b>分類対応</b>を取り、圧力から
    /// 「次に移りやすい形態」と「遷移傾向」を組み立てる上位層＝既存の遷移ロジックの置換でなく被せ物。
    /// 乱数なし・決定論（roll は呼び出し側が渡す）・null/範囲安全。
    /// </summary>
    public static class GovernanceFormTransitionRules
    {
        /// <summary>遷移傾向がこれ以上で実際に移ると見なす既定閾値。</summary>
        public const float DefaultTransitionThreshold = 0.5f;

        /// <summary>
        /// アーキタイプ→六政体（アナキュクローシス分類）。民主主義/共和制＝民主政・寡頭政治＝寡頭政・
        /// 独裁政治＝僭主政（一者の暴政）・君主制＝王政・立憲君主制＝王政（立憲化＝善政側）・共産主義＝寡頭政
        /// （党＝少数支配）。循環論の輪へ載せるための対応（<see cref="AnacyclosisRules"/> へ委譲する橋）。
        /// </summary>
        public static RegimeForm ToRegimeForm(GovernanceArchetype a)
        {
            switch (a)
            {
                case GovernanceArchetype.民主主義:   return RegimeForm.民主政;
                case GovernanceArchetype.共和制:     return RegimeForm.民主政;
                case GovernanceArchetype.君主制:     return RegimeForm.王政;
                case GovernanceArchetype.立憲君主制: return RegimeForm.王政;
                case GovernanceArchetype.寡頭政治:   return RegimeForm.寡頭政;
                case GovernanceArchetype.共産主義:   return RegimeForm.寡頭政; // 党＝少数支配
                case GovernanceArchetype.独裁政治:   return RegimeForm.僭主政;
                default:                             return RegimeForm.僭主政;
            }
        }

        /// <summary>
        /// 六政体→代表アーキタイプ（循環の次形態を7アーキタイプへ戻す写像）。僭主政＝独裁政治・寡頭政＝寡頭政治・
        /// 衆愚政＝民主主義（多数の暴走も多数支配）・王政＝君主制・貴族政＝寡頭政治・民主政＝共和制。
        /// </summary>
        public static GovernanceArchetype FromRegimeForm(RegimeForm r)
        {
            switch (r)
            {
                case RegimeForm.王政:   return GovernanceArchetype.君主制;
                case RegimeForm.僭主政: return GovernanceArchetype.独裁政治;
                case RegimeForm.貴族政: return GovernanceArchetype.寡頭政治;
                case RegimeForm.寡頭政: return GovernanceArchetype.寡頭政治;
                case RegimeForm.民主政: return GovernanceArchetype.共和制;
                default:                return GovernanceArchetype.民主主義; // 衆愚政
            }
        }

        /// <summary>
        /// 圧力から遷移傾向（0..1）を出す。形態の固有脆さ（特性カタログ）×外圧の合成＝<b>形態ごとに違う弱点</b>
        /// が効く：民衆同意依存が高い形態は同意喪失に弱く、継承安定が低い形態は後継危機に弱い、腐敗耐性が低い
        /// 形態は格差/不安定に弱い、抑圧能力が低い形態は戦時の独裁化圧に流されやすい。数値は特性表へ委譲。
        /// </summary>
        public static float TransitionPropensity(GovernanceArchetype from, GovernanceFormPressures pr)
        {
            GovernanceFormProfile p = GovernanceFormCatalog.Profile(from);

            float consentStress = (1f - pr.popularConsent) * p.popularConsentDependence; // 同意喪失×依存
            float successionStress = pr.successionCrisis * (1f - p.successionStability);   // 後継危機×継承の弱さ
            float corruptStress = (pr.inequality + pr.instability) * 0.5f * (1f - p.corruptionResistance);
            float warStress = pr.war * (1f - p.repressionCapacity) * 0.6f;                 // 戦時の独裁化圧

            // 平均でなく「最も効く弱点」を重く見る（最大＋残りの寄与）。係数は単一の危機で閾値を越え、
            // 平時は十分低く留まるよう調整（複合危機は加算で増幅）。
            float maxStress = Mathf.Max(Mathf.Max(consentStress, successionStress), Mathf.Max(corruptStress, warStress));
            float sumStress = consentStress + successionStress + corruptStress + warStress;
            float blended = Mathf.Clamp01(0.85f * maxStress + 0.2f * sumStress);
            return blended;
        }

        /// <summary>
        /// 次に移りやすい形態を推奨する。①革命級の圧（同意崩壊＋格差/不安定）が高ければ <see cref="PoliticalUpheavalRules"/>
        /// 系の危機遷移（収奪的＝共産主義／包摂的＝民主主義 or 共和制／戦時＝独裁政治）へ飛ぶ。②そうでなければ
        /// <see cref="AnacyclosisRules.NextForm"/> の循環方向（腐落/打倒）を代表アーキタイプへ戻す。傾向は
        /// <see cref="TransitionPropensity"/>。閾値未満なら現状維持。並行循環を作らず既存ルールへ委譲。
        /// </summary>
        public static GovernanceTransition Recommend(GovernanceArchetype from, GovernanceFormPressures pr)
        {
            float propensity = TransitionPropensity(from, pr);
            if (propensity < DefaultTransitionThreshold)
                return new GovernanceTransition(from, propensity, false); // 現状維持

            // 危機遷移（非循環）：同意が崩れ格差/不安定が高い＝革命/独裁化の機。
            bool revolutionary = pr.popularConsent < 0.35f && (pr.inequality + pr.instability) * 0.5f > 0.55f;
            if (revolutionary)
            {
                GovernanceArchetype to;
                if (pr.war > 0.6f)
                    to = GovernanceArchetype.独裁政治;            // 戦時の権力集中＝個人独裁
                else if (pr.inequality > 0.6f && pr.popularConsent < 0.25f)
                    to = GovernanceArchetype.共産主義;            // 収奪への革命＝国有/党支配
                else
                    to = GovernanceFormCatalog.IsAutocratic(from)
                        ? GovernanceArchetype.共和制               // 専制への民主革命
                        : GovernanceArchetype.独裁政治;            // 民主の動揺＝独裁化
                if (to == from) return new GovernanceTransition(from, propensity, false);
                return new GovernanceTransition(to, propensity, false);
            }

            // 循環遷移：アナキュクローシスの次形態へ（腐落/打倒の輪）。核は委譲。
            RegimeForm nextRegime = AnacyclosisRules.NextForm(ToRegimeForm(from));
            GovernanceArchetype cyclicalTo = FromRegimeForm(nextRegime);
            if (cyclicalTo == from)
                return new GovernanceTransition(from, propensity, false);
            return new GovernanceTransition(cyclicalTo, propensity, true);
        }

        /// <summary>遷移が実際に起こるか（傾向が閾値以上で移行先が現状と異なる）。決定論判定。</summary>
        public static bool WouldTransition(GovernanceArchetype from, GovernanceFormPressures pr, float threshold)
        {
            GovernanceTransition t = Recommend(from, pr);
            return t.propensity >= Mathf.Clamp01(threshold) && t.to != from;
        }

        /// <summary>既定閾値版。</summary>
        public static bool WouldTransition(GovernanceArchetype from, GovernanceFormPressures pr)
            => WouldTransition(from, pr, DefaultTransitionThreshold);
    }
}
