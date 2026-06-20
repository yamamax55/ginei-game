using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政体形態（#117 政体アーキタイプの統一型）。首長制（出発点・未分化）から、民主主義（立憲君主制/共和制）か
    /// 独裁主義（共産主義/指導者独裁）へ分岐進化する。形態は<b>既存の軸（君主有無×立憲×選挙×所有×軍政）の合成ビュー</b>。
    /// </summary>
    public enum GovernmentForm { 首長制, 君主制, 立憲君主制, 共和制, 共産主義, 指導者独裁 }

    /// <summary>政体を定義する軸の束（形態＝この合成）。<see cref="GovernmentFormRules.Axes"/> が形態の既定値を返す。</summary>
    public readonly struct GovernmentAxes
    {
        public readonly bool sovereign;        // 君主/個人元首が居るか
        public readonly bool constitutional;   // 立憲（法の支配・権利保護が高い）か
        public readonly bool elections;        // 選挙/議会があるか
        public readonly Ownership ownership;   // 私有/国有（共産の標識）
        public readonly CivilianControlType control; // 軍政の型（誰が軍を握る）

        public GovernmentAxes(bool sovereign, bool constitutional, bool elections, Ownership ownership, CivilianControlType control)
        {
            this.sovereign = sovereign;
            this.constitutional = constitutional;
            this.elections = elections;
            this.ownership = ownership;
            this.control = control;
        }
    }

    /// <summary>政体遷移の発火条件に使う社会シグナル（すべて 0..1）。</summary>
    public readonly struct RegimeSignals
    {
        public readonly float legitimacy;    // 正統性（Regime.legitimacy）
        public readonly float corruption;    // 腐敗（Regime.corruption）
        public readonly float consent;       // 合意/協力（Polity.cooperation）
        public readonly float hope;          // 希望/民心（Community.hope）
        public readonly float inclusiveness; // 包摂度（FactionState.inclusiveness）

        public RegimeSignals(float legitimacy, float corruption, float consent, float hope, float inclusiveness)
        {
            this.legitimacy = Mathf.Clamp01(legitimacy);
            this.corruption = Mathf.Clamp01(corruption);
            this.consent = Mathf.Clamp01(consent);
            this.hope = Mathf.Clamp01(hope);
            this.inclusiveness = Mathf.Clamp01(inclusiveness);
        }
    }

    /// <summary>
    /// 政体形態と進化の純ロジック（#117・test-first・唯一の窓口）。形態を軸の合成で<b>分類</b>し（<see cref="Classify"/>）、
    /// 形態の既定軸を<b>導出</b>し（<see cref="Axes"/>＝既存の軍政`CivilianControlType`/所有`Ownership`へ橋渡し）、
    /// 形態間の<b>合法な遷移</b>（<see cref="CanTransition"/>）と<b>発火条件</b>（<see cref="TransitionTrigger"/>）で
    /// 「首長制→民主/独裁→下位形態」を進める。形態は <see cref="FactionState.governmentForm"/> に保持。状態は変えない（<see cref="Apply"/> 以外）。
    /// </summary>
    public static class GovernmentFormRules
    {
        /// <summary>形態の既定軸（君主/立憲/選挙/所有/軍政）。形態↔軸の単一の対応表。</summary>
        public static GovernmentAxes Axes(GovernmentForm form)
        {
            switch (form)
            {
                case GovernmentForm.首長制:     return new GovernmentAxes(true, false, false, Ownership.私有, CivilianControlType.未分化);
                case GovernmentForm.君主制:     return new GovernmentAxes(true, false, false, Ownership.私有, CivilianControlType.君主統帥);
                case GovernmentForm.立憲君主制: return new GovernmentAxes(true, true, true, Ownership.私有, CivilianControlType.文民統制);
                case GovernmentForm.共和制:     return new GovernmentAxes(false, true, true, Ownership.私有, CivilianControlType.文民統制);
                case GovernmentForm.共産主義:   return new GovernmentAxes(false, false, false, Ownership.国有, CivilianControlType.党軍);
                case GovernmentForm.指導者独裁: return new GovernmentAxes(true, false, false, Ownership.私有, CivilianControlType.軍部優位);
                default:                        return new GovernmentAxes(true, false, false, Ownership.私有, CivilianControlType.未分化);
            }
        }

        /// <summary>形態→軍政の型（既存 <see cref="CivilianControlRules"/> への橋渡し＝形態が任免/クーデターリスクに効く起点）。</summary>
        public static CivilianControlType ControlTypeOf(GovernmentForm form) => Axes(form).control;

        /// <summary>形態→所有（既存 <see cref="PropertyRules"/> への橋渡し＝共産は国有＝利潤が国庫へ）。</summary>
        public static Ownership OwnershipOf(GovernmentForm form) => Axes(form).ownership;

        /// <summary>民主主義か（立憲＋選挙）。立憲君主制/共和制。</summary>
        public static bool IsDemocratic(GovernmentForm form) => form == GovernmentForm.立憲君主制 || form == GovernmentForm.共和制;

        /// <summary>独裁主義か（共産/指導者独裁）。</summary>
        public static bool IsAutocratic(GovernmentForm form) => form == GovernmentForm.共産主義 || form == GovernmentForm.指導者独裁;

        /// <summary>
        /// 形態→軍人事ドクトリン（#MILEDU-SWORD への橋渡し）。<b>民主主義（立憲君主制/共和制）は実力主義</b>＝開かれた社会は
        /// merit で出世／<b>それ以外（首長制/君主制/共産主義/指導者独裁）は学閥主義</b>＝credential/閥が人事を握る。
        /// </summary>
        public static PromotionDoctrine PromotionDoctrineOf(GovernmentForm form)
            => IsDemocratic(form) ? PromotionDoctrine.実力主義 : PromotionDoctrine.学閥主義;

        /// <summary>軸の束→形態の分類（合成ビュー）。国有＝共産、立憲＋選挙＝民主（君主有無で立憲君主/共和）、ほかは軍政型で分ける。</summary>
        public static GovernmentForm Classify(bool sovereign, bool constitutional, bool elections, Ownership ownership, CivilianControlType control)
        {
            if (ownership == Ownership.国有 || control == CivilianControlType.党軍) return GovernmentForm.共産主義;
            if (constitutional && elections) return sovereign ? GovernmentForm.立憲君主制 : GovernmentForm.共和制;
            if (control == CivilianControlType.未分化) return GovernmentForm.首長制;
            if (control == CivilianControlType.軍部優位) return GovernmentForm.指導者独裁;
            return sovereign ? GovernmentForm.君主制 : GovernmentForm.指導者独裁;
        }

        public static GovernmentForm Classify(GovernmentAxes a)
            => Classify(a.sovereign, a.constitutional, a.elections, a.ownership, a.control);

        /// <summary>from から to への遷移が合法か（進化グラフ）。同形態は false。</summary>
        public static bool CanTransition(GovernmentForm from, GovernmentForm to)
        {
            if (from == to) return false;
            switch (from)
            {
                case GovernmentForm.首長制:     return to == GovernmentForm.君主制 || to == GovernmentForm.指導者独裁;
                case GovernmentForm.君主制:     return to == GovernmentForm.立憲君主制 || to == GovernmentForm.指導者独裁 || to == GovernmentForm.共産主義;
                case GovernmentForm.立憲君主制: return to == GovernmentForm.共和制 || to == GovernmentForm.指導者独裁;
                case GovernmentForm.共和制:     return to == GovernmentForm.指導者独裁 || to == GovernmentForm.共産主義;
                case GovernmentForm.指導者独裁: return to == GovernmentForm.君主制 || to == GovernmentForm.共産主義 || to == GovernmentForm.共和制;
                case GovernmentForm.共産主義:   return to == GovernmentForm.指導者独裁 || to == GovernmentForm.共和制;
                default: return false;
            }
        }

        /// <summary>
        /// from→to の遷移条件が社会シグナルで満たされるか（合法かつ条件成立で true）。
        /// 共産＝収奪×絶望×正統性喪失（革命）／指導者独裁＝合意崩壊×腐敗／立憲君主制＝有徳×包摂（立憲化）／
        /// 共和制＝高希望×（立憲君主からは包摂・崩壊後からは合意）（君主廃止/民主化）／君主制＝正統性確立（世襲化）。
        /// </summary>
        public static bool TransitionTrigger(GovernmentForm from, GovernmentForm to, RegimeSignals s)
        {
            if (!CanTransition(from, to)) return false;
            switch (to)
            {
                case GovernmentForm.共産主義:   return s.legitimacy < 0.3f && s.hope < 0.35f && s.inclusiveness < 0.4f;
                case GovernmentForm.指導者独裁: return s.consent < 0.4f && s.corruption > 0.5f;
                case GovernmentForm.立憲君主制: return s.legitimacy > 0.5f && s.inclusiveness > 0.6f;
                case GovernmentForm.共和制:
                    return from == GovernmentForm.立憲君主制
                        ? (s.hope > 0.6f && s.inclusiveness > 0.7f)
                        : (s.hope > 0.6f && s.consent > 0.6f);
                case GovernmentForm.君主制:     return s.legitimacy > 0.5f;
                default: return false;
            }
        }

        // 遷移の優先順（危機が先＝革命/独裁化を民主化より優先＝決定論）。
        private static readonly GovernmentForm[] Priority =
        {
            GovernmentForm.共産主義, GovernmentForm.指導者独裁, GovernmentForm.立憲君主制, GovernmentForm.共和制, GovernmentForm.君主制
        };

        /// <summary>その年に進む先の形態（合法かつ条件成立の最優先。無ければ現状維持＝<paramref name="from"/>）。</summary>
        public static GovernmentForm NextForm(GovernmentForm from, RegimeSignals s)
        {
            for (int i = 0; i < Priority.Length; i++)
            {
                GovernmentForm to = Priority[i];
                if (CanTransition(from, to) && TransitionTrigger(from, to, s)) return to;
            }
            return from;
        }

        /// <summary>形態を勢力へ適用する（唯一の状態変更窓口）。形態は <see cref="FactionState.governmentForm"/> に保持。</summary>
        public static void Apply(FactionState s, GovernmentForm form)
        {
            if (s == null) return;
            s.governmentForm = form;
        }

        /// <summary>勢力の社会状態から遷移シグナルを組む（null 安全）。</summary>
        public static RegimeSignals SignalsOf(FactionState s)
        {
            if (s == null) return new RegimeSignals(1f, 0f, 1f, 1f, 0.5f);
            float legitimacy = s.regime != null ? s.regime.legitimacy : 1f;
            float corruption = s.regime != null ? s.regime.corruption : 0f;
            float consent = s.polity != null ? s.polity.cooperation : 1f;
            float hope = s.community != null ? s.community.hope : 1f;
            return new RegimeSignals(legitimacy, corruption, consent, hope, s.inclusiveness);
        }
    }
}
