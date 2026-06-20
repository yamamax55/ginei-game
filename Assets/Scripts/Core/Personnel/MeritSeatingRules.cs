using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 義侠衆議＝功績本位の座次（席次）と集団意思決定（SHZ-5 #1362・水滸伝/梁山泊型）。
    /// 「天罡地煞」の序列＝家柄や年功でなく功績・人望で席次が決まり、
    /// 重要事は頭領の独断でなく好漢たちの衆議で決まる。
    /// 純ロジック（非MonoBehaviour・決定論）。基準値非破壊＝倍率/スコアを返す。
    /// 分担：
    ///  - <c>SeniorityRules</c>（席次の固さ・政体別rigidity）とは別＝こちらは功績型の流動的序列。
    ///  - <c>LeadershipElectionRules</c>（総裁選の票集計）とは別＝こちらは梁山泊型の衆議。
    ///  - <c>OutlawOrganizationRules</c>（同EPIC SHZ・義賊の結束/存続）とは別＝座次と意思決定に特化。
    /// </summary>
    public static class MeritSeatingRules
    {
        /// <summary>座次スコア＝功績・人望・年功の加重合成（功績重視＝年功より功績が重い）。0..1。</summary>
        public static float SeatingRank(float meritScore, float reputationScore, float seniorityScore, MeritSeatingParams p)
        {
            float m = Mathf.Clamp01(meritScore);
            float r = Mathf.Clamp01(reputationScore);
            float s = Mathf.Clamp01(seniorityScore);
            return Mathf.Clamp01(m * p.meritWeight + r * p.reputationWeight + s * p.seniorityWeight);
        }
        public static float SeatingRank(float meritScore, float reputationScore, float seniorityScore)
            => SeatingRank(meritScore, reputationScore, seniorityScore, MeritSeatingParams.Default);

        /// <summary>席次争い＝座次の近い者同士ほど対立しやすい。0..1（差0で最大）。</summary>
        public static float SeatRivalry(float rankA, float rankB, MeritSeatingParams p)
        {
            float a = Mathf.Clamp01(rankA);
            float b = Mathf.Clamp01(rankB);
            float gap = Mathf.Abs(a - b);
            // 差が小さいほど対立↑。peakRivalry を頂点に gap で線形減衰。
            return Mathf.Clamp01(p.peakRivalry * (1f - gap));
        }
        public static float SeatRivalry(float rankA, float rankB)
            => SeatRivalry(rankA, rankB, MeritSeatingParams.Default);

        /// <summary>衆議での一票の重み＝上位ほど重いが独裁ではない。0..1。</summary>
        public static float CollectiveDecisionWeight(float memberRank, int memberCount, MeritSeatingParams p)
        {
            float rank = Mathf.Clamp01(memberRank);
            int n = Mathf.Max(1, memberCount);
            float equalShare = 1f / n;                       // 平等な一票
            float rankShare = rank * p.rankInfluence;        // 座次による上乗せ
            return Mathf.Clamp01(equalShare + rankShare);
        }
        public static float CollectiveDecisionWeight(float memberRank, int memberCount)
            => CollectiveDecisionWeight(memberRank, memberCount, MeritSeatingParams.Default);

        /// <summary>合意度＝賛同割合の配列の最大シェア（割れるほど低い）。null/空は0。0..1。</summary>
        public static float ConsensusLevel(float[] supportShares)
        {
            if (supportShares == null || supportShares.Length == 0) return 0f;
            float max = 0f;
            for (int i = 0; i < supportShares.Length; i++)
            {
                float v = Mathf.Clamp01(supportShares[i]);
                if (v > max) max = v;
            }
            return max;
        }

        /// <summary>決定の正統性＝衆議の合意×頭領の裁可。0..1。</summary>
        public static float DecisionLegitimacy(float consensusLevel, float leaderEndorsement, MeritSeatingParams p)
        {
            float c = Mathf.Clamp01(consensusLevel);
            float e = Mathf.Clamp01(leaderEndorsement);
            return Mathf.Clamp01(c * p.consensusWeight + e * p.endorsementWeight);
        }
        public static float DecisionLegitimacy(float consensusLevel, float leaderEndorsement)
            => DecisionLegitimacy(consensusLevel, leaderEndorsement, MeritSeatingParams.Default);

        /// <summary>座次流動性＝新たな功績で座次が上がりうる度合い（功績本位＝下剋上可）。0..1。</summary>
        public static float MeritMobility(float currentRank, float newMerit, MeritSeatingParams p)
        {
            float rank = Mathf.Clamp01(currentRank);
            float merit = Mathf.Clamp01(newMerit);
            // 現座次が低く新功績が高いほど上昇余地大。功績本位度で増幅。
            float headroom = 1f - rank;
            return Mathf.Clamp01(headroom * merit * p.meritWeight);
        }
        public static float MeritMobility(float currentRank, float newMerit)
            => MeritMobility(currentRank, newMerit, MeritSeatingParams.Default);

        /// <summary>義兄弟の結束＝公正な座次ほど結束が強い（功で報いる）。0..1。</summary>
        public static float BrotherhoodCohesion(float sharedCause, float fairSeating, MeritSeatingParams p)
        {
            float cause = Mathf.Clamp01(sharedCause);
            float fair = Mathf.Clamp01(fairSeating);
            return Mathf.Clamp01(cause * p.causeWeight + fair * p.fairnessWeight);
        }
        public static float BrotherhoodCohesion(float sharedCause, float fairSeating)
            => BrotherhoodCohesion(sharedCause, fairSeating, MeritSeatingParams.Default);

        /// <summary>功績本位度が閾値以上なら正統な序列と判定。</summary>
        public static bool IsLegitimateSeating(float meritWeight, float threshold)
            => Mathf.Clamp01(meritWeight) >= Mathf.Clamp01(threshold);
    }

    /// <summary>座次・衆議の調整値（功績重視）。全フィールドをコンストラクタで Clamp。</summary>
    public readonly struct MeritSeatingParams
    {
        public readonly float meritWeight;       // 功績の重み（最大）
        public readonly float reputationWeight;  // 人望の重み
        public readonly float seniorityWeight;   // 年功の重み（最小）
        public readonly float peakRivalry;       // 席次争いの頂点強度
        public readonly float rankInfluence;     // 衆議での座次上乗せ係数
        public readonly float consensusWeight;   // 正統性での合意の重み
        public readonly float endorsementWeight; // 正統性での裁可の重み
        public readonly float causeWeight;       // 結束での大義の重み
        public readonly float fairnessWeight;    // 結束での公正の重み

        public MeritSeatingParams(
            float meritWeight, float reputationWeight, float seniorityWeight,
            float peakRivalry, float rankInfluence,
            float consensusWeight, float endorsementWeight,
            float causeWeight, float fairnessWeight)
        {
            this.meritWeight = Mathf.Clamp01(meritWeight);
            this.reputationWeight = Mathf.Clamp01(reputationWeight);
            this.seniorityWeight = Mathf.Clamp01(seniorityWeight);
            this.peakRivalry = Mathf.Clamp01(peakRivalry);
            this.rankInfluence = Mathf.Clamp01(rankInfluence);
            this.consensusWeight = Mathf.Clamp01(consensusWeight);
            this.endorsementWeight = Mathf.Clamp01(endorsementWeight);
            this.causeWeight = Mathf.Clamp01(causeWeight);
            this.fairnessWeight = Mathf.Clamp01(fairnessWeight);
        }

        // 既定＝功績0.6/人望0.3/年功0.1（年功より功績が重い）。
        public static MeritSeatingParams Default => new MeritSeatingParams(
            0.6f, 0.3f, 0.1f,
            0.8f, 0.5f,
            0.7f, 0.3f,
            0.5f, 0.5f);
    }
}
