using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 給与・俸給のロジック（#1969 WAGE・純ロジック・唯一の窓口）。誰がいくら受け取るかを算定する：人物（提督/文官 #14）の
    /// 俸給は階級×能力で（WAGE-1）、POPの給与は職業×就労者で<b>ざっくり集計</b>し（WAGE-2）、物価をはがして実質賃金・
    /// 生活水準に（WAGE-3）、人物俸給の総額は人件費＝歳出（#163 WAGE-4）、未払いは士気・忠誠を蝕む（#817 WAGE-5）。
    /// 既存の <see cref="AdmiralData"/>(#14)・<see cref="OccupationRules"/>(#110)・<see cref="GdpRules"/>(#1951)・財政(#163)へ
    /// 接続（read-only/接続のみ）。実効値パターン（基準非破壊）。マクロ近似。test-first。
    /// </summary>
    public static class WageRules
    {
        /// <summary>既定の基本俸（階級tier1の俸給）。</summary>
        public const float DefaultBaseSalary = 10f;

        /// <summary>既定の階級ステップ（1階級ごとに基本俸の0.5倍を上乗せ）。</summary>
        public const float DefaultTierStep = 0.5f;

        /// <summary>既定の能力加給率（能力100で+30%／能力0で−30%）。</summary>
        public const float DefaultAbilityBonus = 0.3f;

        /// <summary>能力の基準値（この能力で加給ゼロ＝AdmiralData の中央値）。</summary>
        public const float AbilityMidpoint = 50f;

        // ===== WAGE-1 人物の俸給 =====

        /// <summary>階級ベースの基本俸＝基本俸×(1＋ステップ×(tier−1))。上位階級ほど高給。tier≤0 は0。</summary>
        public static float RankBasePay(int tier, PayScale scale)
        {
            if (scale == null || tier <= 0) return 0f;
            return scale.baseSalary * (1f + scale.tierStep * Mathf.Max(0, tier - 1));
        }

        /// <summary>能力加給係数＝1＋加給率×clamp((能力−基準)/基準, −1, 1)。能力50で1.0・100で(1+率)・0で(1−率)。</summary>
        public static float AbilityFactor(float ability, PayScale scale)
        {
            if (scale == null) return 1f;
            float t = Mathf.Clamp((ability - AbilityMidpoint) / AbilityMidpoint, -1f, 1f);
            return 1f + scale.abilityBonusRatio * t;
        }

        /// <summary>人物の俸給＝階級ベースの基本俸×能力加給係数（階級で土台・能力で上乗せ）。</summary>
        public static float PersonSalary(int tier, float ability, PayScale scale)
            => RankBasePay(tier, scale) * AbilityFactor(ability, scale);

        /// <summary>人物の俸給（提督データから＝階級tier×実効統率で算定）。null は0。</summary>
        public static float PersonSalary(AdmiralData admiral, PayScale scale)
            => admiral == null ? 0f : PersonSalary(admiral.rankTier, admiral.EffectiveLeadership, scale);

        // ===== WAGE-2 POPの給与（ざっくり集計） =====

        /// <summary>POPの給与総額（ざっくり）＝就労者数×賃金率。</summary>
        public static float PopWageBill(float workers, float wageRate)
            => Mathf.Max(0f, workers) * Mathf.Max(0f, wageRate);

        /// <summary>星系のPOP給与（ざっくり）＝生産年齢人口×就業率×賃金率（<see cref="OccupationRules"/> #110 を利用）。</summary>
        public static float ProvincePopWages(Province province, float wageRate)
        {
            if (province == null) return 0f;
            float employed = OccupationRules.WorkingAge(province) * OccupationRules.EmploymentRate(province);
            return employed * Mathf.Max(0f, wageRate);
        }

        /// <summary>勢力のPOP給与総額＝星系群の合計（消費 GDP の C #1951・課税ベースの源）。</summary>
        public static float AggregatePopWages(IReadOnlyList<Province> provinces, float wageRate)
        {
            if (provinces == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < provinces.Count; i++)
                if (provinces[i] != null) sum += ProvincePopWages(provinces[i], wageRate);
            return sum;
        }

        // ===== WAGE-3 実質賃金と生活水準 =====

        /// <summary>実質賃金＝名目賃金/物価水準（基準1.0）。物価で目減りした本当の購買力。物価0以下は名目そのまま。</summary>
        public static float RealWage(float nominalWage, float priceLevel)
            => priceLevel <= 0f ? nominalWage : nominalWage / priceLevel;

        /// <summary>賃金の生活水準係数＝実質賃金/基準賃金（1.0で標準・上回れば豊か→生活水準 #181/支持 #113）。基準0以下は1.0。</summary>
        public static float WageLivingStandard(float realWage, float referenceWage)
            => referenceWage <= 0f ? 1f : Mathf.Max(0f, realWage) / referenceWage;

        // ===== WAGE-4 人件費と財政 =====

        /// <summary>人件費＝在籍人物の俸給総額（歳出 #163 の人件費。高給・大所帯ほど財政を圧迫）。</summary>
        public static float PayrollCost(IReadOnlyList<AdmiralData> roster, PayScale scale)
        {
            if (roster == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null) sum += PersonSalary(roster[i], scale);
            return sum;
        }

        // ===== WAGE-5 俸給未払いの影響 =====

        /// <summary>
        /// 俸給未払いペナルティ（0..1）＝未払い額/本来の俸給。大きいほど士気・忠誠（#817）が下がり、寝返り・離反の火種。
        /// 本来の俸給0以下は0（無給の任務は対象外）。
        /// </summary>
        public static float ArrearsPenalty(float owedSalary, float expectedSalary)
            => expectedSalary <= 0f ? 0f : Mathf.Clamp01(Mathf.Max(0f, owedSalary) / expectedSalary);
    }
}
