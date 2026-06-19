using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政体アーキタイプ（#政体・ユーザー要望の7形態）。既存の <see cref="GovernmentForm"/> 列挙には無い形態
    /// （民主主義/独裁政治/寡頭政治）も<b>分類キーとして独立に持つ</b>（enum は変えない・既存窓口と橋渡し）。
    /// 民主主義＝直接/代表の多数支配・君主制＝世襲一人支配・立憲君主制＝立憲下の象徴君主・共産主義＝党/国有・
    /// 独裁政治＝個人独裁（軍部/指導者）・寡頭政治＝少数有力者支配・共和制＝立憲共和の代表制。
    /// </summary>
    public enum GovernanceArchetype
    {
        民主主義,
        君主制,
        立憲君主制,
        共産主義,
        独裁政治,
        寡頭政治,
        共和制
    }

    /// <summary>
    /// 政体の差別化特性（すべて 0..1・大きいほど強い）。各形態を「数値の1行」で性格づける単一のデータ表
    /// （<see cref="GovernanceFormCatalog.Profile"/> が返す）。値は <see cref="GovernanceFormEffectRules"/> が
    /// 既存窓口（合意/統治/軍政）へ橋渡しして実効値にする。realism は粒度でなく<b>形態ごとの差異</b>で出す。
    /// </summary>
    public readonly struct GovernanceFormProfile
    {
        /// <summary>正統性の源の強さ（高＝合意/世襲/法など確立した正統性を持ち、低＝力ずくで脆い）。</summary>
        public readonly float legitimacySource;
        /// <summary>意思決定の速さ（高＝一人/少数が即断・低＝合議や選挙で遅い）。</summary>
        public readonly float decisionSpeed;
        /// <summary>腐敗への耐性（高＝制度的歯止めが効き・低＝私物化されやすい）。</summary>
        public readonly float corruptionResistance;
        /// <summary>継承の安定（高＝平和な権力移譲・低＝後継危機で揺れる）。</summary>
        public readonly float successionStability;
        /// <summary>経済の集権度（高＝国有/統制経済・低＝私有/市場）。共産が最高。</summary>
        public readonly float economicCentralization;
        /// <summary>抑圧能力（高＝反対派を力で抑え込める・低＝弾圧手段が弱い）。</summary>
        public readonly float repressionCapacity;
        /// <summary>民衆同意への依存（高＝支持を失うと崩れる・低＝同意なしでも保つ）。</summary>
        public readonly float popularConsentDependence;

        public GovernanceFormProfile(float legitimacySource, float decisionSpeed, float corruptionResistance,
            float successionStability, float economicCentralization, float repressionCapacity,
            float popularConsentDependence)
        {
            this.legitimacySource = Mathf.Clamp01(legitimacySource);
            this.decisionSpeed = Mathf.Clamp01(decisionSpeed);
            this.corruptionResistance = Mathf.Clamp01(corruptionResistance);
            this.successionStability = Mathf.Clamp01(successionStability);
            this.economicCentralization = Mathf.Clamp01(economicCentralization);
            this.repressionCapacity = Mathf.Clamp01(repressionCapacity);
            this.popularConsentDependence = Mathf.Clamp01(popularConsentDependence);
        }
    }

    /// <summary>
    /// 政体カタログ（#政体・唯一の窓口・test-first）。7つの政体アーキタイプを<b>差別化された特性の1行</b>へ写し
    /// （<see cref="Profile"/>）、既存 <see cref="GovernmentForm"/> 列挙との相互変換（<see cref="ToGovernmentForm"/>／
    /// <see cref="FromGovernmentForm"/>）で並行システムを作らずに繋ぐ。形態が enum に無いとき（民主主義/独裁政治/
    /// 寡頭政治）は本カタログのキーで分類し、効果と遷移は既存ルールへ委譲する。乱数なし・null/範囲安全。
    /// </summary>
    public static class GovernanceFormCatalog
    {
        // 形態→特性の単一対応表（差別化の核＝ここが「各形態を最良にする」チューニング行）。
        private static GovernanceFormProfile Build(GovernanceArchetype a)
        {
            switch (a)
            {
                //                                       正統 決速 腐敗耐 継承 経済集 抑圧 同意依
                case GovernanceArchetype.民主主義:   return new GovernanceFormProfile(0.85f, 0.35f, 0.70f, 0.85f, 0.25f, 0.20f, 0.95f);
                case GovernanceArchetype.君主制:     return new GovernanceFormProfile(0.70f, 0.75f, 0.45f, 0.55f, 0.40f, 0.65f, 0.45f);
                case GovernanceArchetype.立憲君主制: return new GovernanceFormProfile(0.90f, 0.50f, 0.75f, 0.90f, 0.30f, 0.30f, 0.80f);
                case GovernanceArchetype.共産主義:   return new GovernanceFormProfile(0.55f, 0.70f, 0.35f, 0.45f, 0.95f, 0.90f, 0.40f);
                case GovernanceArchetype.独裁政治:   return new GovernanceFormProfile(0.30f, 0.90f, 0.25f, 0.30f, 0.55f, 0.95f, 0.30f);
                case GovernanceArchetype.寡頭政治:   return new GovernanceFormProfile(0.50f, 0.55f, 0.30f, 0.60f, 0.45f, 0.55f, 0.50f);
                case GovernanceArchetype.共和制:     return new GovernanceFormProfile(0.80f, 0.45f, 0.65f, 0.80f, 0.30f, 0.25f, 0.90f);
                default:                             return new GovernanceFormProfile(0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f);
            }
        }

        /// <summary>アーキタイプの特性を返す（未知値はニュートラルな 0.5 行へフォールバック）。</summary>
        public static GovernanceFormProfile Profile(GovernanceArchetype archetype) => Build(archetype);

        /// <summary>既存 <see cref="GovernmentForm"/> から特性を引く（橋渡し＝<see cref="FromGovernmentForm"/> 経由）。</summary>
        public static GovernanceFormProfile Profile(GovernmentForm form) => Build(FromGovernmentForm(form));

        /// <summary>
        /// アーキタイプ→既存 <see cref="GovernmentForm"/>（軸の合成ビュー）。enum に無い形態は近い軸へ写す
        /// ＝民主主義→共和制／独裁政治→指導者独裁／寡頭政治→指導者独裁（少数私利支配を独裁系へ丸める）。
        /// </summary>
        public static GovernmentForm ToGovernmentForm(GovernanceArchetype a)
        {
            switch (a)
            {
                case GovernanceArchetype.民主主義:   return GovernmentForm.共和制;
                case GovernanceArchetype.君主制:     return GovernmentForm.君主制;
                case GovernanceArchetype.立憲君主制: return GovernmentForm.立憲君主制;
                case GovernanceArchetype.共産主義:   return GovernmentForm.共産主義;
                case GovernanceArchetype.独裁政治:   return GovernmentForm.指導者独裁;
                case GovernanceArchetype.寡頭政治:   return GovernmentForm.指導者独裁;
                case GovernanceArchetype.共和制:     return GovernmentForm.共和制;
                default:                             return GovernmentForm.首長制;
            }
        }

        /// <summary>既存 <see cref="GovernmentForm"/>→アーキタイプ（首長制は未分化＝独裁政治の素形へ丸める）。</summary>
        public static GovernanceArchetype FromGovernmentForm(GovernmentForm form)
        {
            switch (form)
            {
                case GovernmentForm.君主制:     return GovernanceArchetype.君主制;
                case GovernmentForm.立憲君主制: return GovernanceArchetype.立憲君主制;
                case GovernmentForm.共和制:     return GovernanceArchetype.共和制;
                case GovernmentForm.共産主義:   return GovernanceArchetype.共産主義;
                case GovernmentForm.指導者独裁: return GovernanceArchetype.独裁政治;
                case GovernmentForm.首長制:     return GovernanceArchetype.独裁政治; // 未分化＝個人支配の素形
                default:                        return GovernanceArchetype.独裁政治;
            }
        }

        /// <summary>名前→アーキタイプ（UI/セーブ・大文字小文字/空白に頑健・不明は false）。</summary>
        public static bool TryParse(string name, out GovernanceArchetype archetype)
        {
            archetype = GovernanceArchetype.独裁政治;
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.Trim();
            switch (n)
            {
                case "民主主義": archetype = GovernanceArchetype.民主主義; return true;
                case "君主制": archetype = GovernanceArchetype.君主制; return true;
                case "立憲君主制": archetype = GovernanceArchetype.立憲君主制; return true;
                case "共産主義": archetype = GovernanceArchetype.共産主義; return true;
                case "独裁政治": archetype = GovernanceArchetype.独裁政治; return true;
                case "寡頭政治": archetype = GovernanceArchetype.寡頭政治; return true;
                case "共和制": archetype = GovernanceArchetype.共和制; return true;
                default: return false;
            }
        }

        /// <summary>名前→アーキタイプ（不明は <paramref name="fallback"/>＝既定 独裁政治）。</summary>
        public static GovernanceArchetype ParseOrDefault(string name, GovernanceArchetype fallback = GovernanceArchetype.独裁政治)
            => TryParse(name, out var a) ? a : fallback;

        /// <summary>民主的か（民主主義/立憲君主制/共和制＝立憲＋選挙）。<see cref="GovernmentFormRules.IsDemocratic"/> と整合。</summary>
        public static bool IsDemocratic(GovernanceArchetype a)
            => a == GovernanceArchetype.民主主義 || a == GovernanceArchetype.立憲君主制 || a == GovernanceArchetype.共和制;

        /// <summary>専制的か（君主制/共産主義/独裁政治/寡頭政治＝少数/一人支配）。</summary>
        public static bool IsAutocratic(GovernanceArchetype a) => !IsDemocratic(a);

        /// <summary>全アーキタイプ（列挙の安定順・観測/UI 用）。</summary>
        public static GovernanceArchetype[] All => new[]
        {
            GovernanceArchetype.民主主義, GovernanceArchetype.君主制, GovernanceArchetype.立憲君主制,
            GovernanceArchetype.共産主義, GovernanceArchetype.独裁政治, GovernanceArchetype.寡頭政治,
            GovernanceArchetype.共和制
        };
    }
}
