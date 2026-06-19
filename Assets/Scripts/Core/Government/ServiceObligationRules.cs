using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 服役義務の社会契約の純ロジック（static・STSH-3 #2360）。徴募政策（<see cref="RecruitmentPolicy"/>）が
    /// <b>兵力供給（多）↔合意コスト（少）のトレードオフ</b>を生むループを確立する＝強制徴兵ほど兵は集まるが
    /// 社会の協力（<see cref="ConsentRules"/>）を削り、志願制ほど兵は細いが正統性が高い（『宇宙の戦士』の参政権モデル）。
    ///
    /// 接続先（既存窓口へ委譲し並行レジストリを作らない）：
    /// <list type="bullet">
    ///   <item><see cref="FleetPool"/>（#148/#884）＝徴募可能母数の出所（<see cref="RecruitablePool"/>）。</item>
    ///   <item><see cref="ConsentRules.ControlStrength"/>（#836）＝合意コストを引く統治合意の基準（<see cref="ConsentCost"/>）。</item>
    /// </list>
    /// 乱数なし・決定論・null/clamp 安全。additive のみ（既存窓口は後退させない）。test-first。
    /// </summary>
    public static class ServiceObligationRules
    {
        // --- 徴募政策ごとの兵力歩留まり倍率（強制の度合い＝供給。自発<選抜<全員で単調増加） ---
        /// <summary>志願制の兵力歩留まり（細い供給）。</summary>
        public const float VolunteerYield = 0.25f;
        /// <summary>選抜徴兵の兵力歩留まり（中庸）。</summary>
        public const float ConscriptionYield = 0.60f;
        /// <summary>全員徴兵の兵力歩留まり（最大供給）。</summary>
        public const float TotalConscriptionYield = 1.0f;

        // --- 徴募政策ごとの合意コスト倍率（強制の度合い＝合意の毀損。自発<選抜<全員で単調増加） ---
        /// <summary>志願制の合意コスト倍率（ほぼ無コスト）。</summary>
        public const float VolunteerConsentCost = 0.0f;
        /// <summary>選抜徴兵の合意コスト倍率（中庸）。</summary>
        public const float ConscriptionConsentCost = 0.15f;
        /// <summary>全員徴兵の合意コスト倍率（最大）。</summary>
        public const float TotalConscriptionConsentCost = 0.40f;

        // --- 徴募政策ごとの正統性ボーナス（自発服役のみ＝血の代価で体制の当事者になる） ---
        /// <summary>志願制の正統性ボーナス（自発服役は最強の正統性）。</summary>
        public const float VolunteerLegitimacy = 0.20f;
        /// <summary>選抜徴兵の正統性ボーナス（薄い）。</summary>
        public const float ConscriptionLegitimacy = 0.05f;
        /// <summary>全員徴兵の正統性ボーナス（無し＝強制は当事者意識を生まない）。</summary>
        public const float TotalConscriptionLegitimacy = 0.0f;

        /// <summary>服役年数1年あたりの供給逓増係数（服役が重いほど常備兵は増える）。</summary>
        public const float ServiceYearSupplyScale = 0.10f;
        /// <summary>服役年数1年あたりの合意コスト逓増係数（重い兵役ほど社会の負担も増す）。</summary>
        public const float ServiceYearCostScale = 0.05f;
        /// <summary>必要服役年数の基準（この年数を超えた分だけ逓増が効く）。</summary>
        public const float BaselineServiceYears = 2f;

        /// <summary>徴募政策の兵力歩留まり倍率（0..1＝供給の太さ。null/未知は志願制扱い）。</summary>
        public static float YieldFactor(RecruitmentPolicy policy)
        {
            switch (policy)
            {
                case RecruitmentPolicy.全員徴兵: return TotalConscriptionYield;
                case RecruitmentPolicy.選抜徴兵: return ConscriptionYield;
                default: return VolunteerYield;
            }
        }

        /// <summary>徴募政策の合意コスト倍率（0..1＝強制の社会的代価。null/未知は志願制扱い＝無コスト）。</summary>
        public static float ConsentCostFactor(RecruitmentPolicy policy)
        {
            switch (policy)
            {
                case RecruitmentPolicy.全員徴兵: return TotalConscriptionConsentCost;
                case RecruitmentPolicy.選抜徴兵: return ConscriptionConsentCost;
                default: return VolunteerConsentCost;
            }
        }

        /// <summary>徴募政策の正統性ボーナス（自発服役のみ厚い＝参政権モデル）。</summary>
        public static float LegitimacyFactor(RecruitmentPolicy policy)
        {
            switch (policy)
            {
                case RecruitmentPolicy.全員徴兵: return TotalConscriptionLegitimacy;
                case RecruitmentPolicy.選抜徴兵: return ConscriptionLegitimacy;
                default: return VolunteerLegitimacy;
            }
        }

        /// <summary>
        /// 徴募可能母数＝勢力の保有総艦艇プール（<see cref="FleetPool.Get"/>）×(1−免除率)。
        /// 免除が多いほど集められる母数が細る。null は0。
        /// </summary>
        public static int RecruitablePool(ServiceObligation o)
        {
            if (o == null) return 0;
            int pool = FleetPool.Get(o.factionId);
            float exempt = Mathf.Clamp01(o.exemptionRate);
            return Mathf.Max(0, Mathf.FloorToInt(pool * (1f - exempt)));
        }

        /// <summary>
        /// 服役年数による逓増倍率（≥1）＝1＋max(0, 服役年数−基準)×逓増係数。
        /// 兵役が基準より重いほど常備兵を多く抱える。
        /// </summary>
        public static float ServiceYearSupplyMultiplier(ServiceObligation o)
        {
            if (o == null) return 1f;
            float over = Mathf.Max(0f, o.serviceYearsRequired - BaselineServiceYears);
            return 1f + over * ServiceYearSupplyScale;
        }

        /// <summary>
        /// この服役契約が供給できる艦隊戦力（≥0）＝徴募可能母数×政策歩留まり×服役年数逓増。
        /// 強制（全員徴兵）ほど太く、志願制ほど細い。<see cref="FleetPool"/> を後退させず読むだけ。
        /// </summary>
        public static int FleetStrengthSupply(ServiceObligation o)
        {
            if (o == null) return 0;
            float supply = RecruitablePool(o) * YieldFactor(o.policy) * ServiceYearSupplyMultiplier(o);
            return Mathf.Max(0, Mathf.FloorToInt(supply));
        }

        /// <summary>
        /// 徴募の合意コスト（≥0＝統治合意の毀損量）＝統治合意基準（<see cref="ConsentRules.ControlStrength"/>）×
        /// 政策コスト倍率×(1−免除率)×服役年数による逓増。免除が手厚いほど社会の反発は和らぎ、
        /// 兵役が重いほど代価は増す。志願制は政策倍率0でコスト0。<see cref="Polity"/> が null なら0。
        /// </summary>
        public static float ConsentCost(ServiceObligation o, Polity polity)
        {
            if (o == null || polity == null) return 0f;
            float baseControl = Mathf.Max(0f, ConsentRules.ControlStrength(polity));
            float exempt = Mathf.Clamp01(o.exemptionRate);
            float yearScale = 1f + Mathf.Max(0f, o.serviceYearsRequired - BaselineServiceYears) * ServiceYearCostScale;
            float cost = baseControl * ConsentCostFactor(o.policy) * (1f - exempt) * yearScale;
            return Mathf.Max(0f, cost);
        }

        /// <summary>
        /// 服役契約の正統性ボーナス（≥0）＝政策の正統性係数×(1−免除率)。
        /// 自発服役は厚く、強制は薄い（免除が多いと薄まる＝皆が等しく担うほど正統）。null は0。
        /// </summary>
        public static float LegitimacyBonus(ServiceObligation o)
        {
            if (o == null) return 0f;
            float exempt = Mathf.Clamp01(o.exemptionRate);
            return Mathf.Max(0f, LegitimacyFactor(o.policy) * (1f - exempt));
        }
    }
}
