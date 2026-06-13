using UnityEngine;

namespace Ginei
{
    /// <summary>クーデター解決の入力（軍政の型・統制の強さ・支持・敗戦直後・包摂度）。</summary>
    public readonly struct CoupContext
    {
        public readonly CivilianControlType control;
        public readonly float controlStrength; // 統制の強さ＝軍の忠誠の代理（0..1・高いほど起きにくい）
        public readonly float support;         // 政治的支持/民心（0..1）
        public readonly bool recentDefeat;     // 敗戦直後はリスク上昇
        public readonly float inclusiveness;   // 収奪0↔包摂1（革命後の形態を分ける）

        public CoupContext(CivilianControlType control, float controlStrength, float support, bool recentDefeat, float inclusiveness)
        {
            this.control = control;
            this.controlStrength = Mathf.Clamp01(controlStrength);
            this.support = Mathf.Clamp01(support);
            this.recentDefeat = recentDefeat;
            this.inclusiveness = Mathf.Clamp01(inclusiveness);
        }
    }

    /// <summary>政変の帰結（発火有無・型・結果・形態変化・事後正統性）。<paramref name="newLegitimacy"/> は発火時のみ有効（未発火は-1）。</summary>
    public readonly struct UpheavalResult
    {
        public readonly bool attempted;
        public readonly CoupType type;
        public readonly CoupOutcome outcome;
        public readonly bool formChanged;
        public readonly GovernmentForm newForm;
        public readonly float newLegitimacy;

        public UpheavalResult(bool attempted, CoupType type, CoupOutcome outcome, bool formChanged, GovernmentForm newForm, float newLegitimacy)
        {
            this.attempted = attempted;
            this.type = type;
            this.outcome = outcome;
            this.formChanged = formChanged;
            this.newForm = newForm;
            this.newLegitimacy = newLegitimacy;
        }
    }

    /// <summary>
    /// 政体駆動の純ロジック（C1 Tier A・軍政#145→政変#215→政体#117 を束ねる唯一の窓口・test-first）。
    /// 軍政の型（<see cref="GovernmentFormRules.ControlTypeOf"/>）と統制/支持から <see cref="CivilianControlRules.WouldCoup"/> で
    /// クーデターの発火を、<see cref="CoupRules"/> で帰結を解き、<b>成功すれば政体形態を転換</b>する
    /// （軍部→指導者独裁／革命→収奪的なら共産主義・包摂的なら共和制／宮廷→支配者交代で形態不変）。
    /// ＝「腐敗した不安定な政体は政変で別形態へ変わる」を既存窓口の合成で実現（並行システムを作らない）。
    /// </summary>
    public static class PoliticalUpheavalRules
    {
        /// <summary>勢力状態から政変コンテキストを組む（統制の強さ＝正統性・合意・低腐敗の合成）。null 安全。</summary>
        public static CoupContext ContextOf(FactionState s)
        {
            if (s == null) return new CoupContext(CivilianControlType.未分化, 1f, 1f, false, 0.5f);
            CivilianControlType control = GovernmentFormRules.ControlTypeOf(s.governmentForm);
            float legitimacy = s.regime != null ? s.regime.legitimacy : 1f;
            float corruption = s.regime != null ? s.regime.corruption : 0f;
            float consent = s.polity != null ? s.polity.cooperation : 1f;
            float hope = s.community != null ? s.community.hope : 1f;
            float controlStrength = Mathf.Clamp01((legitimacy + consent + (1f - corruption)) / 3f);
            return new CoupContext(control, controlStrength, hope, false, s.inclusiveness);
        }

        /// <summary>政変の主体を選ぶ：支持崩壊＝革命／軍部優位・未分化＝軍部／ほか＝宮廷。</summary>
        public static CoupType ChooseCoupType(CoupContext c)
        {
            if (c.support < 0.3f) return CoupType.革命;
            if (c.control == CivilianControlType.軍部優位 || c.control == CivilianControlType.未分化) return CoupType.軍部;
            return CoupType.宮廷;
        }

        /// <summary>クーデター成功後の政体形態：軍部→指導者独裁／革命→収奪は共産主義・包摂は共和制／宮廷→形態不変（支配者交代のみ）。</summary>
        public static GovernmentForm FormAfterCoup(GovernmentForm from, CoupType type, float inclusiveness)
        {
            switch (type)
            {
                case CoupType.軍部: return GovernmentForm.指導者独裁;
                case CoupType.革命: return inclusiveness < 0.4f ? GovernmentForm.共産主義 : GovernmentForm.共和制;
                default:           return from; // 宮廷＝閣僚/側近の政変＝支配者は替わるが政体は不変
            }
        }

        /// <summary>政変を解決する（発火→主体→成功確率→帰結→形態転換）。roll(0..1) は呼び出し側が渡す＝決定論。</summary>
        public static UpheavalResult ResolveUpheaval(GovernmentForm from, CoupContext c, float roll,
            CivilianControlRules.ControlParams ctrlPrm, CoupRules.CoupParams coupPrm)
        {
            bool attempt = CivilianControlRules.WouldCoup(c.control, c.controlStrength, c.support, c.recentDefeat, ctrlPrm);
            if (!attempt) return new UpheavalResult(false, CoupType.軍部, CoupOutcome.粛清, false, from, -1f);

            CoupType type = ChooseCoupType(c);
            float chance = CoupRules.CoupSuccessChance(c.controlStrength, c.support, type, coupPrm); // 統制=軍の忠誠の代理
            CoupOutcome outcome = CoupRules.Resolve(chance, roll, coupPrm);
            GovernmentForm newForm = outcome == CoupOutcome.成功 ? FormAfterCoup(from, type, c.inclusiveness) : from;
            float newLegitimacy = CoupRules.PostCoupLegitimacy(outcome, c.support, coupPrm);
            return new UpheavalResult(true, type, outcome, newForm != from, newForm, newLegitimacy);
        }

        /// <summary>既定パラメータ版。</summary>
        public static UpheavalResult ResolveUpheaval(GovernmentForm from, CoupContext c, float roll)
            => ResolveUpheaval(from, c, roll, CivilianControlRules.ControlParams.Default, CoupRules.CoupParams.Default);
    }
}
