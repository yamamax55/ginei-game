using UnityEngine;

namespace Ginei
{
    /// <summary>非攻ドクトリンの調整係数。</summary>
    public readonly struct NonAggressionDoctrineParams
    {
        /// <summary>外交信用に占める「宣言(commitment)」の重み。口で約束した分。</summary>
        public readonly float commitmentWeight;
        /// <summary>外交信用に占める「実績(trackRecord)＝破った前科がない」の重み。守ってきた国ほど信じられる。</summary>
        public readonly float recordWeight;
        /// <summary>信用が時間で蓄積する速度（外交資本の積み上がり）。</summary>
        public readonly float trustAccrualRate;
        /// <summary>非攻を破って攻撃したとき信用が崩壊する強さ（侵略度×この係数）。蓄積より遥かに速い＝非対称。</summary>
        public readonly float breachSeverity;
        /// <summary>攻められた時の防衛戦の正統性が非攻によって底上げされる量（×commitment）。</summary>
        public readonly float defensiveBonus;
        /// <summary>信用が同盟誘引に転化する係数（守ってくれる隣人ほど組みたい）。</summary>
        public readonly float allianceScale;
        /// <summary>評判抑止に占める信用の重み（攻めない国だという評判が攻撃の旨味を削る）。</summary>
        public readonly float reputationWeight;
        /// <summary>評判抑止に占める防衛力の重み（守りが固いから攻めても得しない）。</summary>
        public readonly float defenseWeight;

        public NonAggressionDoctrineParams(float commitmentWeight, float recordWeight,
            float trustAccrualRate, float breachSeverity, float defensiveBonus,
            float allianceScale, float reputationWeight, float defenseWeight)
        {
            this.commitmentWeight = Mathf.Clamp01(commitmentWeight);
            this.recordWeight = Mathf.Clamp01(recordWeight);
            this.trustAccrualRate = Mathf.Max(0f, trustAccrualRate);
            this.breachSeverity = Mathf.Max(0f, breachSeverity);
            this.defensiveBonus = Mathf.Max(0f, defensiveBonus);
            this.allianceScale = Mathf.Max(0f, allianceScale);
            this.reputationWeight = Mathf.Clamp01(reputationWeight);
            this.defenseWeight = Mathf.Clamp01(defenseWeight);
        }

        /// <summary>既定＝宣言重み0.4・実績重み0.6・蓄積0.1・違反崩壊2.0・防衛底上げ0.3・同盟係数0.8・評判重み0.6・防衛重み0.4。</summary>
        public static NonAggressionDoctrineParams Default => new NonAggressionDoctrineParams(
            0.4f, 0.6f, 0.1f, 2f, 0.3f, 0.8f, 0.6f, 0.4f);
    }

    /// <summary>
    /// 非攻ドクトリンの純ロジック（MOZI-2 #1560・墨子「非攻＝侵略戦争を否定する」を国是に自己拘束する）。
    /// 「攻撃戦争は不義、ただし防衛は是」を国是として自らを縛ると、<b>外交信用という資本</b>を得る
    /// （侵略しないと約束し守ってきた国は信頼される）が、<b>先制攻撃の選択肢を捨てる</b>トレードオフを負う。
    /// 信用は時間で積み上がる外交資本だが、一度非攻を破って攻撃すれば偽善者の烙印を押され一瞬で崩れる＝非対称。
    /// 攻められた時の防衛戦の正統性は逆に高まり（非攻＝防衛は是・大義名分）、信用ある非攻国は同盟を組みやすく、
    /// 「攻めない国だが守りは固い」という評判が攻撃の旨味を削る（評判による抑止）。
    /// <see cref="DiplomacyRules"/>（条約の状態遷移）とは別＝こちらは非攻の自己拘束が生む外交信用・攻撃放棄を扱う。
    /// <see cref="DeterrenceRules"/>（報復による抑止）とは別＝あちらは撃ち返す力、こちらは攻めないという評判による抑止。
    /// 信頼の非対称そのもの（CommercialIntegrityRules・別EPIC）とは別＝こちらは国是としての非攻に特化。
    /// 全入力クランプ・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NonAggressionDoctrineRules
    {
        /// <summary>
        /// 外交信用（0..1）＝非攻の宣言(0..1)×宣言重み＋実績(0..1)×実績重み。
        /// 約束だけでは足りず、破った前科がない実績でこそ信じられる（守ってきた国ほど信用される）。
        /// </summary>
        public static float DiplomaticCredibility(float commitment, float trackRecord, NonAggressionDoctrineParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(commitment) * p.commitmentWeight +
                Mathf.Clamp01(trackRecord) * p.recordWeight);
        }

        public static float DiplomaticCredibility(float commitment, float trackRecord)
            => DiplomaticCredibility(commitment, trackRecord, NonAggressionDoctrineParams.Default);

        /// <summary>
        /// 外交資本（0..1）＝現在の信用資本に、外交信用(0..1)×蓄積率×dt を上積みする。
        /// 信用は時間で積み上がる資本＝守り続けた年月だけ厚くなる（だが破れば一瞬で失う＝CommitmentBreach）。
        /// </summary>
        public static float TrustCapital(float currentCapital, float diplomaticCredibility, float dt, NonAggressionDoctrineParams p)
        {
            float cap = Mathf.Clamp01(currentCapital);
            if (dt <= 0f) return cap;
            return Mathf.Clamp01(cap + Mathf.Clamp01(diplomaticCredibility) * p.trustAccrualRate * dt);
        }

        public static float TrustCapital(float currentCapital, float diplomaticCredibility, float dt)
            => TrustCapital(currentCapital, diplomaticCredibility, dt, NonAggressionDoctrineParams.Default);

        /// <summary>
        /// 先制攻撃の放棄度（0..1）＝非攻の宣言度(0..1)そのもの。
        /// 自己拘束が強いほど先制攻撃の選択肢を完全に捨てる＝信用と引き換えに払う機会費用（攻めるという手を失う）。
        /// </summary>
        public static float OffenseForfeit(float commitment)
        {
            return Mathf.Clamp01(commitment);
        }

        /// <summary>
        /// 防衛戦の正統性（0..1）。攻められていない平時は0（非攻だから先に手は出さない）、
        /// 攻められた時(underAttack)は基礎1.0に非攻の宣言度×防衛底上げを加えて頭打ち
        /// ＝非攻を掲げる国の防衛戦は「不義に対する正義の戦い」として大義名分が立つ。
        /// </summary>
        public static float DefensiveLegitimacy(float commitment, bool underAttack, NonAggressionDoctrineParams p)
        {
            if (!underAttack) return 0f;
            return Mathf.Clamp01(1f + Mathf.Clamp01(commitment) * p.defensiveBonus);
        }

        public static float DefensiveLegitimacy(float commitment, bool underAttack)
            => DefensiveLegitimacy(commitment, underAttack, NonAggressionDoctrineParams.Default);

        /// <summary>
        /// 同盟誘引（0..1）＝外交資本(0..1)×同盟係数。
        /// 信用ある非攻国は「守ってくれる隣人」として同盟相手に選ばれやすい。
        /// </summary>
        public static float AllianceAttraction(float trustCapital, NonAggressionDoctrineParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(trustCapital) * p.allianceScale);
        }

        public static float AllianceAttraction(float trustCapital)
            => AllianceAttraction(trustCapital, NonAggressionDoctrineParams.Default);

        /// <summary>
        /// 非攻を破った後に残る外交資本（0..1）＝資本(0..1)−侵略度(0..1)×違反崩壊。
        /// 違反崩壊が大きいほど僅かな攻撃でも積年の信用が吹き飛ぶ＝偽善者の烙印は蓄積より遥かに速く効く（非対称）。
        /// </summary>
        public static float CommitmentBreach(float trustCapital, float aggression, NonAggressionDoctrineParams p)
        {
            float cap = Mathf.Clamp01(trustCapital);
            return Mathf.Clamp01(cap - Mathf.Clamp01(aggression) * p.breachSeverity);
        }

        public static float CommitmentBreach(float trustCapital, float aggression)
            => CommitmentBreach(trustCapital, aggression, NonAggressionDoctrineParams.Default);

        /// <summary>
        /// 評判による抑止（0..1）＝外交資本(0..1)×評判重み＋防衛力(0..1)×防衛重み。
        /// 「攻めない国だが守りは固い」＝信用と防衛力の合算が、相手に「攻めても得しない」と思わせる
        /// （報復で脅す DeterrenceRules と違い、評判と守勢で攻撃の旨味を奪う）。
        /// </summary>
        public static float DeterrenceViaReputation(float trustCapital, float defensiveStrength, NonAggressionDoctrineParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(trustCapital) * p.reputationWeight +
                Mathf.Clamp01(defensiveStrength) * p.defenseWeight);
        }

        public static float DeterrenceViaReputation(float trustCapital, float defensiveStrength)
            => DeterrenceViaReputation(trustCapital, defensiveStrength, NonAggressionDoctrineParams.Default);

        /// <summary>信頼できる非攻国か＝外交信用が閾値(0..1)以上。閾値は呼び出し側のAI判断の素。</summary>
        public static bool IsCrediblyPacifist(float diplomaticCredibility, float threshold)
        {
            return Mathf.Clamp01(diplomaticCredibility) >= Mathf.Clamp01(threshold);
        }
    }
}
