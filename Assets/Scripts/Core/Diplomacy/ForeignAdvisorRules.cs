using UnityEngine;

namespace Ginei
{
    /// <summary>外国顧問（お雇い外国人）の純データ。</summary>
    public struct ForeignAdvisor
    {
        /// <summary>専門知識（0..1）。顧問が持つ先進技術・運用ノウハウの水準。</summary>
        public float expertise;
        /// <summary>教える意欲（0..1）。同盟関係に依存＝親密なほど惜しみなく教える。</summary>
        public float willingnessToTeach;
        /// <summary>移転済み知識（0..1）。受入国へ根付いた知識の蓄積（最初は依存・次第に自立）。</summary>
        public float knowledgeTransferred;

        public ForeignAdvisor(float expertise, float willingnessToTeach, float knowledgeTransferred = 0f)
        {
            this.expertise = Mathf.Clamp01(expertise);
            this.willingnessToTeach = Mathf.Clamp01(willingnessToTeach);
            this.knowledgeTransferred = Mathf.Clamp01(knowledgeTransferred);
        }
    }

    /// <summary>外国顧問・軍事援助の調整係数。</summary>
    public readonly struct ForeignAdvisorParams
    {
        /// <summary>同盟が招請可能度に寄与する重み（同盟の強さ）。</summary>
        public readonly float allianceWeight;
        /// <summary>受入国の魅力（hostPrestige）が招請可能度に寄与する重み。</summary>
        public readonly float prestigeWeight;
        /// <summary>顧問1人ぶんの研究加速の最大倍率分（expertise=1・吸収=1 のとき +この値）。</summary>
        public readonly float maxResearchBonus;
        /// <summary>吸収0でも受け取れる加速の下限割合（学ぶ側に最低限の下地）。</summary>
        public readonly float absorptionFloor;
        /// <summary>人材育成1tickの基礎速度（expertise=1・育成対象=1・dt=1 で蓄積する量）。</summary>
        public readonly float talentRate;
        /// <summary>知識移転1tickの基礎速度（expertise=1・意欲=1・dt=1 で進む量）。</summary>
        public readonly float transferRate;
        /// <summary>依存リスクの最大値（自立0・移転途上で被る上限・0..1）。</summary>
        public readonly float maxDependencyRisk;
        /// <summary>顧問急引き上げのショック幅（依存最大で被る打撃の上限・0..1）。</summary>
        public readonly float withdrawalShockScale;
        /// <summary>自立判定の既定しきい値（移転がこれ以上で外国顧問なしに立つ）。</summary>
        public readonly float selfSufficientThreshold;

        public ForeignAdvisorParams(float allianceWeight, float prestigeWeight,
                                    float maxResearchBonus, float absorptionFloor,
                                    float talentRate, float transferRate,
                                    float maxDependencyRisk, float withdrawalShockScale,
                                    float selfSufficientThreshold)
        {
            this.allianceWeight = Mathf.Clamp01(allianceWeight);
            this.prestigeWeight = Mathf.Clamp01(prestigeWeight);
            this.maxResearchBonus = Mathf.Max(0f, maxResearchBonus);
            this.absorptionFloor = Mathf.Clamp01(absorptionFloor);
            this.talentRate = Mathf.Max(0f, talentRate);
            this.transferRate = Mathf.Max(0f, transferRate);
            this.maxDependencyRisk = Mathf.Clamp01(maxDependencyRisk);
            this.withdrawalShockScale = Mathf.Clamp01(withdrawalShockScale);
            this.selfSufficientThreshold = Mathf.Clamp01(selfSufficientThreshold);
        }

        /// <summary>既定＝同盟重み0.6/魅力重み0.4/研究加速最大+1.0/吸収下限0.2/育成0.1/移転0.05/依存リスク上限0.6/引き上げショック0.5/自立しきい値0.7。</summary>
        public static ForeignAdvisorParams Default =>
            new ForeignAdvisorParams(0.6f, 0.4f, 1f, 0.2f, 0.1f, 0.05f, 0.6f, 0.5f, 0.7f);
    }

    /// <summary>
    /// 外国顧問・軍事援助（坂の上の雲型近代化・#1435）の純ロジック＝お雇い外国人による近代化加速。
    /// 後発国は同盟・援助条件下で先進国から軍事顧問・技術者を招き（招請可能度＝同盟の強さ×受入国の魅力）、
    /// 研究・人材育成を加速する（明治日本がイギリス海軍・ドイツ陸軍に学んだように）。ただし学ぶ側に
    /// 吸収の下地が要り（下地ゼロでは加速も鈍い）、顧問に依存し続けると技術が根付かず自立できないが、
    /// 知識移転が進めば外国顧問を不要にして自立する＝近代化の完成。同盟が破綻して顧問が急に引き上げると、
    /// 依存度が高いほど打撃が大きい。
    /// <see cref="ForeignAidRules"/>（越境する援助の opinion・依存）とは別＝こちらは<b>外国顧問による知識移転</b>。
    /// <see cref="MentorshipRules"/>（個対個の師弟伝承）とは別＝こちらは<b>国家規模のお雇い外国人</b>。
    /// 近代化計画そのもの（<see cref="ModernizationProgramRules"/>＝予算・改革の進捗）や、技術が個人に宿る
    /// こと（<see cref="TechBearerRules"/>＝名工が死ねば工法も死ぬ）とは別系統で、ここは「外国顧問の招請と
    /// 知識移転・依存と自立のジレンマ」の係数のみを扱う。倍率は基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ForeignAdvisorRules
    {
        /// <summary>
        /// 外国顧問を招ける度合い（0..1）＝同盟の強さ×同盟重み ＋ 受入国の魅力×魅力重み。
        /// 同盟・援助条件が顧問招請の前提＝同盟が固く受入国が魅力的なほど良い顧問を招ける。
        /// </summary>
        public static float AdvisorAvailability(float allianceStrength, float hostPrestige, ForeignAdvisorParams p)
        {
            float alliance = Mathf.Clamp01(allianceStrength);
            float prestige = Mathf.Clamp01(hostPrestige);
            return Mathf.Clamp01(alliance * p.allianceWeight + prestige * p.prestigeWeight);
        }

        public static float AdvisorAvailability(float allianceStrength, float hostPrestige)
            => AdvisorAvailability(allianceStrength, hostPrestige, ForeignAdvisorParams.Default);

        /// <summary>
        /// 研究加速倍率（1..1+maxResearchBonus）。顧問の専門知識×受入側の吸収能力で研究を加速する。
        /// 吸収（absorptiveCapacity 0..1）は absorptionFloor を下限に効く＝学ぶ側に下地が要る
        /// （下地ゼロでも顧問の専門知識ぶんはわずかに効く）。顧問がいなければ（expertise=0）等倍＝従来動作。
        /// 研究進捗量に掛ける想定（実効値パターン・基準非破壊）。
        /// </summary>
        public static float ResearchAcceleration(float advisorExpertise, float absorptiveCapacity, ForeignAdvisorParams p)
        {
            float exp = Mathf.Clamp01(advisorExpertise);
            float absorption = Mathf.Lerp(p.absorptionFloor, 1f, Mathf.Clamp01(absorptiveCapacity));
            return 1f + p.maxResearchBonus * exp * absorption;
        }

        public static float ResearchAcceleration(float advisorExpertise, float absorptiveCapacity)
            => ResearchAcceleration(advisorExpertise, absorptiveCapacity, ForeignAdvisorParams.Default);

        /// <summary>
        /// 人材育成の1tickぶんの蓄積量＝顧問の専門知識×育成対象の規模（traineeCount 0..1）×基礎速度×dt。
        /// 顧問が現地の人材を育てる（留学・指導）＝専門知識が高く育成対象が多いほど速く人材が育つ。
        /// 現地人材の能力ボーナス等へ積む想定（基準非破壊）。
        /// </summary>
        public static float TalentDevelopment(float advisorExpertise, float traineeCount, float dt, ForeignAdvisorParams p)
        {
            float exp = Mathf.Clamp01(advisorExpertise);
            float trainees = Mathf.Clamp01(traineeCount);
            return p.talentRate * exp * trainees * Mathf.Max(0f, dt);
        }

        public static float TalentDevelopment(float advisorExpertise, float traineeCount, float dt)
            => TalentDevelopment(advisorExpertise, traineeCount, dt, ForeignAdvisorParams.Default);

        /// <summary>
        /// 知識移転の1tick後の値（0..1）＝移転＋（基礎速度×顧問の専門知識×教える意欲×伸び代(1−移転)）×dt。
        /// 知識が時間で移転していく＝最初は顧問に依存し、移転が進むほど受入国へ根付く。
        /// 教える意欲（同盟関係に依存）が低い／専門知識が乏しいと移転は鈍る。
        /// </summary>
        public static float KnowledgeTransferTick(float transferred, float advisorExpertise, float willingnessToTeach, float dt, ForeignAdvisorParams p)
        {
            float t = Mathf.Clamp01(transferred);
            float exp = Mathf.Clamp01(advisorExpertise);
            float willing = Mathf.Clamp01(willingnessToTeach);
            float delta = p.transferRate * exp * willing * (1f - t) * Mathf.Max(0f, dt);
            return Mathf.Clamp01(t + delta);
        }

        public static float KnowledgeTransferTick(float transferred, float advisorExpertise, float willingnessToTeach, float dt)
            => KnowledgeTransferTick(transferred, advisorExpertise, willingnessToTeach, dt, ForeignAdvisorParams.Default);

        /// <summary>
        /// 顧問依存リスク（0..maxDependencyRisk）＝（1−自立度）×(1−移転)×上限。
        /// 顧問に依存し続けると自立できない＝知識が根付かず（移転が浅く）自前の自給力（selfSufficiency 0..1）が
        /// 低いほど危ない。移転が進み自給力が育てばリスクは消える（技術が根付く）。
        /// </summary>
        public static float DependencyRisk(float knowledgeTransferred, float selfSufficiency, ForeignAdvisorParams p)
        {
            float transferred = Mathf.Clamp01(knowledgeTransferred);
            float self = Mathf.Clamp01(selfSufficiency);
            return (1f - self) * (1f - transferred) * p.maxDependencyRisk;
        }

        public static float DependencyRisk(float knowledgeTransferred, float selfSufficiency)
            => DependencyRisk(knowledgeTransferred, selfSufficiency, ForeignAdvisorParams.Default);

        /// <summary>
        /// 外国顧問なしで立てる自立度（0..1）＝移転済み知識×現地能力。移転が進むと外国顧問なしで自立する
        /// ＝お雇い外国人を不要にする（近代化の完成）。移転または現地能力（localCapability 0..1）のどちらかが
        /// 欠ければ自立はできない（知識があっても運用する人がいなければ立てない）。
        /// </summary>
        public static float IndependenceTransition(float knowledgeTransferred, float localCapability, ForeignAdvisorParams p)
        {
            float transferred = Mathf.Clamp01(knowledgeTransferred);
            float local = Mathf.Clamp01(localCapability);
            return Mathf.Clamp01(transferred * local);
        }

        public static float IndependenceTransition(float knowledgeTransferred, float localCapability)
            => IndependenceTransition(knowledgeTransferred, localCapability, ForeignAdvisorParams.Default);

        /// <summary>
        /// 顧問の急引き上げが与える打撃（0..1）＝依存度×ショック幅。
        /// 同盟破綻等で顧問が急に引き上げると、依存度が高いほど痛い（自前で回せない＝近代化が止まる）。
        /// suddenWithdrawal=false なら円満な引き継ぎ＝打撃なし。研究・産出の打撃係数として使う想定。
        /// </summary>
        public static float AdvisorWithdrawalShock(float dependency, bool suddenWithdrawal, ForeignAdvisorParams p)
        {
            if (!suddenWithdrawal) return 0f;
            return Mathf.Clamp01(dependency) * p.withdrawalShockScale;
        }

        public static float AdvisorWithdrawalShock(float dependency, bool suddenWithdrawal)
            => AdvisorWithdrawalShock(dependency, suddenWithdrawal, ForeignAdvisorParams.Default);

        /// <summary>
        /// 外国顧問なしで自立できる段階か＝移転済み知識がしきい値以上。真なら顧問を不要にして自立した
        /// （近代化の完成・引き上げの打撃を恐れる必要がない）。
        /// </summary>
        public static bool IsSelfSufficient(float knowledgeTransferred, float threshold)
        {
            return Mathf.Clamp01(knowledgeTransferred) >= Mathf.Clamp01(threshold);
        }

        public static bool IsSelfSufficient(float knowledgeTransferred)
            => IsSelfSufficient(knowledgeTransferred, ForeignAdvisorParams.Default.selfSufficientThreshold);
    }
}
