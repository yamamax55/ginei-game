using UnityEngine;

namespace Ginei
{
    /// <summary>将官選抜の調整係数（提督登用の適性評価と任地配置）。</summary>
    public readonly struct FlagOfficerSelectionParams
    {
        /// <summary>指揮適性で戦術技能に与える重み。</summary>
        public readonly float tacticalWeight;
        /// <summary>指揮適性で統率に与える重み。</summary>
        public readonly float leadershipWeight;
        /// <summary>指揮適性で経験に与える重み。</summary>
        public readonly float experienceWeight;
        /// <summary>専門相性が任務適合に与える効き（0=相性無視／1=相性が支配的）。</summary>
        public readonly float specialtyWeight;
        /// <summary>派閥のしがらみが信頼性審査を割り引く強さ（しがらみが信頼を蝕む度合い）。</summary>
        public readonly float factionDragWeight;
        /// <summary>抜擢リスクの最大値（読み違えた抜擢が最も重い局面で被る損失の上限）。</summary>
        public readonly float maxBoldRisk;
        /// <summary>コネ登用の弊害の最大値（実力差を派閥で覆して無能を引き上げる害の上限）。</summary>
        public readonly float maxCronyism;
        /// <summary>登用に適すと判ずる既定閾値（NetSelectionValue がこれ以上で適任）。</summary>
        public readonly float suitableThreshold;

        public FlagOfficerSelectionParams(
            float tacticalWeight,
            float leadershipWeight,
            float experienceWeight,
            float specialtyWeight,
            float factionDragWeight,
            float maxBoldRisk,
            float maxCronyism,
            float suitableThreshold)
        {
            this.tacticalWeight = Mathf.Max(0f, tacticalWeight);
            this.leadershipWeight = Mathf.Max(0f, leadershipWeight);
            this.experienceWeight = Mathf.Max(0f, experienceWeight);
            this.specialtyWeight = Mathf.Clamp01(specialtyWeight);
            this.factionDragWeight = Mathf.Clamp01(factionDragWeight);
            this.maxBoldRisk = Mathf.Clamp01(maxBoldRisk);
            this.maxCronyism = Mathf.Clamp01(maxCronyism);
            this.suitableThreshold = Mathf.Clamp(suitableThreshold, -1f, 1f);
        }

        /// <summary>既定＝戦術0.4/統率0.4/経験0.2・専門相性0.5・派閥0.5・抜擢リスク上限0.4・コネ害上限0.5・適任閾値0.1。</summary>
        public static FlagOfficerSelectionParams Default
            => new FlagOfficerSelectionParams(0.4f, 0.4f, 0.2f, 0.5f, 0.5f, 0.4f, 0.5f, 0.1f);
    }

    /// <summary>
    /// 将官選抜＝提督登用の純ロジック。誰を提督に引き上げ、どこへ配するかを決める評価層。
    /// 指揮適性の総合評価（戦術×統率×経験）、任務との専門相性、忠誠と清廉の信頼性審査（派閥のしがらみで割引）、
    /// 抜擢か無難かのリスク（読み切れぬ将を重い局面に賭ける危うさ）、コネ登用の弊害（実力差を派閥で覆す害）を
    /// 合算して最終の登用価値（-1..1）を返す＝適材適所と情実人事のせめぎ合い。
    /// 分担：<see cref="OperationalAptitudeRules"/> が「提督×戦闘類型」の得手不得手（適性等級）、
    /// <see cref="CommandAcumenRules"/> が会戦中の運営・情報の係数、本ルールは「登用そのものの可否」を扱う。
    /// 乱数なし決定論・全入力クランプ・実効値パターン（基準非破壊）・配列安全。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FlagOfficerSelectionRules
    {
        /// <summary>能力値の上限（提督能力と同じ 0..100 スケール）。</summary>
        public const float MaxStatValue = 100f;

        /// <summary>
        /// 指揮適性の総合評価(0..1)＝戦術技能×統率×経験の重み付き平均（重みは正規化）。
        /// 三拍子そろった将が最も高く、どれかが欠ければ均して落ちる。全入力 0..100。重み総和0なら等重み平均。
        /// </summary>
        public static float CommandAptitude(float tacticalSkill, float leadership, float experience, FlagOfficerSelectionParams p)
        {
            float tac = Mathf.Clamp(tacticalSkill, 0f, MaxStatValue) / MaxStatValue;
            float lead = Mathf.Clamp(leadership, 0f, MaxStatValue) / MaxStatValue;
            float exp = Mathf.Clamp(experience, 0f, MaxStatValue) / MaxStatValue;

            float wt = p.tacticalWeight;
            float wl = p.leadershipWeight;
            float we = p.experienceWeight;
            float sum = wt + wl + we;
            if (sum <= 0f)
                return Mathf.Clamp01((tac + lead + exp) / 3f);
            return Mathf.Clamp01((tac * wt + lead * wl + exp * we) / sum);
        }

        public static float CommandAptitude(float tacticalSkill, float leadership, float experience)
            => CommandAptitude(tacticalSkill, leadership, experience, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// 任務適合(0..1)＝指揮適性×任務との専門相性。
        /// 専門相性(0..1)を specialtyWeight ぶん混ぜた倍率を適性に掛ける＝得意な任務ほど活き、畑違いなら割り引く。
        /// missionType は将来の任務種別ぶんけ用（現状は specialtyMatch に集約され値に影響しない）。
        /// </summary>
        public static float MissionFit(float aptitude, int missionType, float specialtyMatch, FlagOfficerSelectionParams p)
        {
            float apt = Mathf.Clamp01(aptitude);
            float match = Mathf.Clamp01(specialtyMatch);
            // 相性 0.5 を中庸として、specialtyWeight ぶん相性を効かせる倍率（0..1+w の範囲）。
            float fitFactor = Mathf.Lerp(1f, match * 2f, p.specialtyWeight);
            if (fitFactor < 0f) fitFactor = 0f;
            return Mathf.Clamp01(apt * fitFactor);
        }

        public static float MissionFit(float aptitude, int missionType, float specialtyMatch)
            => MissionFit(aptitude, missionType, specialtyMatch, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// 信頼性審査(0..1)＝忠誠×清廉を、派閥のしがらみで割り引いたもの。
        /// 忠誠と清廉の幾何平均（どちらかが低いと崩れる）に、派閥のしがらみ(0..1)が factionDragWeight ぶん減点を入れる。
        /// 忠勇でも汚職なら信を置けず、清廉でも派閥に縛られれば任を託しにくい。全入力 0..100（factionTies は 0..1）。
        /// </summary>
        public static float TrustworthinessVetting(float loyalty, float integrity, float factionTies, FlagOfficerSelectionParams p)
        {
            float loy = Mathf.Clamp(loyalty, 0f, MaxStatValue) / MaxStatValue;
            float intg = Mathf.Clamp(integrity, 0f, MaxStatValue) / MaxStatValue;
            float ties = Mathf.Clamp01(factionTies);

            // 忠誠×清廉の幾何平均（一方が崩れれば全体が崩れる）。
            float core = Mathf.Sqrt(Mathf.Max(0f, loy) * Mathf.Max(0f, intg));
            // 派閥のしがらみで割引（しがらみが強いほど信を割り引く）。
            float drag = 1f - ties * p.factionDragWeight;
            return Mathf.Clamp01(core * Mathf.Clamp01(drag));
        }

        public static float TrustworthinessVetting(float loyalty, float integrity, float factionTies)
            => TrustworthinessVetting(loyalty, integrity, factionTies, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// 選抜スコア(0..1)＝任務適合×信頼性審査。能力と信頼の両輪がそろって初めて高くなる。
        /// 任務に適っても信が置けねば登用に値せず、信があっても任に堪えねば同じ（積で両立を要求）。
        /// </summary>
        public static float SelectionScore(float missionFit, float trustworthiness, FlagOfficerSelectionParams p)
        {
            float fit = Mathf.Clamp01(missionFit);
            float trust = Mathf.Clamp01(trustworthiness);
            return Mathf.Clamp01(fit * trust);
        }

        public static float SelectionScore(float missionFit, float trustworthiness)
            => SelectionScore(missionFit, trustworthiness, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// 抜擢のリスク(0..maxBoldRisk)＝適性の不確かさ×局面の重さ。
        /// 実績の薄い将（不確かさ大）を重大な局面（stakes 大）へ賭けるほど跳ね返りが大きい。
        /// 確かな将や軽い局面ならリスクは小さい。両入力 0..1。
        /// </summary>
        public static float BoldPickRisk(float aptitudeUncertainty, float stakes, FlagOfficerSelectionParams p)
        {
            float u = Mathf.Clamp01(aptitudeUncertainty);
            float s = Mathf.Clamp01(stakes);
            return Mathf.Clamp01(u * s) * p.maxBoldRisk;
        }

        public static float BoldPickRisk(float aptitudeUncertainty, float stakes)
            => BoldPickRisk(aptitudeUncertainty, stakes, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// コネ登用の弊害(0..maxCronyism)＝派閥コネ×実力差。
        /// 派閥のコネ(0..1)が強く、適任者との実力差(0..1)が大きいほど害が大きい＝無能を情実で引き上げる損失。
        /// 実力差が無ければ（コネで選んでも適材なら）害ゼロ、コネが無ければ実力で選んだので害ゼロ。
        /// </summary>
        public static float CronyismPenalty(float factionTies, float meritGap, FlagOfficerSelectionParams p)
        {
            float ties = Mathf.Clamp01(factionTies);
            float gap = Mathf.Clamp01(meritGap);
            return Mathf.Clamp01(ties * gap) * p.maxCronyism;
        }

        public static float CronyismPenalty(float factionTies, float meritGap)
            => CronyismPenalty(factionTies, meritGap, FlagOfficerSelectionParams.Default);

        /// <summary>
        /// 最終の登用価値(-1..1)＝選抜スコア −抜擢リスク −コネ害。
        /// 能力・信頼の値打ちから、賭けの危うさと情実の害を差し引いた正味。正＝引き上げる値打ちあり／負＝登用は害。
        /// </summary>
        public static float NetSelectionValue(float selectionScore, float boldPickRisk, float cronyismPenalty, FlagOfficerSelectionParams p)
        {
            float score = Mathf.Clamp01(selectionScore);
            float risk = Mathf.Clamp01(boldPickRisk);
            float crony = Mathf.Clamp01(cronyismPenalty);
            return Mathf.Clamp(score - risk - crony, -1f, 1f);
        }

        public static float NetSelectionValue(float selectionScore, float boldPickRisk, float cronyismPenalty)
            => NetSelectionValue(selectionScore, boldPickRisk, cronyismPenalty, FlagOfficerSelectionParams.Default);

        /// <summary>登用に適すか＝最終登用価値が閾値以上か。</summary>
        public static bool IsSuitableAppointment(float netSelectionValue, float threshold)
            => Mathf.Clamp(netSelectionValue, -1f, 1f) >= Mathf.Clamp(threshold, -1f, 1f);

        public static bool IsSuitableAppointment(float netSelectionValue)
            => IsSuitableAppointment(netSelectionValue, FlagOfficerSelectionParams.Default.suitableThreshold);
    }
}
