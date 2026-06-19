using UnityEngine;

namespace Ginei
{
    /// <summary>協力者（コラボレーター）力学の調整係数。</summary>
    public readonly struct CollaboratorParams
    {
        /// <summary>保身（身の安全）が協力動機を占める重み（0..1）。占領下で生き延びるための妥協。</summary>
        public readonly float selfPreservationWeight;
        /// <summary>利得（地位・財）が協力動機を占める重み（0..1）。占領者に付いて得をする打算。</summary>
        public readonly float materialGainWeight;
        /// <summary>信念（思想的一致）が協力動機を占める重み（0..1）。占領者の理念に共鳴する確信。</summary>
        public readonly float ideologicalWeight;
        /// <summary>占領者の寛容さが協力者の母数を広げる重み（0..1）。寛大な統治ほど協力者が現れる。</summary>
        public readonly float leniencyWeight;
        /// <summary>有能さが統治貢献に効く重み（0..1）。協力者の行政能力。</summary>
        public readonly float competenceWeight;
        /// <summary>現地知識が統治貢献に効く重み（0..1）。占領者が持たぬ土地勘・人脈。</summary>
        public readonly float localKnowledgeWeight;
        /// <summary>協力の露骨さが烙印を強める重み（0..1）。あからさまに付くほど指弾される。</summary>
        public readonly float visibilityWeight;
        /// <summary>住民敵意が烙印を強める重み（0..1）。占領への憎悪が裏切り者へ向く。</summary>
        public readonly float hostilityWeight;
        /// <summary>報復の恐れが占領者への忠誠を縛る重み（0..1）。逃げ場がないほど離反できない。</summary>
        public readonly float fearWeight;
        /// <summary>政権交代が解放後の粛清を強める重み（0..1）。旧体制の打倒ほど報復が苛烈。</summary>
        public readonly float regimeChangeWeight;
        /// <summary>二股の係数（0..1・忠誠の薄さ×粛清リスクで保険をかける度合いの上限寄与）。</summary>
        public readonly float doubleDealingWeight;

        public CollaboratorParams(float selfPreservationWeight, float materialGainWeight,
            float ideologicalWeight, float leniencyWeight, float competenceWeight,
            float localKnowledgeWeight, float visibilityWeight, float hostilityWeight,
            float fearWeight, float regimeChangeWeight, float doubleDealingWeight)
        {
            this.selfPreservationWeight = Mathf.Clamp01(selfPreservationWeight);
            this.materialGainWeight = Mathf.Clamp01(materialGainWeight);
            this.ideologicalWeight = Mathf.Clamp01(ideologicalWeight);
            this.leniencyWeight = Mathf.Clamp01(leniencyWeight);
            this.competenceWeight = Mathf.Clamp01(competenceWeight);
            this.localKnowledgeWeight = Mathf.Clamp01(localKnowledgeWeight);
            this.visibilityWeight = Mathf.Clamp01(visibilityWeight);
            this.hostilityWeight = Mathf.Clamp01(hostilityWeight);
            this.fearWeight = Mathf.Clamp01(fearWeight);
            this.regimeChangeWeight = Mathf.Clamp01(regimeChangeWeight);
            this.doubleDealingWeight = Mathf.Clamp01(doubleDealingWeight);
        }

        /// <summary>
        /// 既定＝保身0.5・利得0.3・信念0.2（動機の加重和は和=1で正規化済み）／
        /// 寛容重み0.5・有能0.6・現地知識0.4・露骨0.5・住民敵意0.5・報復恐れ0.6・政権交代0.6・二股0.5。
        /// </summary>
        public static CollaboratorParams Default =>
            new CollaboratorParams(0.5f, 0.3f, 0.2f, 0.5f, 0.6f, 0.4f, 0.5f, 0.5f, 0.6f, 0.6f, 0.5f);
    }

    /// <summary>
    /// 協力者（コラボレーター）力学の純ロジック（占領者に協力する現地住民）。
    /// 協力する動機（保身／利得／信念）、占領者にとっての登用価値、住民からの反感（裏切り者の烙印）、
    /// 占領者への忠誠（保身ゆえもろい）、解放後の粛清リスク、忠誠が薄く粛清が怖いと二股をかける二重忠誠を扱う。
    /// 銀英伝＝占領星系の現地行政官／恭順した貴族／傀儡政権など。占領後の統治の安定と解放後の報復の連鎖を式に出す。
    /// InsurgencyRules（占領地の反乱そのもの）とは別系統＝こちらは反乱でなく占領者へ付く側の力学。
    /// 倍率・スカラは各係数に掛けて使う（実効値パターン・基準非破壊）。乱数なし・決定論・入力clamp。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CollaboratorRules
    {
        /// <summary>
        /// 協力動機（0..1）。保身／利得／信念の加重和で占領者に協力する動機の強さ。
        /// 各重みの和で正規化するので、どの動機が支配的かはParamsで決まる（既定は保身が最大）。
        /// 三つとも0なら動機なし、いずれかが高く対応する重みも大きいほど1へ近づく。
        /// </summary>
        public static float CollaborationIncentive(float selfPreservation, float materialGain,
            float ideologicalAlignment, CollaboratorParams p)
        {
            float self = Mathf.Clamp01(selfPreservation);
            float gain = Mathf.Clamp01(materialGain);
            float ideo = Mathf.Clamp01(ideologicalAlignment);
            float weightSum = p.selfPreservationWeight + p.materialGainWeight + p.ideologicalWeight;
            if (weightSum <= 0.0001f) return 0f; // 重みが全て0なら動機を定義できない
            float weighted = self * p.selfPreservationWeight
                + gain * p.materialGainWeight
                + ideo * p.ideologicalWeight;
            return Mathf.Clamp01(weighted / weightSum);
        }

        public static float CollaborationIncentive(float selfPreservation, float materialGain,
            float ideologicalAlignment)
            => CollaborationIncentive(selfPreservation, materialGain, ideologicalAlignment,
                CollaboratorParams.Default);

        /// <summary>
        /// 協力者の母数（0..1）。協力動機×占領者の寛容さで協力者がどれだけ現れるか。
        /// 寛容さは（1−寛容重み＋寛容重み×占領者寛容）で効かせる＝寛大な統治ほど協力者が増え、
        /// 苛烈な統治では動機があっても付きにくい。動機0なら母数0（積）。
        /// </summary>
        public static float RecruitmentPool(float incentive, float occupierLeniency, CollaboratorParams p)
        {
            float inc = Mathf.Clamp01(incentive);
            float leniency = Mathf.Clamp01(occupierLeniency);
            float leniencyFactor = (1f - p.leniencyWeight) + p.leniencyWeight * leniency;
            return Mathf.Clamp01(inc * leniencyFactor);
        }

        public static float RecruitmentPool(float incentive, float occupierLeniency)
            => RecruitmentPool(incentive, occupierLeniency, CollaboratorParams.Default);

        /// <summary>
        /// 統治への行政貢献（0..1）。協力者の有能さ×現地知識で占領統治への登用価値。
        /// 有能さ・現地知識をそれぞれ重みで効かせた幾何平均で、どちらか一方が欠ければ価値が崩れる
        /// （無能な現地人も、有能でも土地勘がない者も、占領統治には使いにくい）。
        /// </summary>
        public static float AdministrativeValue(float collaboratorCompetence, float localKnowledge,
            CollaboratorParams p)
        {
            float comp = Mathf.Clamp01(collaboratorCompetence);
            float local = Mathf.Clamp01(localKnowledge);
            float compTerm = comp * p.competenceWeight;
            float localTerm = local * p.localKnowledgeWeight;
            // 重み付き値の幾何平均＝両輪が揃って初めて高い登用価値（片輪欠けは崩れる）
            return Mathf.Clamp01(Mathf.Sqrt(compTerm * localTerm));
        }

        public static float AdministrativeValue(float collaboratorCompetence, float localKnowledge)
            => AdministrativeValue(collaboratorCompetence, localKnowledge, CollaboratorParams.Default);

        /// <summary>
        /// 社会的烙印（0..1・裏切り者の烙印）。協力の露骨さ×住民敵意で住民からの反感。
        /// あからさまに占領者へ付くほど、また住民の敵意が強いほど指弾される。
        /// 露骨さ・住民敵意をそれぞれ重みで効かせた積＝どちらも低ければ烙印は付かない。
        /// </summary>
        public static float SocialStigma(float collaborationVisibility, float populationHostility,
            CollaboratorParams p)
        {
            float vis = Mathf.Clamp01(collaborationVisibility);
            float hostility = Mathf.Clamp01(populationHostility);
            float visTerm = vis * p.visibilityWeight;
            float hostileTerm = hostility * p.hostilityWeight;
            // 両者の積を正規化（重みの積で割って0..1へ）＝露骨さと敵意が揃うと烙印が濃くなる
            float denom = Mathf.Max(0.0001f, p.visibilityWeight * p.hostilityWeight);
            return Mathf.Clamp01(visTerm * hostileTerm / denom);
        }

        public static float SocialStigma(float collaborationVisibility, float populationHostility)
            => SocialStigma(collaborationVisibility, populationHostility, CollaboratorParams.Default);

        /// <summary>
        /// 占領者への忠誠（0..1）。協力動機×報復の恐れで占領者にどれだけ縛られているか。
        /// 報復の恐れは（1−恐れ重み＋恐れ重み×報復の恐れ）で効かせる＝逃げ場がないほど離反できず忠誠が上がる。
        /// ただし保身由来の動機ゆえもろい＝信念でなく打算で付いている者は状況が変われば容易に裏切る
        /// （この値が低いと DoubleDealing が二股を促す）。動機0なら忠誠0（積）。
        /// </summary>
        public static float LoyaltyToOccupier(float incentive, float fearOfReprisal, CollaboratorParams p)
        {
            float inc = Mathf.Clamp01(incentive);
            float fear = Mathf.Clamp01(fearOfReprisal);
            float fearFactor = (1f - p.fearWeight) + p.fearWeight * fear;
            return Mathf.Clamp01(inc * fearFactor);
        }

        public static float LoyaltyToOccupier(float incentive, float fearOfReprisal)
            => LoyaltyToOccupier(incentive, fearOfReprisal, CollaboratorParams.Default);

        /// <summary>
        /// 解放後の粛清リスク（0..1）。社会的烙印×政権交代で解放後にどれだけ報復されるか。
        /// 烙印が濃いほど、また旧体制が打倒されて政権が代わるほど報復が苛烈になる。
        /// 政権交代は（1−政権交代重み＋政権交代重み×政権交代）で効かせる＝占領者がそのまま残れば
        /// 粛清は起きにくいが、解放・政権交代で裏切り者狩りが始まる。烙印0なら粛清リスク0（積）。
        /// </summary>
        public static float PostLiberationRisk(float socialStigma, float regimeChange, CollaboratorParams p)
        {
            float stigma = Mathf.Clamp01(socialStigma);
            float regime = Mathf.Clamp01(regimeChange);
            float regimeFactor = (1f - p.regimeChangeWeight) + p.regimeChangeWeight * regime;
            return Mathf.Clamp01(stigma * regimeFactor);
        }

        public static float PostLiberationRisk(float socialStigma, float regimeChange)
            => PostLiberationRisk(socialStigma, regimeChange, CollaboratorParams.Default);

        /// <summary>
        /// 二股の度合い（0..1・二重忠誠）。占領者への忠誠が低く、解放後の粛清リスクが高いほど
        /// 抵抗勢力にも保険をかける＝表で占領者に協力しつつ裏で解放側へ通じる二股。
        /// 忠誠の薄さ(1−忠誠)×粛清リスク×二股重み＝忠誠が高ければ保険は要らず、粛清リスクが低ければ
        /// 保険を打つ動機がない（両方揃って初めて二股になる）。占領統治の信頼性を蝕む。
        /// </summary>
        public static float DoubleDealing(float loyaltyToOccupier, float postLiberationRisk,
            CollaboratorParams p)
        {
            float loyalty = Mathf.Clamp01(loyaltyToOccupier);
            float risk = Mathf.Clamp01(postLiberationRisk);
            float disloyalty = 1f - loyalty; // 忠誠が薄いほど二股の余地
            return Mathf.Clamp01(disloyalty * risk * p.doubleDealingWeight);
        }

        public static float DoubleDealing(float loyaltyToOccupier, float postLiberationRisk)
            => DoubleDealing(loyaltyToOccupier, postLiberationRisk, CollaboratorParams.Default);

        /// <summary>
        /// 信頼できる協力者か（bool）。占領者への忠誠が閾値以上、かつ二股の度合いが（1−閾値）以下なら true。
        /// 高い忠誠だけでなく裏切りの保険を打っていないことの両方を要求する＝占領者が安心して統治を委ねられる協力者。
        /// </summary>
        public static bool IsReliableCollaborator(float loyaltyToOccupier, float doubleDealing,
            float threshold)
        {
            float loyalty = Mathf.Clamp01(loyaltyToOccupier);
            float dealing = Mathf.Clamp01(doubleDealing);
            float thr = Mathf.Clamp01(threshold);
            return loyalty >= thr && dealing <= (1f - thr);
        }

        /// <summary>既定閾値0.5で信頼できる協力者か判定する委譲版。</summary>
        public static bool IsReliableCollaborator(float loyaltyToOccupier, float doubleDealing)
            => IsReliableCollaborator(loyaltyToOccupier, doubleDealing, 0.5f);
    }
}
