using UnityEngine;

namespace Ginei
{
    /// <summary>大衆動員（政治運動への参加）の調整係数。</summary>
    public readonly struct MassMobilizationParams
    {
        /// <summary>同調効果（バンドワゴン）の非線形度（参加比率の何乗で効くか＝1未満で初期参加者が効く）。</summary>
        public readonly float bandwagonExponent;
        /// <summary>臨界量到達に対する同調効果の重み（人の動きが臨界量をどれだけ押し上げるか）。</summary>
        public readonly float criticalMassWeight;
        /// <summary>臨界量到達に対する動員訴求の重み（大義/魅力が臨界量をどれだけ押し上げるか）。</summary>
        public readonly float appealWeight;
        /// <summary>ネットワーク増幅の上限（紐帯×情報流通でも増幅はこの天井まで）。</summary>
        public readonly float maxNetworkAmplification;
        /// <summary>抑圧の抑止に効く弾圧の重み（弾圧がどれだけ動員を抑え込むか）。</summary>
        public readonly float repressionWeight;
        /// <summary>動員規模に対するネットワーク増幅の寄与の重み（増幅が規模をどれだけ底上げするか）。</summary>
        public readonly float amplificationWeight;
        /// <summary>運動が雪だるま式に成立したとみなす動員規模の既定閾値（これ超で運動が立ち上がる）。</summary>
        public readonly float snowballThreshold;

        public MassMobilizationParams(float bandwagonExponent, float criticalMassWeight, float appealWeight,
            float maxNetworkAmplification, float repressionWeight, float amplificationWeight, float snowballThreshold)
        {
            this.bandwagonExponent = Mathf.Max(0f, bandwagonExponent);
            this.criticalMassWeight = Mathf.Clamp01(criticalMassWeight);
            this.appealWeight = Mathf.Clamp01(appealWeight);
            this.maxNetworkAmplification = Mathf.Max(1f, maxNetworkAmplification);
            this.repressionWeight = Mathf.Clamp01(repressionWeight);
            this.amplificationWeight = Mathf.Clamp01(amplificationWeight);
            this.snowballThreshold = Mathf.Clamp01(snowballThreshold);
        }

        /// <summary>既定＝同調指数0.5・臨界量(同調0.6/訴求0.4)・増幅上限2.0・弾圧重み0.8・増幅寄与0.3・雪だるま閾値0.5。</summary>
        public static MassMobilizationParams Default => new MassMobilizationParams(0.5f, 0.6f, 0.4f, 2f, 0.8f, 0.3f, 0.5f);
    }

    /// <summary>
    /// 大衆動員＝政治運動・社会運動への人々の参加の純ロジック。動員の呼びかけ（大義×指導者の魅力）、
    /// 参加の閾値（個人のリスク／効力感＝人は他人が参加すると参加する＝グラノヴェッターの閾値モデル）、
    /// 臨界量（クリティカルマス）、フリーライダー問題（みんなやるから自分は要らない＝オルソンの集合行為問題）、
    /// 動員のネットワーク効果（社会的紐帯×情報流通＝弱い紐帯の動員力）、抑圧下の動員（弾圧による抑止）、
    /// 運動の雪だるま式拡大を写像する。中核 <see cref="MobilizationScale"/> は 0..1 の動員規模＝臨界×増幅から
    /// 抑止・ただ乗りを引いた成立度。全入力クランプ・乱数なし・決定論・基準値非破壊。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class MassMobilizationRules
    {
        /// <summary>
        /// 動員の訴求（0..1）＝大義の切迫×指導者の魅力で運動への呼びかけが響く度合い。
        /// 切迫も魅力も無ければ呼びかけは届かない（どちらか欠ければ訴求は弱い）。
        /// </summary>
        public static float MobilizationAppeal(float causeUrgency, float leadershipCharisma, MassMobilizationParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(causeUrgency) * Mathf.Clamp01(leadershipCharisma));
        }

        /// <summary>動員の訴求（既定Params版）。</summary>
        public static float MobilizationAppeal(float causeUrgency, float leadershipCharisma)
            => MobilizationAppeal(causeUrgency, leadershipCharisma, MassMobilizationParams.Default);

        /// <summary>
        /// 参加の閾値（0..1）＝個人のリスク÷(1+効力感)＝この値が高いほど人は動かない（割に合わない）。
        /// 効力感（自分の参加が成果に結びつく感覚）が高いほど閾値が下がり参加しやすくなる。
        /// </summary>
        public static float ParticipationThreshold(float personalRisk, float perceivedEfficacy, MassMobilizationParams p)
        {
            float risk = Mathf.Clamp01(personalRisk);
            float efficacy = Mathf.Clamp01(perceivedEfficacy);
            return Mathf.Clamp01(risk / (1f + efficacy));
        }

        /// <summary>参加の閾値（既定Params版）。</summary>
        public static float ParticipationThreshold(float personalRisk, float perceivedEfficacy)
            => ParticipationThreshold(personalRisk, perceivedEfficacy, MassMobilizationParams.Default);

        /// <summary>
        /// 同調効果（0..1）＝現参加者÷総人口で得た参加比率を非線形に効かせる（人が動くと人が動く）。
        /// =（参加者/人口）^bandwagonExponent＝指数<1で初期の少数参加者の動きが大きく効く。総人口0なら0。
        /// </summary>
        public static float BandwagonEffect(float currentParticipants, float totalPopulation, MassMobilizationParams p)
        {
            float pop = Mathf.Max(0f, totalPopulation);
            if (pop <= 0f) return 0f;
            float ratio = Mathf.Clamp01(Mathf.Max(0f, currentParticipants) / pop);
            return Mathf.Clamp01(Mathf.Pow(ratio, p.bandwagonExponent));
        }

        /// <summary>同調効果（既定Params版）。</summary>
        public static float BandwagonEffect(float currentParticipants, float totalPopulation)
            => BandwagonEffect(currentParticipants, totalPopulation, MassMobilizationParams.Default);

        /// <summary>
        /// 臨界量への到達（0..1）＝同調効果×重み＋訴求×重み＝人の動きと大義が揃うと臨界点へ近づく。
        /// =bandwagonEffect×criticalMassWeight＋mobilizationAppeal×appealWeight。
        /// </summary>
        public static float CriticalMass(float bandwagonEffect, float mobilizationAppeal, MassMobilizationParams p)
        {
            float band = Mathf.Clamp01(bandwagonEffect);
            float appeal = Mathf.Clamp01(mobilizationAppeal);
            return Mathf.Clamp01(band * p.criticalMassWeight + appeal * p.appealWeight);
        }

        /// <summary>臨界量への到達（既定Params版）。</summary>
        public static float CriticalMass(float bandwagonEffect, float mobilizationAppeal)
            => CriticalMass(bandwagonEffect, mobilizationAppeal, MassMobilizationParams.Default);

        /// <summary>
        /// フリーライダー問題（0..1）＝公共財×(1−個人の貢献感)＝みんなやるから自分は要らないと降りる。
        /// 公共財の魅力が大きいほど、かつ自分の貢献が必須でないと感じるほどただ乗りが増える。
        /// </summary>
        public static float FreeRiderProblem(float collectiveGood, float personalContribution, MassMobilizationParams p)
        {
            float good = Mathf.Clamp01(collectiveGood);
            float contribution = Mathf.Clamp01(personalContribution);
            return Mathf.Clamp01(good * (1f - contribution));
        }

        /// <summary>フリーライダー問題（既定Params版）。</summary>
        public static float FreeRiderProblem(float collectiveGood, float personalContribution)
            => FreeRiderProblem(collectiveGood, personalContribution, MassMobilizationParams.Default);

        /// <summary>
        /// ネットワーク増幅（≥1.0）＝社会的紐帯×情報流通で動員が伝播し増幅する（口コミ・弱い紐帯）。
        /// =1＋socialTies×informationFlow×(maxNetworkAmplification−1)、上限 maxNetworkAmplification。
        /// </summary>
        public static float NetworkAmplification(float socialTies, float informationFlow, MassMobilizationParams p)
        {
            float ties = Mathf.Clamp01(socialTies);
            float flow = Mathf.Clamp01(informationFlow);
            float boost = ties * flow * (p.maxNetworkAmplification - 1f);
            return Mathf.Clamp(1f + boost, 1f, p.maxNetworkAmplification);
        }

        /// <summary>ネットワーク増幅（既定Params版）。</summary>
        public static float NetworkAmplification(float socialTies, float informationFlow)
            => NetworkAmplification(socialTies, informationFlow, MassMobilizationParams.Default);

        /// <summary>
        /// 抑圧による抑止（0..1）＝弾圧×(1−訴求)＝国家の弾圧が動員を抑え込む（が大義が強いほど効きにくい）。
        /// =stateRepression×repressionWeight×(1−mobilizationAppeal)＝訴求が高いと弾圧が逆効果になりにくい。
        /// </summary>
        public static float RepressionDeterrence(float stateRepression, float mobilizationAppeal, MassMobilizationParams p)
        {
            float repression = Mathf.Clamp01(stateRepression);
            float appeal = Mathf.Clamp01(mobilizationAppeal);
            return Mathf.Clamp01(repression * p.repressionWeight * (1f - appeal));
        }

        /// <summary>抑圧による抑止（既定Params版）。</summary>
        public static float RepressionDeterrence(float stateRepression, float mobilizationAppeal)
            => RepressionDeterrence(stateRepression, mobilizationAppeal, MassMobilizationParams.Default);

        /// <summary>
        /// 動員規模（0..1）＝臨界量×(1＋増幅寄与)から抑圧の抑止とただ乗りを引いた運動の成立度。
        /// =criticalMass×(1＋(networkAmplification−1)×amplificationWeight)−repressionDeterrence−freeRiderProblem。
        /// 臨界に達しネットワークで増幅されるほど大きく、弾圧とただ乗りが規模を削る。
        /// </summary>
        public static float MobilizationScale(float criticalMass, float networkAmplification,
            float repressionDeterrence, float freeRiderProblem, MassMobilizationParams p)
        {
            float critical = Mathf.Clamp01(criticalMass);
            float amplification = Mathf.Max(1f, networkAmplification);
            float deterrence = Mathf.Clamp01(repressionDeterrence);
            float freeRider = Mathf.Clamp01(freeRiderProblem);
            float amplified = critical * (1f + (amplification - 1f) * p.amplificationWeight);
            return Mathf.Clamp01(amplified - deterrence - freeRider);
        }

        /// <summary>動員規模（既定Params版）。</summary>
        public static float MobilizationScale(float criticalMass, float networkAmplification,
            float repressionDeterrence, float freeRiderProblem)
            => MobilizationScale(criticalMass, networkAmplification, repressionDeterrence, freeRiderProblem,
                MassMobilizationParams.Default);

        /// <summary>
        /// 運動が雪だるま式に立ち上がったか＝動員規模が閾値（既定0.5）を超える（少数の参加が連鎖して拡大）。
        /// </summary>
        public static bool IsSnowballing(float mobilizationScale, float threshold)
        {
            return Mathf.Clamp01(mobilizationScale) > Mathf.Clamp01(threshold);
        }

        /// <summary>運動が雪だるま式に立ち上がったか（既定閾値版）。</summary>
        public static bool IsSnowballing(float mobilizationScale)
            => IsSnowballing(mobilizationScale, MassMobilizationParams.Default.snowballThreshold);
    }
}
