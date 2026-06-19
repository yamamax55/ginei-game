using UnityEngine;

namespace Ginei
{
    /// <summary>猟官制・恩顧主義の調整係数（スポイルズ・システム）。</summary>
    public readonly struct PatronageParams
    {
        /// <summary>全ポストを猟官化したとき買える忠誠の最大値。</summary>
        public readonly float loyaltyGainScale;
        /// <summary>無能な縁故者で全ポストを埋めたときの行政能力の最大劣化幅。</summary>
        public readonly float qualityLossScale;
        /// <summary>実力者流出が加速し始める猟官割合の閾値（ここまでは我慢、超えると見切る）。</summary>
        public readonly float exodusThreshold;
        /// <summary>閾値以下での流出の緩傾斜（多少の縁故なら才能はまだ残る）。</summary>
        public readonly float exodusBaseRate;
        /// <summary>全ポスト猟官化での実力者流出の最大割合。</summary>
        public readonly float exodusMax;
        /// <summary>長年染み付いた猟官制への改革抵抗の最大値（既得層が試験制度を殺しに来る強さ）。</summary>
        public readonly float reformResistanceMax;
        /// <summary>改革抵抗が最大に固まるまでの既得年数。</summary>
        public readonly float entrenchYearsToMax;

        public PatronageParams(
            float loyaltyGainScale, float qualityLossScale,
            float exodusThreshold, float exodusBaseRate, float exodusMax,
            float reformResistanceMax, float entrenchYearsToMax)
        {
            this.loyaltyGainScale = Mathf.Clamp01(loyaltyGainScale);
            this.qualityLossScale = Mathf.Clamp01(qualityLossScale);
            this.exodusThreshold = Mathf.Clamp(exodusThreshold, 0f, 0.99f);
            this.exodusBaseRate = Mathf.Max(0f, exodusBaseRate);
            this.exodusMax = Mathf.Clamp01(exodusMax);
            this.reformResistanceMax = Mathf.Clamp01(reformResistanceMax);
            this.entrenchYearsToMax = Mathf.Max(1f, entrenchYearsToMax);
        }

        /// <summary>既定＝忠誠0.6・劣化0.5・流出閾値0.5/緩傾斜0.2/最大0.8・改革抵抗0.9/既得20年。</summary>
        public static PatronageParams Default => new PatronageParams(0.6f, 0.5f, 0.5f, 0.2f, 0.8f, 0.9f, 20f);
    }

    /// <summary>
    /// 猟官制・恩顧主義の純ロジック（官職を支持者に配って忠誠を買う人事）。
    /// 政権基盤は固まるが行政能力が劣化し、昇進が忠誠で決まる組織から才能が逃げる
    /// ＝「忠誠は買えるが有能は買えない」。短期の安定 vs 長期の劣化を NetRegimeValue で数値化する。
    /// 分担：<see cref="SeniorityRules"/> は「席次 vs 実力」（年功序列の固さ）、
    /// <see cref="CareerPipelineRules"/> は「出自パイプライン」（武/官/技の供給経路）、
    /// ここは「忠誠 vs 能力」（縁故配分そのものの損益）を扱う。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PatronageRules
    {
        /// <summary>改革抵抗の下限比率＝既得年数ゼロでも猟官層は抵抗する（最大値の半分から始まる）。</summary>
        public const float FreshResistanceRatio = 0.5f;

        /// <summary>
        /// 買えた忠誠（0..loyaltyGainScale）＝猟官ポストの割合 patronageShare(0..1) に比例。
        /// 配るほど政権基盤は固い（恩を受けた者は裏切らない）。
        /// </summary>
        public static float LoyaltyPurchased(float patronageShare, PatronageParams p)
        {
            return Mathf.Clamp01(patronageShare) * p.loyaltyGainScale;
        }

        public static float LoyaltyPurchased(float patronageShare)
            => LoyaltyPurchased(patronageShare, PatronageParams.Default);

        /// <summary>
        /// 行政能力（0..1、1=満全）＝1−猟官割合×(1−縁故者の腕前 loyalistCompetence(0..1))×劣化幅。
        /// 有能な子分なら劣化は浅い（腕前1で無傷）、無能な取り巻きで埋めると最大 qualityLossScale 落ちる。
        /// </summary>
        public static float AdministrativeQuality(float patronageShare, float loyalistCompetence, PatronageParams p)
        {
            float incompetence = 1f - Mathf.Clamp01(loyalistCompetence);
            return Mathf.Clamp01(1f - Mathf.Clamp01(patronageShare) * incompetence * p.qualityLossScale);
        }

        public static float AdministrativeQuality(float patronageShare, float loyalistCompetence)
            => AdministrativeQuality(patronageShare, loyalistCompetence, PatronageParams.Default);

        /// <summary>
        /// 実力者の流出（0..exodusMax）＝昇進が忠誠で決まる組織から才能は逃げる。
        /// 閾値 exodusThreshold までは緩傾斜（exodusBaseRate）、超えると加速して
        /// 全ポスト猟官化で exodusMax に達する（見切りの雪崩）。
        /// </summary>
        public static float MeritocracyExodus(float patronageShare, PatronageParams p)
        {
            float share = Mathf.Clamp01(patronageShare);
            float atThreshold = p.exodusThreshold * p.exodusBaseRate;
            if (share <= p.exodusThreshold)
            {
                return Mathf.Clamp01(share * p.exodusBaseRate);
            }
            float denom = Mathf.Max(0.01f, 1f - p.exodusThreshold);
            float slope = Mathf.Max(0f, p.exodusMax - atThreshold) / denom;
            return Mathf.Clamp01(atThreshold + (share - p.exodusThreshold) * slope);
        }

        public static float MeritocracyExodus(float patronageShare)
            => MeritocracyExodus(patronageShare, PatronageParams.Default);

        /// <summary>
        /// 論功行賞の期待圧力（0..1）＝報われない支持者の割合 max(0, 支持者−ポスト)/支持者。
        /// 支持者がポストより多ければ配っても恨まれる（勝っても全員には報いられない）。支持者0なら圧力0。
        /// </summary>
        public static float SpoilsExpectation(int supporterCount, int postCount)
        {
            int supporters = Mathf.Max(0, supporterCount);
            if (supporters == 0) return 0f;
            int posts = Mathf.Max(0, postCount);
            int unrewarded = Mathf.Max(0, supporters - posts);
            return Mathf.Clamp01((float)unrewarded / supporters);
        }

        /// <summary>
        /// 猟官制の改革抵抗（0..reformResistanceMax）＝猟官割合×既得の固さ。
        /// 既得層は試験制度（実力人事）を殺しに来る。既得年数 entrenchedYears が
        /// entrenchYearsToMax に達すると抵抗は最大、年数ゼロでも FreshResistanceRatio 分は抵抗する。
        /// </summary>
        public static float ReformResistance(float patronageShare, float entrenchedYears, PatronageParams p)
        {
            float entrenchment = Mathf.Clamp01(Mathf.Max(0f, entrenchedYears) / p.entrenchYearsToMax);
            float hardness = FreshResistanceRatio + (1f - FreshResistanceRatio) * entrenchment;
            return Mathf.Clamp01(patronageShare) * hardness * p.reformResistanceMax;
        }

        public static float ReformResistance(float patronageShare, float entrenchedYears)
            => ReformResistance(patronageShare, entrenchedYears, PatronageParams.Default);

        /// <summary>
        /// 政権にとっての損益＝買えた忠誠−行政劣化−実力者流出。
        /// 有能な子分への控えめな配分だけがわずかに引き合い、全面猟官化は縁故者が天才揃いでも
        /// 流出で赤字になる＝「忠誠は買えるが有能は買えない」を数値で出す。
        /// </summary>
        public static float NetRegimeValue(float patronageShare, float loyalistCompetence, PatronageParams p)
        {
            float qualityLoss = 1f - AdministrativeQuality(patronageShare, loyalistCompetence, p);
            return LoyaltyPurchased(patronageShare, p) - qualityLoss - MeritocracyExodus(patronageShare, p);
        }

        public static float NetRegimeValue(float patronageShare, float loyalistCompetence)
            => NetRegimeValue(patronageShare, loyalistCompetence, PatronageParams.Default);
    }
}
