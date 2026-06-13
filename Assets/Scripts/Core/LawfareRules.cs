using UnityEngine;

namespace Ginei
{
    /// <summary>法律戦（lawfare）の調整値（#1380・限定戦争ULW-2）。マジックナンバー禁止＝ここに集約。</summary>
    public readonly struct LawfareParams
    {
        /// <summary>法的専門性1.0・規範的高み1.0あたりの法的梃子の最大幅（法に通じ大義名分があるほど強い）。</summary>
        public readonly float leverageScale;
        /// <summary>梃子が「行動空間の収縮」に効く重み（相手の手足を法で縛る効き）。</summary>
        public readonly float constrictionWeight;
        /// <summary>条約の曖昧さ1.0あたりの解釈優位の最大幅（曖昧な条項ほど自国有利に読める余地が大きい）。</summary>
        public readonly float interpretationScale;
        /// <summary>相手の行動を非正当化する効きの最大幅（国際法違反として印象づける）。</summary>
        public readonly float delegitimizeScale;
        /// <summary>法の濫用1.0・偽善1.0あたりの逆効果（ダブルスタンダードが自国の信用を蝕む諸刃）。</summary>
        public readonly float backlashScale;
        /// <summary>偽善（ダブルスタンダード）が逆効果を非線形に増幅する指数。</summary>
        public readonly float hypocrisyExponent;
        /// <summary>規範的高み1.0・法律戦技能1.0あたりの第三国支持の最大幅（合法性が味方を呼ぶ）。</summary>
        public readonly float supportScale;

        public LawfareParams(float leverageScale, float constrictionWeight, float interpretationScale,
            float delegitimizeScale, float backlashScale, float hypocrisyExponent, float supportScale)
        {
            this.leverageScale = Mathf.Max(0f, leverageScale);
            this.constrictionWeight = Mathf.Max(0f, constrictionWeight);
            this.interpretationScale = Mathf.Max(0f, interpretationScale);
            this.delegitimizeScale = Mathf.Max(0f, delegitimizeScale);
            this.backlashScale = Mathf.Max(0f, backlashScale);
            this.hypocrisyExponent = Mathf.Max(1f, hypocrisyExponent);
            this.supportScale = Mathf.Max(0f, supportScale);
        }

        /// <summary>既定＝梃子1.0/収縮重み0.9/解釈0.8/非正当化0.85/逆効果0.7・偽善指数2/支持0.8。</summary>
        public static LawfareParams Default => new LawfareParams(
            1.0f, 0.9f, 0.8f,
            0.85f, 0.7f, 2f, 0.8f);
    }

    /// <summary>
    /// 法律戦（lawfare＝law+warfare）の純ロジック（#1380・限定戦争ULW-2）。
    /// 条約・国際法・規範を<b>攻撃的な武器</b>として使い、相手の行動空間を法的に収縮させる：
    /// 正当な軍事行動を違法に見せかけ手足を縛り(<see cref="ActionSpaceConstriction"/>)、
    /// 曖昧な条項を自国有利に解釈し(<see cref="TreatyInterpretationAdvantage"/>)、
    /// 相手の行動を国際法違反として非正当化する(<see cref="DelegitimizeOpponent"/>)。
    /// だが恣意的に使いすぎる＝ダブルスタンダードは逆に自国の信用を失う(<see cref="LawfareBacklash"/>)＝法を武器にすることの諸刃。
    /// <b>分担</b>：<see cref="DiplomacyRules"/>＝外交状態の遷移（締結/破棄/宣戦/講和）、
    /// <see cref="TreatyRules"/>＝条約の効果（opinion 修正子・レバレッジ・違約判定）、
    /// <see cref="WarGoalRules"/>＝戦争目標と casus belli（戦争の正当事由）、
    /// 同EPIC ULW の <c>GreyZoneRules</c>＝グレーゾーン（戦争未満の威圧）。
    /// ここは「法そのものを攻撃的に振るって相手の行動空間を縛る」を扱う＝外交状態でも条約効果でもなく lawfare。
    /// 調整値は <see cref="LawfareParams"/> に集約（マジックナンバー禁止）。乱数なし決定論。test-first。
    /// </summary>
    public static class LawfareRules
    {
        /// <summary>
        /// 法的梃子＝法的専門性×規範的な高み（0..1）。
        /// 法に通じ（専門性）かつ大義名分（規範的高み）があるほど、法を武器として強く振るえる。
        /// どちらか欠ければ梃子は痩せる（積＝両方が要る）。
        /// </summary>
        public static float LegalLeverage(float legalExpertise, float normativeHighGround, LawfareParams p)
        {
            float ex = Mathf.Clamp01(legalExpertise);
            float hg = Mathf.Clamp01(normativeHighGround);
            return Mathf.Clamp01(ex * hg * p.leverageScale);
        }

        /// <summary>既定 Params で法的梃子を返す簡易窓口。</summary>
        public static float LegalLeverage(float legalExpertise, float normativeHighGround)
            => LegalLeverage(legalExpertise, normativeHighGround, LawfareParams.Default);

        /// <summary>
        /// 行動空間の収縮＝法的梃子×相手の脆弱性（0..1）。
        /// 相手の正当な軍事行動を違法に見せかけ、法的に縛って手足を奪う。
        /// 脆弱性が高い（法的に付け入る隙がある）ほど深く収縮させられる。
        /// </summary>
        public static float ActionSpaceConstriction(float legalLeverage, float targetVulnerability, LawfareParams p)
        {
            float lev = Mathf.Clamp01(legalLeverage);
            float vul = Mathf.Clamp01(targetVulnerability);
            return Mathf.Clamp01(lev * vul * p.constrictionWeight);
        }

        /// <summary>既定 Params で行動空間の収縮を返す簡易窓口。</summary>
        public static float ActionSpaceConstriction(float legalLeverage, float targetVulnerability)
            => ActionSpaceConstriction(legalLeverage, targetVulnerability, LawfareParams.Default);

        /// <summary>
        /// 条約解釈の優位＝条項の曖昧さ×自国の法務能力（0..1）。
        /// 曖昧な条項を自国に有利に読み、解釈で相手を縛る（明文に書いていない義務を相手に負わせる）。
        /// 曖昧さが大きく自国の法務が強いほど、解釈戦で取れる余地が広い。
        /// </summary>
        public static float TreatyInterpretationAdvantage(float ambiguity, float ownLegalCapacity, LawfareParams p)
        {
            float amb = Mathf.Clamp01(ambiguity);
            float cap = Mathf.Clamp01(ownLegalCapacity);
            return Mathf.Clamp01(amb * cap * p.interpretationScale);
        }

        /// <summary>既定 Params で条約解釈の優位を返す簡易窓口。</summary>
        public static float TreatyInterpretationAdvantage(float ambiguity, float ownLegalCapacity)
            => TreatyInterpretationAdvantage(ambiguity, ownLegalCapacity, LawfareParams.Default);

        /// <summary>
        /// 相手の非正当化＝相手の行動（の問題性）×法的フレーミング技能（0..1）。
        /// 相手の行動を国際法違反として描き出し、世論・第三国を味方につける。
        /// 相手の行動が問題含み（付け入る材料がある）で、それを法的に切り取る技能が高いほど効く。
        /// </summary>
        public static float DelegitimizeOpponent(float opponentAction, float legalFramingSkill, LawfareParams p)
        {
            float act = Mathf.Clamp01(opponentAction);
            float skill = Mathf.Clamp01(legalFramingSkill);
            return Mathf.Clamp01(act * skill * p.delegitimizeScale);
        }

        /// <summary>既定 Params で相手の非正当化を返す簡易窓口。</summary>
        public static float DelegitimizeOpponent(float opponentAction, float legalFramingSkill)
            => DelegitimizeOpponent(opponentAction, legalFramingSkill, LawfareParams.Default);

        /// <summary>
        /// 法律戦の逆効果＝法の濫用×偽善（ダブルスタンダード、非線形に増幅）（0..1）。
        /// 法を恣意的に使いすぎ、自分には甘く相手には厳しいダブルスタンダードが露わになると、
        /// 「法を口実にしているだけ」と見抜かれて逆に自国の信用を失う＝法を武器にすることの諸刃。
        /// 偽善は <see cref="LawfareParams.hypocrisyExponent"/> 乗で効き、僅かな偽善でも積み重なると効く。
        /// </summary>
        public static float LawfareBacklash(float overreach, float hypocrisy, LawfareParams p)
        {
            float over = Mathf.Clamp01(overreach);
            float hyp = Mathf.Clamp01(hypocrisy);
            float hypAmplified = Mathf.Pow(hyp, p.hypocrisyExponent);
            return Mathf.Clamp01(over * hypAmplified * p.backlashScale);
        }

        /// <summary>既定 Params で法律戦の逆効果を返す簡易窓口。</summary>
        public static float LawfareBacklash(float overreach, float hypocrisy)
            => LawfareBacklash(overreach, hypocrisy, LawfareParams.Default);

        /// <summary>
        /// 国際支持＝規範的な高み×法律戦の技能（0..1）。
        /// 自国の法的な正しさ（規範的高み）を巧みに示すことで第三国の支持を取りつける＝合法性が味方を呼ぶ。
        /// 大義名分があっても示し方が拙ければ支持は集まらない（積＝両方が要る）。
        /// </summary>
        public static float InternationalSupport(float normativeHighGround, float lawfareSkill, LawfareParams p)
        {
            float hg = Mathf.Clamp01(normativeHighGround);
            float skill = Mathf.Clamp01(lawfareSkill);
            return Mathf.Clamp01(hg * skill * p.supportScale);
        }

        /// <summary>既定 Params で国際支持を返す簡易窓口。</summary>
        public static float InternationalSupport(float normativeHighGround, float lawfareSkill)
            => InternationalSupport(normativeHighGround, lawfareSkill, LawfareParams.Default);

        /// <summary>
        /// 非対称の法律戦＝弱者の法務技能×強者の法的露出（0..1）。
        /// 軍事的に劣る側が、法を盾にして強者の行動を縛る＝軍事弱者の武器（非対称戦）。
        /// 強者が露出（縛れる法的な隙）を抱え、弱者がそれを突く法務技能を持つほど、力の差を法で埋められる。
        /// </summary>
        public static float AsymmetricLawfare(float weakPartyLegalSkill, float strongPartyExposure, LawfareParams p)
        {
            float skill = Mathf.Clamp01(weakPartyLegalSkill);
            float exp = Mathf.Clamp01(strongPartyExposure);
            // 弱者の技能×強者の露出に、法的梃子と同じ収縮重みを掛けて行動空間を縛る。
            return Mathf.Clamp01(skill * exp * p.constrictionWeight);
        }

        /// <summary>既定 Params で非対称の法律戦を返す簡易窓口。</summary>
        public static float AsymmetricLawfare(float weakPartyLegalSkill, float strongPartyExposure)
            => AsymmetricLawfare(weakPartyLegalSkill, strongPartyExposure, LawfareParams.Default);

        /// <summary>
        /// 法律戦で相手の行動空間を支配したか＝行動空間の収縮が閾値以上。
        /// 法を武器に相手を縛り切り、物理的戦闘に至らずとも法廷・規範の場で優位を確定した状態。
        /// </summary>
        public static bool IsLawfareDominant(float actionSpaceConstriction, float threshold)
        {
            return Mathf.Clamp01(actionSpaceConstriction) >= threshold;
        }
    }
}
