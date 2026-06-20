using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 傭兵団（金で雇う私兵部隊）の調整係数。練度×装備の戦力品質、契約金・維持費、忠誠（金で買う＝脆い）、
    /// 寝返り・略奪のリスク、評判による募集、正味価値の重み。全フィールド ctor で clamp。
    /// </summary>
    public readonly struct MercenaryCompanyParams
    {
        /// <summary>戦力品質に占める経験の比重（0..1。残りが装備の比重）。</summary>
        public readonly float experienceWeight;
        /// <summary>契約金・維持費の基準係数（品質×規模に掛ける）。</summary>
        public readonly float costScale;
        /// <summary>忠誠に占める支払いの確実さの比重（0..1。残りが分け前の比重）。</summary>
        public readonly float paymentWeight;
        /// <summary>寝返りリスクで敵の買収が効く強さ（0..1）。</summary>
        public readonly float bribeInfluence;
        /// <summary>寝返りリスクで給与遅配が効く強さ（0..1）。</summary>
        public readonly float arrearsInfluence;
        /// <summary>評判に占める戦績の比重（0..1。残りが信頼性の比重）。</summary>
        public readonly float recordWeight;
        /// <summary>募集の引きで提示報酬が効く強さ（0..1。残りが評判の比重）。</summary>
        public readonly float payOfferInfluence;
        /// <summary>正味価値で寝返りリスクを引く強さ。</summary>
        public readonly float defectionPenalty;
        /// <summary>正味価値でコストを引く強さ。</summary>
        public readonly float costPenalty;

        public MercenaryCompanyParams(
            float experienceWeight, float costScale, float paymentWeight,
            float bribeInfluence, float arrearsInfluence, float recordWeight,
            float payOfferInfluence, float defectionPenalty, float costPenalty)
        {
            this.experienceWeight = Mathf.Clamp01(experienceWeight);
            this.costScale = Mathf.Max(0f, costScale);
            this.paymentWeight = Mathf.Clamp01(paymentWeight);
            this.bribeInfluence = Mathf.Clamp01(bribeInfluence);
            this.arrearsInfluence = Mathf.Clamp01(arrearsInfluence);
            this.recordWeight = Mathf.Clamp01(recordWeight);
            this.payOfferInfluence = Mathf.Clamp01(payOfferInfluence);
            this.defectionPenalty = Mathf.Max(0f, defectionPenalty);
            this.costPenalty = Mathf.Max(0f, costPenalty);
        }

        /// <summary>既定＝経験比重0.6・コスト係数1・支払い比重0.7・買収0.5・遅配0.5・戦績比重0.6・提示報酬0.5・寝返り罰0.6・コスト罰0.4。</summary>
        public static MercenaryCompanyParams Default =>
            new MercenaryCompanyParams(0.6f, 1f, 0.7f, 0.5f, 0.5f, 0.6f, 0.5f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 傭兵団の純ロジック。傭兵の戦力は練度（経験）と装備で決まり、戦力品質と規模で契約金・維持費が嵩む。
    /// 忠誠は理念でなく金で買う＝支払いの確実さと分け前で保たれ、給与遅配・敵の買収で寝返る（金の切れ目が縁の切れ目）。
    /// 戦績と信頼性が評判を生み次の雇用を呼び、規律の緩みと給与不足が略奪を招く。最後に品質−寝返り−コストで正味価値を量る。
    /// 決定論・乱数なし・入力clamp・実効値パターン。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MercenaryCompanyRules
    {
        /// <summary>戦力品質（0..1）＝経験と装備の加重平均。練度の高い精兵が良装備を持てば1に近づく。</summary>
        public static float CombatQuality(float experience, float equipment, MercenaryCompanyParams p)
        {
            float exp = Mathf.Clamp01(experience);
            float eq = Mathf.Clamp01(equipment);
            return Mathf.Clamp01(exp * p.experienceWeight + eq * (1f - p.experienceWeight));
        }

        public static float CombatQuality(float experience, float equipment)
            => CombatQuality(experience, equipment, MercenaryCompanyParams.Default);

        /// <summary>契約金・維持費＝品質×規模×コスト係数（精強で大規模なほど高くつく）。非負。</summary>
        public static float ContractCost(float combatQuality, float companySize, MercenaryCompanyParams p)
        {
            float q = Mathf.Clamp01(combatQuality);
            float size = Mathf.Max(0f, companySize);
            return q * size * p.costScale;
        }

        public static float ContractCost(float combatQuality, float companySize)
            => ContractCost(combatQuality, companySize, MercenaryCompanyParams.Default);

        /// <summary>忠誠（0..1）＝支払いの確実さと分け前の加重平均（金で買う忠誠）。</summary>
        public static float Loyalty(float paymentReliability, float sharedPlunder, MercenaryCompanyParams p)
        {
            float pay = Mathf.Clamp01(paymentReliability);
            float plunder = Mathf.Clamp01(sharedPlunder);
            return Mathf.Clamp01(pay * p.paymentWeight + plunder * (1f - p.paymentWeight));
        }

        public static float Loyalty(float paymentReliability, float sharedPlunder)
            => Loyalty(paymentReliability, sharedPlunder, MercenaryCompanyParams.Default);

        /// <summary>寝返りリスク（0..1）＝(1−忠誠)の素地に、敵の買収と給与遅配が拍車をかける。</summary>
        public static float DefectionRisk(float loyalty, float enemyBribe, float paymentArrears, MercenaryCompanyParams p)
        {
            float disloyal = 1f - Mathf.Clamp01(loyalty);
            float bribe = Mathf.Clamp01(enemyBribe);
            float arrears = Mathf.Clamp01(paymentArrears);
            float pressure = 1f + bribe * p.bribeInfluence + arrears * p.arrearsInfluence;
            return Mathf.Clamp01(disloyal * pressure);
        }

        public static float DefectionRisk(float loyalty, float enemyBribe, float paymentArrears)
            => DefectionRisk(loyalty, enemyBribe, paymentArrears, MercenaryCompanyParams.Default);

        /// <summary>評判（0..1）＝戦績と信頼性の加重平均（次の雇用に響く）。</summary>
        public static float Reputation(float victoryRecord, float reliability, MercenaryCompanyParams p)
        {
            float record = Mathf.Clamp01(victoryRecord);
            float rel = Mathf.Clamp01(reliability);
            return Mathf.Clamp01(record * p.recordWeight + rel * (1f - p.recordWeight));
        }

        public static float Reputation(float victoryRecord, float reliability)
            => Reputation(victoryRecord, reliability, MercenaryCompanyParams.Default);

        /// <summary>募集の引き（0..1）＝評判と提示報酬の加重平均（名声か金で人を集める）。</summary>
        public static float RecruitmentDraw(float reputation, float payOffered, MercenaryCompanyParams p)
        {
            float rep = Mathf.Clamp01(reputation);
            float pay = Mathf.Clamp01(payOffered);
            return Mathf.Clamp01(pay * p.payOfferInfluence + rep * (1f - p.payOfferInfluence));
        }

        public static float RecruitmentDraw(float reputation, float payOffered)
            => RecruitmentDraw(reputation, payOffered, MercenaryCompanyParams.Default);

        /// <summary>略奪傾向（0..1）＝(1−規律)の素地に給与不足が拍車をかける（払わないと現地で奪う）。</summary>
        public static float PlunderTendency(float discipline, float payShortfall, MercenaryCompanyParams p)
        {
            float indiscipline = 1f - Mathf.Clamp01(discipline);
            float shortfall = Mathf.Clamp01(payShortfall);
            // 規律の緩みが地、給与不足が乗数（払えていれば略奪は抑えられる）。
            return Mathf.Clamp01(indiscipline * (0.5f + 0.5f * shortfall) + 0.5f * shortfall * indiscipline);
        }

        public static float PlunderTendency(float discipline, float payShortfall)
            => PlunderTendency(discipline, payShortfall, MercenaryCompanyParams.Default);

        /// <summary>
        /// 傭兵の正味価値（-1..1）＝戦力品質から寝返りリスクとコストを差し引く。
        /// contractCost は規模で青天井になりうるので 0..1 に正規化して効かせる（品質1基準）。
        /// 高品質でも寝返りやすく高コストなら負＝雇う価値なしを表す。
        /// </summary>
        public static float MercenaryValue(float combatQuality, float defectionRisk, float contractCost, MercenaryCompanyParams p)
        {
            float q = Mathf.Clamp01(combatQuality);
            float defect = Mathf.Clamp01(defectionRisk);
            float costNorm = Mathf.Clamp01(contractCost); // コストは 0..1 へ正規化（呼び側で品質基準にスケール）
            float value = q - defect * p.defectionPenalty - costNorm * p.costPenalty;
            return Mathf.Clamp(value, -1f, 1f);
        }

        public static float MercenaryValue(float combatQuality, float defectionRisk, float contractCost)
            => MercenaryValue(combatQuality, defectionRisk, contractCost, MercenaryCompanyParams.Default);
    }
}
