using UnityEngine;

namespace Ginei
{
    /// <summary>作戦会議（軍議＝幕僚合議）の調整係数。</summary>
    public readonly struct WarCouncilParams
    {
        /// <summary>幕僚1人あたりが意見の幅へ与える基礎寄与（多いほど意見が広がる）。</summary>
        public readonly float diversityPerStaff;
        /// <summary>議論の質に占める幕僚の有能さの重み（残りは意見の幅が担う）。</summary>
        public readonly float competenceWeight;
        /// <summary>集団思考の最大弊害（主将の威圧と同調圧力が満タンでこの割合まで議論を腐らせる）。</summary>
        public readonly float groupthinkPenalty;
        /// <summary>作戦の質に占める主将の慧眼の重み（残りは議論×異論が担う）。</summary>
        public readonly float insightWeight;
        /// <summary>幕僚1人あたりの決定遅延（多いほど合議に時間がかかる＝決定速度を下げる）。</summary>
        public readonly float speedPenaltyPerStaff;
        /// <summary>機会損失に占める状況の切迫の重み（切迫した局面ほど遅延が高くつく）。</summary>
        public readonly float urgencyWeight;
        /// <summary>作戦が妥当とみなす既定の作戦の質しきい値。</summary>
        public readonly float soundThreshold;

        public WarCouncilParams(float diversityPerStaff, float competenceWeight, float groupthinkPenalty,
                                float insightWeight, float speedPenaltyPerStaff, float urgencyWeight,
                                float soundThreshold)
        {
            this.diversityPerStaff = Mathf.Max(0f, diversityPerStaff);
            this.competenceWeight = Mathf.Clamp01(competenceWeight);
            this.groupthinkPenalty = Mathf.Clamp01(groupthinkPenalty);
            this.insightWeight = Mathf.Clamp01(insightWeight);
            this.speedPenaltyPerStaff = Mathf.Max(0f, speedPenaltyPerStaff);
            this.urgencyWeight = Mathf.Clamp01(urgencyWeight);
            this.soundThreshold = Mathf.Clamp01(soundThreshold);
        }

        /// <summary>既定＝幕僚寄与0.12/有能さ重み0.5/集団思考弊害0.6/慧眼重み0.4/決定遅延0.08/切迫重み1.0/妥当しきい値0.5。</summary>
        public static WarCouncilParams Default =>
            new WarCouncilParams(0.12f, 0.5f, 0.6f, 0.4f, 0.08f, 1f, 0.5f);
    }

    /// <summary>
    /// 作戦会議（軍議＝幕僚合議の質と意思決定）の純ロジック。幕僚の意見の多様性、議論による作戦の質の向上、
    /// 集団思考（同調圧力）の弊害、主将の決断、会議の長さと機会損失、異論の価値を扱う＝銀英伝の幕僚会議を想定。
    /// 多様な経歴の幕僚が活発に議論すれば作戦の質は上がるが、主将が威圧し同調圧力が強いと集団思考に陥り異論が死ぬ。
    /// 幕僚が多いほど合議は遅く、切迫した局面では遅延が機会損失になる＝決断力ある主将は速やかに裁く。
    /// 倍率・指標は基準値に掛けて使う想定（実効値パターン・基準非破壊）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WarCouncilRules
    {
        /// <summary>
        /// 意見の多様性（0..1）＝幕僚数×経歴の多様性。幕僚が多く経歴が多彩なほど意見の幅が広がる。
        /// staffCount は非負、backgroundVariety 0..1。多様性ゼロ（経歴が一様）なら頭数だけ多くても幅は出ない。
        /// </summary>
        public static float OpinionDiversity(int staffCount, float backgroundVariety, WarCouncilParams p)
        {
            float count = Mathf.Max(0, staffCount);
            float variety = Mathf.Clamp01(backgroundVariety);
            return Mathf.Clamp01(count * p.diversityPerStaff * variety);
        }

        public static float OpinionDiversity(int staffCount, float backgroundVariety)
            => OpinionDiversity(staffCount, backgroundVariety, WarCouncilParams.Default);

        /// <summary>
        /// 議論の質（0..1）＝意見の幅×幕僚の有能さ。意見の幅を、有能さで底上げした係数で評価する＝
        /// 幅広い意見も無能な幕僚では磨かれず、有能なら少ない幅でも質を引き出す。competenceWeight が
        /// 有能さの効き具合（残りは無条件で効く）。
        /// </summary>
        public static float DeliberationQuality(float opinionDiversity, float staffCompetence, WarCouncilParams p)
        {
            float diversity = Mathf.Clamp01(opinionDiversity);
            float comp = Mathf.Clamp01(staffCompetence);
            float gain = Mathf.Lerp(1f - p.competenceWeight, 1f, comp);
            return Mathf.Clamp01(diversity * gain);
        }

        public static float DeliberationQuality(float opinionDiversity, float staffCompetence)
            => DeliberationQuality(opinionDiversity, staffCompetence, WarCouncilParams.Default);

        /// <summary>
        /// 集団思考の弊害（0..1）＝主将の威圧×同調圧力。主将が異論を許さず同調圧力が強いほど、
        /// 反対意見が出ず集団思考に陥る。両者の積を groupthinkPenalty 幅で効かせる＝片方でも低ければ崩れる。
        /// </summary>
        public static float Groupthink(float commanderDominance, float conformityPressure, WarCouncilParams p)
        {
            float dominance = Mathf.Clamp01(commanderDominance);
            float pressure = Mathf.Clamp01(conformityPressure);
            return Mathf.Clamp01(dominance * pressure * p.groupthinkPenalty);
        }

        public static float Groupthink(float commanderDominance, float conformityPressure)
            => Groupthink(commanderDominance, conformityPressure, WarCouncilParams.Default);

        /// <summary>
        /// 異論の価値（0..1）＝意見の幅×(1-集団思考)。多様な意見があっても集団思考が支配すれば異論は死ぬ。
        /// 集団思考が無ければ意見の幅がそのまま異論として活きる＝忌憚なき進言が作戦を救う。
        /// </summary>
        public static float DissentValue(float opinionDiversity, float groupthink, WarCouncilParams p)
        {
            float diversity = Mathf.Clamp01(opinionDiversity);
            float gt = Mathf.Clamp01(groupthink);
            return Mathf.Clamp01(diversity * (1f - gt));
        }

        public static float DissentValue(float opinionDiversity, float groupthink)
            => DissentValue(opinionDiversity, groupthink, WarCouncilParams.Default);

        /// <summary>
        /// 作戦の質（0..1）＝議論×異論×主将の慧眼。議論の質と異論の価値の相乗（幾何平均）を土台に、
        /// 主将の慧眼を insightWeight 幅で底上げする＝合議が割れても明察な主将が良案を掬い上げる。
        /// </summary>
        public static float PlanQuality(float deliberationQuality, float dissentValue, float commanderInsight,
                                        WarCouncilParams p)
        {
            float delib = Mathf.Clamp01(deliberationQuality);
            float dissent = Mathf.Clamp01(dissentValue);
            float insight = Mathf.Clamp01(commanderInsight);
            float core = Mathf.Sqrt(delib * dissent);
            // 慧眼で底上げ＝核を (1-w) で効かせ、残り w を慧眼が担う
            return Mathf.Clamp01(core * (1f - p.insightWeight) + insight * p.insightWeight);
        }

        public static float PlanQuality(float deliberationQuality, float dissentValue, float commanderInsight)
            => PlanQuality(deliberationQuality, dissentValue, commanderInsight, WarCouncilParams.Default);

        /// <summary>
        /// 決定の速さ（0..1）＝幕僚が多いほど合議に時間がかかり遅く、主将の決断力がそれを取り戻す。
        /// 基準1.0から幕僚数×speedPenaltyPerStaff ぶん遅くし、決断力で線形に速める＝即断の主将は会議を短く裁く。
        /// </summary>
        public static float DecisionSpeed(int staffCount, float commanderDecisiveness, WarCouncilParams p)
        {
            float count = Mathf.Max(0, staffCount);
            float decisive = Mathf.Clamp01(commanderDecisiveness);
            float baseSpeed = Mathf.Clamp01(1f - count * p.speedPenaltyPerStaff);
            return Mathf.Lerp(baseSpeed, 1f, decisive);
        }

        public static float DecisionSpeed(int staffCount, float commanderDecisiveness)
            => DecisionSpeed(staffCount, commanderDecisiveness, WarCouncilParams.Default);

        /// <summary>
        /// 機会損失（0..1）＝決定の遅さ×状況の切迫。決定が遅い(1-speed)ほど、かつ局面が切迫しているほど、
        /// 好機を逃す。切迫していなければ（urgency 0）遅くとも損失は小さい＝悠長な軍議は急場で命取り。
        /// </summary>
        public static float OpportunityCost(float decisionSpeed, float situationUrgency, WarCouncilParams p)
        {
            float slow = 1f - Mathf.Clamp01(decisionSpeed);
            float urgency = Mathf.Lerp(1f - p.urgencyWeight, 1f, Mathf.Clamp01(situationUrgency));
            return Mathf.Clamp01(slow * urgency);
        }

        public static float OpportunityCost(float decisionSpeed, float situationUrgency)
            => OpportunityCost(decisionSpeed, situationUrgency, WarCouncilParams.Default);

        /// <summary>作戦が妥当か＝作戦の質が threshold 以上なら true（採るに足る作戦）。</summary>
        public static bool IsPlanSound(float planQuality, float threshold)
            => Mathf.Clamp01(planQuality) >= Mathf.Clamp01(threshold);

        /// <summary>既定しきい値（<see cref="WarCouncilParams.Default"/> の soundThreshold）で妥当か判定する委譲版。</summary>
        public static bool IsPlanSound(float planQuality)
            => IsPlanSound(planQuality, WarCouncilParams.Default.soundThreshold);
    }
}
