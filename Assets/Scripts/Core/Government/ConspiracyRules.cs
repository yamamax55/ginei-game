using UnityEngine;

namespace Ginei
{
    /// <summary>謀反・クーデターの陰謀の調整係数（救国軍事会議／リップシュタット型の政変計画）。</summary>
    public readonly struct ConspiracyParams
    {
        /// <summary>共謀者の糾合の上限（不満×魅力×政権の弱さが満点でも超えない天井）。</summary>
        public readonly float recruitmentCeiling;
        /// <summary>密告リスクの強さ（人数×諜報×露呈に掛かる係数）。</summary>
        public readonly float informantWeight;
        /// <summary>決行成否で密告リスクが食い込む重み（密告が露見・失敗へ寄与する度合い）。</summary>
        public readonly float informantPenalty;
        /// <summary>失敗後の粛清規模の最大幅（決行失敗・体制の苛烈さが満点のとき）。</summary>
        public readonly float purgeScale;
        /// <summary>決起に値すると判断する既定の成否閾値。</summary>
        public readonly float viableThreshold;

        public ConspiracyParams(float recruitmentCeiling, float informantWeight,
                                float informantPenalty, float purgeScale, float viableThreshold)
        {
            this.recruitmentCeiling = Mathf.Clamp01(recruitmentCeiling);
            this.informantWeight = Mathf.Clamp01(informantWeight);
            this.informantPenalty = Mathf.Clamp01(informantPenalty);
            this.purgeScale = Mathf.Max(0f, purgeScale);
            this.viableThreshold = Mathf.Clamp01(viableThreshold);
        }

        /// <summary>既定＝糾合天井0.9・密告係数0.8・密告ペナルティ0.5・粛清幅1.0・決起閾値0.5。</summary>
        public static ConspiracyParams Default => new ConspiracyParams(0.9f, 0.8f, 0.5f, 1f, 0.5f);
    }

    /// <summary>
    /// 政権転覆（謀反・クーデター）の陰謀の純ロジック。共謀者の糾合・計画の秘匿・決行の機の
    /// 見極め・軍の支持取り付け・内通密告のリスク・決行成否・失敗時の粛清を、状態を持たず
    /// 関数として解く。銀英伝のクーデター（リップシュタット戦役・救国軍事会議）を想定。
    /// 大兵力の共謀ほど秘匿が崩れ密告を呼ぶ＝規律と速さで凌ぐ綱引きが核。乱数は使わず決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ConspiracyRules
    {
        /// <summary>
        /// 共謀者の糾合（0..ceiling）＝不満(0..1)×首謀者の魅力(0..1)×政権の弱さ(0..1)。
        /// 不満があり、人を集める魅力があり、政権が弱っていて初めて謀議の輪が広がる。
        /// </summary>
        public static float ConspiratorRecruitment(float grievance, float leaderCharisma, float regimeWeakness, ConspiracyParams p)
        {
            float g = Mathf.Clamp01(grievance);
            float c = Mathf.Clamp01(leaderCharisma);
            float w = Mathf.Clamp01(regimeWeakness);
            return Mathf.Clamp01(g * c * w * p.recruitmentCeiling);
        }

        public static float ConspiratorRecruitment(float grievance, float leaderCharisma, float regimeWeakness)
            => ConspiratorRecruitment(grievance, leaderCharisma, regimeWeakness, ConspiracyParams.Default);

        /// <summary>
        /// 計画の秘匿（0..1）＝規律(0..1)/(1+共謀者数)。人が増えるほど口は漏れ、規律が高いほど締まる。
        /// 共謀者数は非負として扱う（負入力は0へ）。
        /// </summary>
        public static float PlanSecrecy(float cellDiscipline, float conspiratorCount, ConspiracyParams p)
        {
            float d = Mathf.Clamp01(cellDiscipline);
            float n = Mathf.Max(0f, conspiratorCount);
            return Mathf.Clamp01(d / (1f + n));
        }

        public static float PlanSecrecy(float cellDiscipline, float conspiratorCount)
            => PlanSecrecy(cellDiscipline, conspiratorCount, ConspiracyParams.Default);

        /// <summary>
        /// 軍の取り付け（0..1）＝将校の支持/(将校の支持+体制への忠誠)。両者ゼロなら0（中立）。
        /// クーデターは軍の同心が要＝将校を引き込めるか兵が体制に忠実なままかの比で決まる。
        /// </summary>
        public static float MilitaryBacking(float officerSupport, float troopLoyaltyToRegime, ConspiracyParams p)
        {
            float s = Mathf.Clamp01(officerSupport);
            float l = Mathf.Clamp01(troopLoyaltyToRegime);
            float denom = s + l;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(s / denom);
        }

        public static float MilitaryBacking(float officerSupport, float troopLoyaltyToRegime)
            => MilitaryBacking(officerSupport, troopLoyaltyToRegime, ConspiracyParams.Default);

        /// <summary>
        /// 内通・密告のリスク（0..1）＝密告係数×共謀者数の露呈圧×体制の諜報(0..1)×(1-秘匿)。
        /// 露呈圧は人数を 1-1/(1+人数) で 0..1 に飽和（多数の共謀ほど誰かが漏らす）。
        /// 秘匿が高く諜報が低ければリスクは沈む。
        /// </summary>
        public static float InformantRisk(float conspiratorCount, float regimeIntelligence, float planSecrecy, ConspiracyParams p)
        {
            float n = Mathf.Max(0f, conspiratorCount);
            float exposure = 1f - 1f / (1f + n); // 0..1（人数で飽和）
            float intel = Mathf.Clamp01(regimeIntelligence);
            float leak = 1f - Mathf.Clamp01(planSecrecy);
            return Mathf.Clamp01(p.informantWeight * exposure * intel * leak);
        }

        public static float InformantRisk(float conspiratorCount, float regimeIntelligence, float planSecrecy)
            => InformantRisk(conspiratorCount, regimeIntelligence, planSecrecy, ConspiracyParams.Default);

        /// <summary>
        /// 決行の機（0..1）＝体制の隙(0..1)×共謀者の準備(0..1)。隙だけでも準備だけでも機は熟さない。
        /// </summary>
        public static float TimingReadiness(float regimeVulnerability, float conspiratorPreparation, ConspiracyParams p)
        {
            float v = Mathf.Clamp01(regimeVulnerability);
            float prep = Mathf.Clamp01(conspiratorPreparation);
            return Mathf.Clamp01(v * prep);
        }

        public static float TimingReadiness(float regimeVulnerability, float conspiratorPreparation)
            => TimingReadiness(regimeVulnerability, conspiratorPreparation, ConspiracyParams.Default);

        /// <summary>
        /// 決行の成否（0..1）＝軍支持(0..1)×決行の機(0..1) から密告ペナルティ×密告リスク を引く。
        /// 軍が付き機が熟していても、密告で先手を打たれれば成功率は削られる。
        /// </summary>
        public static float CoupSuccess(float militaryBacking, float timingReadiness, float informantRisk, ConspiracyParams p)
        {
            float b = Mathf.Clamp01(militaryBacking);
            float t = Mathf.Clamp01(timingReadiness);
            float risk = Mathf.Clamp01(informantRisk);
            float raw = b * t - p.informantPenalty * risk;
            return Mathf.Clamp01(raw);
        }

        public static float CoupSuccess(float militaryBacking, float timingReadiness, float informantRisk)
            => CoupSuccess(militaryBacking, timingReadiness, informantRisk, ConspiracyParams.Default);

        /// <summary>
        /// 失敗後の粛清規模（0..purgeScale）＝決行失敗(1-成否)×体制の苛烈さ(0..1)。
        /// しくじったクーデターは反逆として根こそぎ粛清される＝救国軍事会議の末路。
        /// </summary>
        public static float PurgeAftermath(float coupSuccess, float regimeVengefulness, ConspiracyParams p)
        {
            float failure = 1f - Mathf.Clamp01(coupSuccess);
            float v = Mathf.Clamp01(regimeVengefulness);
            return Mathf.Max(0f, failure * v * p.purgeScale);
        }

        public static float PurgeAftermath(float coupSuccess, float regimeVengefulness)
            => PurgeAftermath(coupSuccess, regimeVengefulness, ConspiracyParams.Default);

        /// <summary>決起に値するか＝決行成否が閾値以上なら true（旗を上げる価値がある）。</summary>
        public static bool IsCoupViable(float coupSuccess, float threshold)
        {
            return Mathf.Clamp01(coupSuccess) >= Mathf.Clamp01(threshold);
        }

        public static bool IsCoupViable(float coupSuccess)
            => IsCoupViable(coupSuccess, ConspiracyParams.Default.viableThreshold);
    }
}
