using UnityEngine;

namespace Ginei
{
    /// <summary>最後の貸し手の調整値（マジックナンバー禁止＝集約・top-level）。</summary>
    public readonly struct LenderOfLastResortParams
    {
        public readonly float arrestThreshold;    // 必要量に対しこの割合を供給できれば取付けは沈静
        public readonly float penaltyPerSeverity;  // 危機の深さ1あたりの罰則金利上乗せ（健全銀行のみ借りに来る選別）
        public readonly float minHaircut;          // 優良担保でも掛ける最低限の割引（過信防止）
        public readonly float buildupRate;         // 救済常態化がモラルハザードを育てる速度
        public readonly float insolvencyQuality;   // 担保の質がこれ未満かつ過負債なら支払不能（救うべきでない）

        public LenderOfLastResortParams(float arrestThreshold, float penaltyPerSeverity, float minHaircut, float buildupRate, float insolvencyQuality)
        {
            this.arrestThreshold = Mathf.Clamp01(arrestThreshold);
            this.penaltyPerSeverity = Mathf.Max(0f, penaltyPerSeverity);
            this.minHaircut = Mathf.Clamp01(minHaircut);
            this.buildupRate = Mathf.Max(0f, buildupRate);
            this.insolvencyQuality = Mathf.Clamp01(insolvencyQuality);
        }

        /// <summary>既定＝供給/必要1.0で沈静・危機深さ1あたり罰則+5%・最低掛け目5%・蓄積速度0.5・担保質0.4未満で支払不能。</summary>
        public static LenderOfLastResortParams Default => new LenderOfLastResortParams(1.0f, 0.05f, 0.05f, 0.5f, 0.4f);
    }

    /// <summary>
    /// 最後の貸し手＝Bagehot原則の純ロジック（KNDB-2 #1613・キンドルバーガー『熱狂、恐慌、崩壊』＋バジョット『ロンバード街』）。
    /// 危機の取付けを止めるには「高い金利で・優良担保に対して・無制限に」貸す＝無制限の流動性供給が取付けを鎮める。
    /// だが救済が常態化すると「どうせ助かる」と無謀な賭けを誘う（<b>モラルハザード</b>）。罰則金利と優良担保がその毒消し。
    /// <see cref="BankRules"/>（信用創造・取付けリスク）とは別＝危機時の中央銀行の最後の貸し手機能。
    /// 防火壁・伝播遮断は <c>FinancialContagionRules</c>（同EPIC・Wave30）、国家財政（国債/金利）は <see cref="FiscalRules"/> が担う。
    /// 全入力クランプ・乱数なし決定論・基準値非破壊。test-first。
    /// </summary>
    public static class LenderOfLastResortRules
    {
        /// <summary>必要な緊急流動性 0..1＝取付けの深さ×非流動資産（流動化できない資産が多いほど現金が要る）。</summary>
        public static float LiquidityNeeded(float panicSeverity, float illiquidAssets)
            => Mathf.Clamp01(panicSeverity) * Mathf.Clamp01(illiquidAssets);

        /// <summary>取付けの鎮静効果 0..1＝供給/必要の充足率。閾値以上で沈静（=1）、不足なら供給比に応じ連鎖が残る。
        /// 無制限供給が取付けを止める核（必要量を満たせば沈静）。necessary=0なら危機なし＝完全沈静。</summary>
        public static float PanicArrest(float liquidityProvided, float liquidityNeeded, LenderOfLastResortParams p)
        {
            float need = Mathf.Max(0f, liquidityNeeded);
            float prov = Mathf.Max(0f, liquidityProvided);
            if (need <= 0f) return 1f; // 危機が無ければ完全沈静
            float ratio = prov / need;
            if (ratio >= p.arrestThreshold) return 1f; // 閾値を満たせば取付けは止まる
            return Mathf.Clamp01(ratio / Mathf.Max(0.0001f, p.arrestThreshold));
        }

        /// <summary>Bagehotの罰則金利＝基準＋危機の深さ×上乗せ（高金利＝健全な銀行しか借りに来ない自己選別）。</summary>
        public static float PenaltyRate(float baseRate, float stigmaWeight, float panicSeverity)
        {
            float b = Mathf.Max(0f, baseRate);
            float w = Mathf.Max(0f, stigmaWeight);
            return b + w * Mathf.Clamp01(panicSeverity);
        }

        /// <summary>優良担保ほど低い掛け目 0..1＝(1−質)＝劣悪担保を割り引く（救済の規律）。最低掛け目を下限に最良担保でも少し割る。</summary>
        public static float CollateralHaircut(float assetQuality, LenderOfLastResortParams p)
            => Mathf.Clamp(1f - Mathf.Clamp01(assetQuality), p.minHaircut, 1f);

        /// <summary>モラルハザードの蓄積＝救済の常態化×リスク選好を時間積分（「どうせ助かる」が無謀な賭けを育てる）。0..1。</summary>
        public static float MoralHazardBuildup(float bailoutFrequency, float riskAppetite, float dt, LenderOfLastResortParams p)
        {
            if (dt <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(bailoutFrequency) * Mathf.Clamp01(riskAppetite) * p.buildupRate * dt);
        }

        /// <summary>モラルハザード抑制 0..1＝罰則金利と優良担保が「どうせ助かる」を毒消し（高金利×良担保ほど規律が効く）。</summary>
        public static float MoralHazardMitigation(float penaltyRate, float collateralQuality)
        {
            // 罰則金利は青天井なので飽和させて 0..1 に写す。
            float rateEffect = Mathf.Clamp01(Mathf.Max(0f, penaltyRate) / (Mathf.Max(0f, penaltyRate) + 0.1f));
            float collat = Mathf.Clamp01(collateralQuality);
            // 罰則金利と優良担保の合成（どちらも欠けると抑制0＝積で表す）。
            return Mathf.Clamp01(rateEffect * collat);
        }

        /// <summary>流動性不足（救うべき＝true）か支払不能（救うべきでない＝false）かの弁別。
        /// 担保の質が閾値以上、または負債が控えめなら「流動性不足」＝Bagehot救済対象。
        /// 担保が劣悪かつ過負債なら「支払不能（ゾンビ）」＝救済は損失の付け替えにすぎない。</summary>
        public static bool IlliquidVsInsolvent(float assetQuality, float liabilities, LenderOfLastResortParams p)
        {
            float q = Mathf.Clamp01(assetQuality);
            float liab = Mathf.Max(0f, liabilities);
            // 担保の質が負債を上回って覆えるなら流動性不足（救える）。
            return q >= liab || q >= p.insolvencyQuality;
        }

        /// <summary>Bagehotの三原則充足＝高金利×優良担保（=高い掛け目を課す規律）×無制限供給。
        /// penaltyRate&gt;baseRate（罰則あり）、collateralHaircut&gt;0（担保を割り引く）、unlimited（無制限）の全充足。</summary>
        public static bool IsBagehotCompliant(float penaltyRate, float baseRate, float collateralHaircut, bool unlimited)
            => penaltyRate > baseRate && collateralHaircut > 0f && unlimited;
    }
}
