using UnityEngine;

namespace Ginei
{
    /// <summary>契約兵（任期制職業兵士の労働市場）の調整係数。トップレベルの readonly struct ＋ Default ＋ ctor 全 Clamp。</summary>
    public readonly struct ContractSoldierParams
    {
        /// <summary>報酬の供給弾力（高いほど報酬差が志願者数に強く効く）。</summary>
        public readonly float payElasticity;
        /// <summary>再契約ボーナスが定着率を押し上げる強さ 0..1。</summary>
        public readonly float reenlistmentWeight;
        /// <summary>危険手当の最大要求倍率（リスク×死傷率が満ちたときの上乗せ）。</summary>
        public readonly float maxHazardPay;
        /// <summary>専門性の勤続飽和年数（この年数で勤続寄与がほぼ満ちる）。</summary>
        public readonly float skillSaturationYears;
        /// <summary>除隊流出の上限割合 0..1（満了×不満が満ちたときの最大流出）。</summary>
        public readonly float maxDischargeAttrition;
        /// <summary>市場逼迫が賃金上昇へ変換される強さ。</summary>
        public readonly float wageInflationGain;
        /// <summary>兵力安定の供給寄与の重み（定着とのブレンド）。</summary>
        public readonly float supplyWeight;

        public ContractSoldierParams(float payElasticity, float reenlistmentWeight, float maxHazardPay,
            float skillSaturationYears, float maxDischargeAttrition, float wageInflationGain, float supplyWeight)
        {
            this.payElasticity = Mathf.Clamp(payElasticity, 0.1f, 4f);
            this.reenlistmentWeight = Mathf.Clamp01(reenlistmentWeight);
            this.maxHazardPay = Mathf.Max(0f, maxHazardPay);
            this.skillSaturationYears = Mathf.Max(1f, skillSaturationYears);
            this.maxDischargeAttrition = Mathf.Clamp01(maxDischargeAttrition);
            this.wageInflationGain = Mathf.Max(0f, wageInflationGain);
            this.supplyWeight = Mathf.Clamp01(supplyWeight);
        }

        /// <summary>既定＝弾力1・再契約重み0.4・危険手当上限0.8・専門飽和20年・除隊上限0.6・賃金上昇1.5・供給重み0.5。</summary>
        public static ContractSoldierParams Default => new ContractSoldierParams(1f, 0.4f, 0.8f, 20f, 0.6f, 1.5f, 0.5f);
    }

    /// <summary>
    /// 契約兵（任期雇用の職業兵士）の純ロジック。報酬と民間の選択肢で志願者が集まり、職務満足と再契約ボーナスで
    /// 定着し、戦闘リスク・死傷率が危険手当を吊り上げる。勤続と専門化が職業軍人の専門性を育て、契約満了と不満が
    /// 除隊による人材流出を生む。軍の需要と徴募適齢人口で兵士労働市場が逼迫し賃金が上がる。定着・供給・流出の
    /// 綱引きで兵力が安定する。決定論・LINQ/Log/Exp 非使用・入力 clamp。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ContractSoldierRules
    {
        /// <summary>
        /// 志願者の供給（0..1）＝報酬/(報酬+民間の選択肢)。payElasticity で報酬の効きを増幅（実効報酬＝報酬^弾力）。
        /// 報酬・民間の選択肢ともに非負 clamp。両方0なら供給0。
        /// </summary>
        public static float RecruitmentSupply(float payLevel, float civilianAlternatives, ContractSoldierParams p)
        {
            float pay = Mathf.Pow(Mathf.Max(0f, payLevel), p.payElasticity);
            float civ = Mathf.Max(0f, civilianAlternatives);
            float denom = pay + civ;
            if (denom <= 1e-4f) return 0f;
            return Mathf.Clamp01(pay / denom);
        }

        public static float RecruitmentSupply(float payLevel, float civilianAlternatives)
            => RecruitmentSupply(payLevel, civilianAlternatives, ContractSoldierParams.Default);

        /// <summary>
        /// 定着率（0..1）＝職務満足を土台に、再契約ボーナスが reenlistmentWeight ぶん残余を押し上げる。
        /// satisfaction=1 なら1。jobSatisfaction・reenlistmentBonus は 0..1 clamp。
        /// </summary>
        public static float RetentionRate(float jobSatisfaction, float reenlistmentBonus, ContractSoldierParams p)
        {
            float sat = Mathf.Clamp01(jobSatisfaction);
            float bonus = Mathf.Clamp01(reenlistmentBonus);
            float lifted = sat + (1f - sat) * p.reenlistmentWeight * bonus;
            return Mathf.Clamp01(lifted);
        }

        public static float RetentionRate(float jobSatisfaction, float reenlistmentBonus)
            => RetentionRate(jobSatisfaction, reenlistmentBonus, ContractSoldierParams.Default);

        /// <summary>
        /// 危険手当の要求（0..maxHazardPay）＝戦闘リスク×死傷率（どちらも高いほど上乗せ）。
        /// combatRisk・casualtyRate は 0..1 clamp。基本報酬への上乗せ倍率として使う。
        /// </summary>
        public static float HazardPayDemand(float combatRisk, float casualtyRate, ContractSoldierParams p)
        {
            float risk = Mathf.Clamp01(combatRisk);
            float cas = Mathf.Clamp01(casualtyRate);
            return Mathf.Max(0f, p.maxHazardPay * risk * cas);
        }

        public static float HazardPayDemand(float combatRisk, float casualtyRate)
            => HazardPayDemand(combatRisk, casualtyRate, ContractSoldierParams.Default);

        /// <summary>
        /// 職業軍人の専門性（0..1）＝勤続の飽和曲線（serviceYears/skillSaturationYears、上限1）と専門化(0..1)を
        /// 幾何平均でブレンド（どちらか低いと専門性は伸びきらない）。serviceYears は非負 clamp。
        /// </summary>
        public static float ProfessionalSkill(float serviceYears, float specialization, ContractSoldierParams p)
        {
            float years = Mathf.Max(0f, serviceYears);
            float tenure = Mathf.Clamp01(years / p.skillSaturationYears);
            float spec = Mathf.Clamp01(specialization);
            return Mathf.Clamp01(Mathf.Sqrt(tenure * spec));
        }

        public static float ProfessionalSkill(float serviceYears, float specialization)
            => ProfessionalSkill(serviceYears, specialization, ContractSoldierParams.Default);

        /// <summary>
        /// 除隊による人材流出（0..maxDischargeAttrition）＝契約満了の度合い×不満。満了が近く不満が高いほど去る。
        /// contractExpiry・dissatisfaction は 0..1 clamp。
        /// </summary>
        public static float AttritionFromDischarge(float contractExpiry, float dissatisfaction, ContractSoldierParams p)
        {
            float expiry = Mathf.Clamp01(contractExpiry);
            float dissat = Mathf.Clamp01(dissatisfaction);
            return Mathf.Clamp01(p.maxDischargeAttrition * expiry * dissat);
        }

        public static float AttritionFromDischarge(float contractExpiry, float dissatisfaction)
            => AttritionFromDischarge(contractExpiry, dissatisfaction, ContractSoldierParams.Default);

        /// <summary>
        /// 兵士労働市場の逼迫（0..1）＝軍の需要/徴募適齢人口。需要が人口に迫るほど逼迫が1へ近づく。
        /// militaryDemand は非負 clamp、eligiblePopulation は下限 clamp。人口0なら逼迫1（限界）。
        /// </summary>
        public static float LaborMarketTightness(float militaryDemand, float eligiblePopulation, ContractSoldierParams p)
        {
            float demand = Mathf.Max(0f, militaryDemand);
            float pop = Mathf.Max(1e-4f, eligiblePopulation);
            return Mathf.Clamp01(demand / pop);
        }

        public static float LaborMarketTightness(float militaryDemand, float eligiblePopulation)
            => LaborMarketTightness(militaryDemand, eligiblePopulation, ContractSoldierParams.Default);

        /// <summary>
        /// 賃金上昇圧力（0..1）＝市場逼迫×wageInflationGain（逼迫した労働市場ほど賃金を吊り上げる）。
        /// laborMarketTightness は 0..1 clamp。
        /// </summary>
        public static float WageInflation(float laborMarketTightness, ContractSoldierParams p)
        {
            float tight = Mathf.Clamp01(laborMarketTightness);
            return Mathf.Clamp01(tight * p.wageInflationGain);
        }

        public static float WageInflation(float laborMarketTightness)
            => WageInflation(laborMarketTightness, ContractSoldierParams.Default);

        /// <summary>
        /// 兵力の安定（0..1）＝定着と供給のブレンド（supplyWeight）から除隊流出を差し引く。
        /// 定着・供給が高く流出が低いほど1へ近づく。全入力 0..1 clamp。
        /// </summary>
        public static float ForceStability(float retentionRate, float recruitmentSupply, float attritionFromDischarge, ContractSoldierParams p)
        {
            float ret = Mathf.Clamp01(retentionRate);
            float sup = Mathf.Clamp01(recruitmentSupply);
            float att = Mathf.Clamp01(attritionFromDischarge);
            float baseline = Mathf.Lerp(ret, sup, p.supplyWeight);
            return Mathf.Clamp01(baseline - att);
        }

        public static float ForceStability(float retentionRate, float recruitmentSupply, float attritionFromDischarge)
            => ForceStability(retentionRate, recruitmentSupply, attritionFromDischarge, ContractSoldierParams.Default);
    }
}
