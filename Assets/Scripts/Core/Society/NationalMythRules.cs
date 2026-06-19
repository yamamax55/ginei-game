using UnityEngine;

namespace Ginei
{
    /// <summary>建国神話・国家叙事の調整係数（国民統合の物語）。</summary>
    public readonly struct NationalMythParams
    {
        /// <summary>文化的反復が浸透の底上げに効く重み（0..1。1で文化反復だけで満点まで持ち上げ得る）。</summary>
        public readonly float reinforcementWeight;
        /// <summary>共有アイデンティティが統合に効く重み（0..1）。</summary>
        public readonly float identityWeight;
        /// <summary>修正主義の挑戦で神話の硬直が脆さに効く重み（0..1。硬直した神話は対抗叙事に折れやすい）。</summary>
        public readonly float rigidityWeight;
        /// <summary>神話と現実の緊張で矛盾する事実が浸透を裏返す重み（0..1）。</summary>
        public readonly float tensionWeight;
        /// <summary>教育継続が世代継承に効く重み（0..1。断絶すれば神話は1世代で薄れる）。</summary>
        public readonly float continuityWeight;
        /// <summary>神話が持続するとみなす頑健さの既定閾値（0..1）。</summary>
        public readonly float enduringThreshold;

        public NationalMythParams(float reinforcementWeight, float identityWeight, float rigidityWeight,
                                  float tensionWeight, float continuityWeight, float enduringThreshold)
        {
            this.reinforcementWeight = Mathf.Clamp01(reinforcementWeight);
            this.identityWeight = Mathf.Clamp01(identityWeight);
            this.rigidityWeight = Mathf.Clamp01(rigidityWeight);
            this.tensionWeight = Mathf.Clamp01(tensionWeight);
            this.continuityWeight = Mathf.Clamp01(continuityWeight);
            this.enduringThreshold = Mathf.Clamp01(enduringThreshold);
        }

        /// <summary>既定＝反復0.5・帰属0.6・硬直0.5・緊張0.7・継続0.8・持続閾値0.5。</summary>
        public static NationalMythParams Default => new NationalMythParams(0.5f, 0.6f, 0.5f, 0.7f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 建国神話・国家叙事の純ロジック（国民統合の物語）。教育普及と文化的反復で神話が浸透し（帝国＝ルドルフ神話／
    /// 同盟＝自由への大逃避行）、浸透が共有アイデンティティと結びついて国民統合へ寄与し、建国譚が歴史的正統性を主張する。
    /// 対抗叙事（修正主義）が硬直した神話に挑み、矛盾する事実が浸透と現実の緊張を生む。統合から緊張を引いた叙事の頑健さが
    /// 教育継続を通じて世代を超えて継承される＝頑健さが修正主義の挑戦を上回る間だけ神話は持続する。
    /// 乱数なし・決定論。実効値パターン（入力非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class NationalMythRules
    {
        /// <summary>
        /// 神話の浸透度（0..1）＝教育普及 educationReach を土台に、文化的反復 culturalReinforcement が
        /// reinforcementWeight の重みで底上げする：教育がゼロなら浸透もゼロ（反復だけでは根づかない）、
        /// 教育がある層に反復が効くほど満点へ近づく。
        /// </summary>
        public static float MythPenetration(float educationReach, float culturalReinforcement, NationalMythParams p)
        {
            float edu = Mathf.Clamp01(educationReach);
            float reinforce = Mathf.Clamp01(culturalReinforcement);
            // 教育が土台＝reinforce が effective を 1 へ持ち上げる余地は educationReach に比例。
            return Mathf.Clamp01(edu * Mathf.Lerp(1f, 1f + p.reinforcementWeight, reinforce));
        }

        public static float MythPenetration(float educationReach, float culturalReinforcement)
            => MythPenetration(educationReach, culturalReinforcement, NationalMythParams.Default);

        /// <summary>
        /// 国民統合（0..1）＝神話の浸透 mythPenetration ×共有アイデンティティ sharedIdentity。
        /// identityWeight が帰属の効き具合を決める（帰属が薄くても浸透があれば下限ぶんは統合する）。
        /// </summary>
        public static float NationalCohesion(float mythPenetration, float sharedIdentity, NationalMythParams p)
        {
            float myth = Mathf.Clamp01(mythPenetration);
            float identity = Mathf.Clamp01(sharedIdentity);
            return Mathf.Clamp01(myth * Mathf.Lerp(1f - p.identityWeight, 1f, identity));
        }

        public static float NationalCohesion(float mythPenetration, float sharedIdentity)
            => NationalCohesion(mythPenetration, sharedIdentity, NationalMythParams.Default);

        /// <summary>
        /// 歴史的正統性（0..1）＝神話の浸透 ×建国譚 foundingNarrative の幾何平均（Sqrt）。
        /// どちらか一方だけでは正統性は主張できない＝浸透した神話と説得力ある建国譚が揃って初めて立つ。
        /// </summary>
        public static float HistoricalLegitimacy(float mythPenetration, float foundingNarrative, NationalMythParams p)
        {
            float myth = Mathf.Clamp01(mythPenetration);
            float narrative = Mathf.Clamp01(foundingNarrative);
            return Mathf.Clamp01(Mathf.Sqrt(myth * narrative));
        }

        public static float HistoricalLegitimacy(float mythPenetration, float foundingNarrative)
            => HistoricalLegitimacy(mythPenetration, foundingNarrative, NationalMythParams.Default);

        /// <summary>
        /// 修正主義の挑戦（0..1）＝対抗叙事の強さ counterNarrativeStrength を、神話の硬直 mythRigidity が
        /// rigidityWeight の重みで増幅する：硬直した（更新できない）神話ほど対抗叙事に折れやすく挑戦を許す。
        /// </summary>
        public static float RevisionistChallenge(float counterNarrativeStrength, float mythRigidity, NationalMythParams p)
        {
            float counter = Mathf.Clamp01(counterNarrativeStrength);
            float rigidity = Mathf.Clamp01(mythRigidity);
            return Mathf.Clamp01(counter * Mathf.Lerp(1f, 1f + p.rigidityWeight, rigidity));
        }

        public static float RevisionistChallenge(float counterNarrativeStrength, float mythRigidity)
            => RevisionistChallenge(counterNarrativeStrength, mythRigidity, NationalMythParams.Default);

        /// <summary>
        /// 神話と現実の緊張（0..1）＝神話の浸透 ×矛盾する事実 contradictingFacts ×tensionWeight。
        /// 浸透していない神話は矛盾しても緊張を生まない（誰も信じていない）＝浸透が深いほど現実との齟齬が痛む。
        /// </summary>
        public static float MythRealityTension(float mythPenetration, float contradictingFacts, NationalMythParams p)
        {
            float myth = Mathf.Clamp01(mythPenetration);
            float facts = Mathf.Clamp01(contradictingFacts);
            return Mathf.Clamp01(myth * facts * p.tensionWeight);
        }

        public static float MythRealityTension(float mythPenetration, float contradictingFacts)
            => MythRealityTension(mythPenetration, contradictingFacts, NationalMythParams.Default);

        /// <summary>
        /// 叙事の頑健さ（-1..1）＝国民統合 nationalCohesion から神話と現実の緊張 mythRealityTension を引いた符号付き値。
        /// 正＝統合が緊張を上回り叙事が国を束ねる／負＝緊張が統合を侵食し叙事が破綻へ向かう。
        /// </summary>
        public static float NarrativeResilience(float nationalCohesion, float mythRealityTension, NationalMythParams p)
        {
            float cohesion = Mathf.Clamp01(nationalCohesion);
            float tension = Mathf.Clamp01(mythRealityTension);
            return Mathf.Clamp(cohesion - tension, -1f, 1f);
        }

        public static float NarrativeResilience(float nationalCohesion, float mythRealityTension)
            => NarrativeResilience(nationalCohesion, mythRealityTension, NationalMythParams.Default);

        /// <summary>
        /// 世代継承（0..1）＝叙事の頑健さ narrativeResilience（負は継承ゼロ＝破綻した神話は引き継がれない）を、
        /// 教育継続 educationContinuity が continuityWeight の重みで担う：教育が断絶すれば頑健な神話も次世代で薄れる。
        /// </summary>
        public static float GenerationalTransmission(float narrativeResilience, float educationContinuity, NationalMythParams p)
        {
            float resilience = Mathf.Clamp01(narrativeResilience); // 負の頑健さは継承されない
            float continuity = Mathf.Clamp01(educationContinuity);
            return Mathf.Clamp01(resilience * Mathf.Lerp(1f - p.continuityWeight, 1f, continuity));
        }

        public static float GenerationalTransmission(float narrativeResilience, float educationContinuity)
            => GenerationalTransmission(narrativeResilience, educationContinuity, NationalMythParams.Default);

        /// <summary>
        /// 神話が持続するか＝叙事の頑健さ narrativeResilience が修正主義の挑戦 revisionistChallenge を threshold ぶん
        /// 上回るとき true。頑健さが負（破綻）なら問答無用で false。
        /// </summary>
        public static bool IsMythEnduring(float narrativeResilience, float revisionistChallenge, float threshold)
        {
            float resilience = Mathf.Clamp(narrativeResilience, -1f, 1f);
            float challenge = Mathf.Clamp01(revisionistChallenge);
            if (resilience <= 0f) return false;
            return resilience - challenge >= Mathf.Clamp01(threshold);
        }

        public static bool IsMythEnduring(float narrativeResilience, float revisionistChallenge)
            => IsMythEnduring(narrativeResilience, revisionistChallenge, NationalMythParams.Default.enduringThreshold);
    }
}
