using UnityEngine;

namespace Ginei
{
    /// <summary>資質本位の後継指名（HDRN-2 #1804）の調整係数。</summary>
    public readonly struct HeirDesignationParams
    {
        /// <summary>資質スコアにおける能力(competence)の重み。</summary>
        public readonly float competenceWeight;
        /// <summary>資質スコアにおける忠誠(loyalty)の重み。</summary>
        public readonly float loyaltyWeight;
        /// <summary>資質スコアにおける活力(vigor)の重み。</summary>
        public readonly float vigorWeight;
        /// <summary>資質が法順位を覆す感度（大きいほど少しの資質差で法順位を無視できる）。</summary>
        public readonly float meritOverrideSensitivity;
        /// <summary>指名養子の成立に対する血統距離ペナルティ係数（血が遠いほど成立しにくい）。</summary>
        public readonly float bloodlinePenalty;
        /// <summary>正統性論争のスケール（指名が法順位を飛び越すほど論争が大きくなる上限）。</summary>
        public readonly float contestScale;
        /// <summary>継承危機リスクのスケール（論争×対抗者の強さの上限）。</summary>
        public readonly float crisisScale;

        public HeirDesignationParams(float competenceWeight, float loyaltyWeight, float vigorWeight,
                                     float meritOverrideSensitivity, float bloodlinePenalty,
                                     float contestScale, float crisisScale)
        {
            this.competenceWeight = Mathf.Clamp01(competenceWeight);
            this.loyaltyWeight = Mathf.Clamp01(loyaltyWeight);
            this.vigorWeight = Mathf.Clamp01(vigorWeight);
            this.meritOverrideSensitivity = Mathf.Clamp01(meritOverrideSensitivity);
            this.bloodlinePenalty = Mathf.Clamp01(bloodlinePenalty);
            this.contestScale = Mathf.Clamp01(contestScale);
            this.crisisScale = Mathf.Clamp01(crisisScale);
        }

        /// <summary>既定＝能力0.5・忠誠0.25・活力0.25・覆し感度0.6・血統ペナ0.7・論争1.0・危機1.0。</summary>
        public static HeirDesignationParams Default
            => new HeirDesignationParams(0.5f, 0.25f, 0.25f, 0.6f, 0.7f, 1.0f, 1.0f);
    }

    /// <summary>
    /// 資質本位の後継指名の純ロジック（HDRN-2 #1804）。法定の継承順位（長子相続等）でなく、
    /// 資質スコア（能力×忠誠×活力）で後継者を選ぶ＝ローマの養子皇帝のように、血統外でも有能な者を
    /// <b>指名養子</b>として後継に据える。資質で選べば有能だが、法順位を覆すほど<b>正統性論争・継承危機</b>を招く
    /// ＝有能さと正統性のトレードオフ。
    /// <see cref="SuccessionLawRules"/>（継承法＝長子/分割/指名/選挙の取り分・継承危機リスク）とは別＝
    /// 資質スコアでの後継選定そのものに特化。<see cref="InheritanceRules"/>（資産・封土の相続）とも別＝
    /// 元首の後継<b>指名</b>。同EPIC HDRN の <see cref="SuccessionQualityRules"/>（後継の質）・
    /// <see cref="AbdicationRules"/>（譲位）と接続する想定。<see cref="VacancyRules"/>（役職の後任補充）とも別。
    /// 盤面非依存のplain引数・乱数は roll 引数で決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class HeirDesignationRules
    {
        /// <summary>
        /// 候補の資質スコア（0..1）＝能力×competenceWeight＋忠誠×loyaltyWeight＋活力×vigorWeight の加重和。
        /// 重みの合計で正規化（合計0なら0）。＝有能・忠実・壮健なほど高い。
        /// </summary>
        public static float CandidateScore(float competence, float loyalty, float vigor, HeirDesignationParams p)
        {
            float c = Mathf.Clamp01(competence);
            float l = Mathf.Clamp01(loyalty);
            float v = Mathf.Clamp01(vigor);
            float wsum = p.competenceWeight + p.loyaltyWeight + p.vigorWeight;
            if (wsum <= 0f) return 0f;
            float raw = c * p.competenceWeight + l * p.loyaltyWeight + v * p.vigorWeight;
            return Mathf.Clamp01(raw / wsum);
        }

        public static float CandidateScore(float competence, float loyalty, float vigor)
            => CandidateScore(competence, loyalty, vigor, HeirDesignationParams.Default);

        /// <summary>
        /// 2候補のうち資質の高い方（-1=A、0=同等、1=B）。資質本位の選定＝法順位でなくスコアで決める。
        /// </summary>
        public static int BestCandidate(float scoreA, float scoreB)
        {
            float a = Mathf.Clamp01(scoreA);
            float b = Mathf.Clamp01(scoreB);
            if (a > b) return -1;
            if (b > a) return 1;
            return 0;
        }

        /// <summary>
        /// 資質が法順位を覆して選ばれる度合い（0..1）。資質スコア meritScore(0..1) が高く、法順位 lineageRank
        /// （0=法定の筆頭、1=末位ほど劣後）が劣後しているほど大きい。＝資質差が大きいほど法順位を無視できる。
        /// override = clamp01( (meritScore − (1−lineageRank)) × 感度 ＋ meritScore×lineageRank )。
        /// 単純化：法順位が劣後(lineageRank大)で資質が高いほど、覆しの度合いが上がる。
        /// </summary>
        public static float MeritOverLineage(float meritScore, float lineageRank, HeirDesignationParams p)
        {
            float m = Mathf.Clamp01(meritScore);
            float r = Mathf.Clamp01(lineageRank);   // 0=筆頭・1=末位
            // 法定後継が当然に選ばれる「正統スコア」=1−r。それを資質で上回るぶん×感度。
            float legitClaim = 1f - r;
            float excess = Mathf.Max(0f, m - legitClaim);
            return Mathf.Clamp01(excess * p.meritOverrideSensitivity + m * r * p.meritOverrideSensitivity);
        }

        public static float MeritOverLineage(float meritScore, float lineageRank)
            => MeritOverLineage(meritScore, lineageRank, HeirDesignationParams.Default);

        /// <summary>
        /// 指名養子（血統外）の成立度（0..1）＝候補の資質 candidateMerit(0..1) が血統距離 bloodlineDistance(0..1)
        /// のペナルティを補う。viability = clamp01( candidateMerit − bloodlineDistance×bloodlinePenalty )。
        /// ＝資質が高ければ血の遠さを補って養子継承が成立する（ローマの養子皇帝）。
        /// </summary>
        public static float AdoptionViability(float candidateMerit, float bloodlineDistance, HeirDesignationParams p)
        {
            float m = Mathf.Clamp01(candidateMerit);
            float d = Mathf.Clamp01(bloodlineDistance);
            return Mathf.Clamp01(m - d * p.bloodlinePenalty);
        }

        public static float AdoptionViability(float candidateMerit, float bloodlineDistance)
            => AdoptionViability(candidateMerit, bloodlineDistance, HeirDesignationParams.Default);

        /// <summary>
        /// 正統性論争（-1..0）。指名された後継の法順位 designatedHeirRank が、法定後継の順位 legalHeirRank を
        /// 飛び越すほど（=指名側のほうが劣後しているほど）論争が大きい（負へ）。飛び越さない（指名=法定 or 上位）なら0。
        /// contest = −clamp01( (designatedHeirRank − legalHeirRank) × contestScale )。
        /// </summary>
        public static float LegitimacyContest(float designatedHeirRank, float legalHeirRank, HeirDesignationParams p)
        {
            float d = Mathf.Clamp01(designatedHeirRank);   // 0=筆頭・1=末位
            float lg = Mathf.Clamp01(legalHeirRank);
            float leap = Mathf.Max(0f, d - lg);            // 指名が法定より劣後＝飛び越し量
            return -Mathf.Clamp01(leap * p.contestScale);
        }

        public static float LegitimacyContest(float designatedHeirRank, float legalHeirRank)
            => LegitimacyContest(designatedHeirRank, legalHeirRank, HeirDesignationParams.Default);

        /// <summary>
        /// 継承危機リスク（0..1）＝正統性論争の大きさ |legitimacyContest|(-1..0や-1..1を絶対値で) ×
        /// 対抗者の強さ rivalClaimantStrength(0..1) × crisisScale。論争が大きく対抗者が強いほど危機。
        /// </summary>
        public static float SuccessionCrisisRisk(float legitimacyContest, float rivalClaimantStrength, HeirDesignationParams p)
        {
            float contest = Mathf.Clamp01(Mathf.Abs(legitimacyContest));
            float rival = Mathf.Clamp01(rivalClaimantStrength);
            return Mathf.Clamp01(contest * rival * p.crisisScale);
        }

        public static float SuccessionCrisisRisk(float legitimacyContest, float rivalClaimantStrength)
            => SuccessionCrisisRisk(legitimacyContest, rivalClaimantStrength, HeirDesignationParams.Default);

        /// <summary>
        /// 指名の安定度（0..1）＝後継の資質 heirMerit(0..1) と宮廷の合意 courtConsensus(0..1) の積。
        /// 資質が高く宮廷が合意しているほど指名は安定する（どちらか欠ければ不安定）。
        /// </summary>
        public static float DesignationStability(float heirMerit, float courtConsensus)
        {
            return Mathf.Clamp01(Mathf.Clamp01(heirMerit) * Mathf.Clamp01(courtConsensus));
        }

        /// <summary>
        /// 有能さと正統性のトレードオフ（-1..1）＝資質で選ぶ利得 meritGain(0..1) と、法順位を覆す代償
        /// legitimacyLoss(0..1) の差。正＝資質本位が割に合う／負＝正統性の損失が利得を上回る。
        /// </summary>
        public static float MeritVsStabilityTradeoff(float meritGain, float legitimacyLoss)
        {
            float g = Mathf.Clamp01(meritGain);
            float l = Mathf.Clamp01(legitimacyLoss);
            return Mathf.Clamp(g - l, -1f, 1f);
        }

        /// <summary>
        /// 資質本位の継承か（bool）＝資質の重み meritWeight(0..1) が閾値 threshold(0..1) 以上なら true。
        /// ＝法順位より資質を優先する体制かどうか（ローマ養子皇帝＝高／長子相続の絶対王政＝低）。
        /// </summary>
        public static bool IsMeritBasedSuccession(float meritWeight, float threshold)
        {
            return Mathf.Clamp01(meritWeight) >= Mathf.Clamp01(threshold);
        }
    }
}
