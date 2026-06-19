using UnityEngine;

namespace Ginei
{
    /// <summary>寝返り工作（敵の将官・要人の勧誘）の調整係数。</summary>
    public readonly struct DefectionRecruitmentParams
    {
        /// <summary>不満が乗りやすさに効く重み。</summary>
        public readonly float grievanceWeight;
        /// <summary>野心が乗りやすさに効く重み。</summary>
        public readonly float ambitionWeight;
        /// <summary>大義への疑念が乗りやすさに効く重み。</summary>
        public readonly float doubtWeight;
        /// <summary>提示した見返りの効きの強さ。</summary>
        public readonly float rewardScale;
        /// <summary>報復の恐れが忠誠の抵抗を固める強さ。</summary>
        public readonly float fearScale;
        /// <summary>勧誘が露見するリスクの基準係数。</summary>
        public readonly float exposureScale;
        /// <summary>寝返りとみなす確率の閾値。</summary>
        public readonly float defectThreshold;

        public DefectionRecruitmentParams(float grievanceWeight, float ambitionWeight, float doubtWeight,
            float rewardScale, float fearScale, float exposureScale, float defectThreshold)
        {
            this.grievanceWeight = Mathf.Clamp01(grievanceWeight);
            this.ambitionWeight = Mathf.Clamp01(ambitionWeight);
            this.doubtWeight = Mathf.Clamp01(doubtWeight);
            this.rewardScale = Mathf.Clamp01(rewardScale);
            this.fearScale = Mathf.Clamp01(fearScale);
            this.exposureScale = Mathf.Clamp01(exposureScale);
            this.defectThreshold = Mathf.Clamp01(defectThreshold);
        }

        /// <summary>既定＝不満0.5・野心0.3・疑念0.2・見返り0.6・恐れ0.7・露見0.5・寝返り閾値0.5。</summary>
        public static DefectionRecruitmentParams Default
            => new DefectionRecruitmentParams(0.5f, 0.3f, 0.2f, 0.6f, 0.7f, 0.5f, 0.5f);
    }

    /// <summary>
    /// 寝返り工作の純ロジック（敵の将官・官僚・技術者を勧誘して味方に引き込む能動的工作）。
    /// 敵要人の勧誘に乗りやすさは不満・野心・大義への疑念で決まり、提示した見返り（金銭・地位・大義）で押し、
    /// 相手の忠誠の固さと報復の恐れがそれを拒む。乗りやすさと見返りが忠誠の抵抗を上回れば寝返る確率が立つ。
    /// だが乗り気すぎる相手は罠（二重スパイ）のリスクを孕み、大胆な接触ほど敵の防諜に露見しやすい。
    /// 寝返った要人の価値は地位と機密へのアクセスで決まる。
    /// 旗幟・寝返りカスケード（<see cref="LoyaltyRules"/>）が「板挟みの旗本が傾く」受動的な現象なのに対し、
    /// こちらは「こちらから敵要人に近づいて引き抜く」能動的な勧誘工作＝別系統。
    /// 内部離間（<see cref="AlienationStratagemRules"/>＝敵同士を仲違いさせる）とも別＝こちらは引き抜き。
    /// 盤面非依存・乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DefectionRecruitmentRules
    {
        /// <summary>
        /// 勧誘に乗りやすさ（0..1）＝不満 grievance(0..1)・野心 ambition(0..1)・大義への疑念 ideologicalDoubt(0..1)を
        /// 各重みで加重平均。不満が大きく、野心が強く、信じる大義に疑いがあるほど引き抜きに乗りやすい。
        /// </summary>
        public static float TargetSusceptibility(float grievance, float ambition, float ideologicalDoubt,
            DefectionRecruitmentParams p)
        {
            float g = Mathf.Clamp01(grievance);
            float a = Mathf.Clamp01(ambition);
            float d = Mathf.Clamp01(ideologicalDoubt);
            float wsum = p.grievanceWeight + p.ambitionWeight + p.doubtWeight;
            if (wsum <= 0f) return 0f;
            float s = (g * p.grievanceWeight + a * p.ambitionWeight + d * p.doubtWeight) / wsum;
            return Mathf.Clamp01(s);
        }

        public static float TargetSusceptibility(float grievance, float ambition, float ideologicalDoubt)
            => TargetSusceptibility(grievance, ambition, ideologicalDoubt, DefectionRecruitmentParams.Default);

        /// <summary>
        /// 提示した見返りの効き（0..1）＝見返り rewardOffered(0..1)×rewardScale×乗りやすさ targetSusceptibility(0..1)。
        /// 同じ金銭・地位を積んでも、もともと乗り気な相手ほど刺さる＝乗りにくい相手には見返りが効きにくい。
        /// </summary>
        public static float Inducement(float rewardOffered, float targetSusceptibility, DefectionRecruitmentParams p)
        {
            float r = Mathf.Clamp01(rewardOffered);
            float s = Mathf.Clamp01(targetSusceptibility);
            return Mathf.Clamp01(r * p.rewardScale * s);
        }

        public static float Inducement(float rewardOffered, float targetSusceptibility)
            => Inducement(rewardOffered, targetSusceptibility, DefectionRecruitmentParams.Default);

        /// <summary>
        /// 寝返りを拒む忠誠の抵抗（0..1）＝忠誠 targetLoyalty(0..1)＋報復の恐れ fearOfReprisal(0..1)×fearScale を合成（独立確率の和）。
        /// 忠誠が固く、寝返りがバレたときの報復が怖いほど引き抜きに抵抗する＝抵抗が大きいほど寝返りにくい。
        /// </summary>
        public static float LoyaltyResistance(float targetLoyalty, float fearOfReprisal, DefectionRecruitmentParams p)
        {
            float loy = Mathf.Clamp01(targetLoyalty);
            float fear = Mathf.Clamp01(fearOfReprisal) * p.fearScale;
            float r = 1f - (1f - loy) * (1f - fear);
            return Mathf.Clamp01(r);
        }

        public static float LoyaltyResistance(float targetLoyalty, float fearOfReprisal)
            => LoyaltyResistance(targetLoyalty, fearOfReprisal, DefectionRecruitmentParams.Default);

        /// <summary>
        /// 寝返る確率（0..1）＝見返りの効き inducement(0..1)×（1−忠誠の抵抗 loyaltyResistance(0..1)）。
        /// 見返りが効き、相手の抵抗が薄いほど寝返る＝抵抗が完全（1）なら見返りに関わらず寝返らない。
        /// </summary>
        public static float DefectionProbability(float inducement, float loyaltyResistance)
        {
            float ind = Mathf.Clamp01(inducement);
            float res = Mathf.Clamp01(loyaltyResistance);
            return Mathf.Clamp01(ind * (1f - res));
        }

        /// <summary>
        /// 寝返りが罠（二重スパイ）のリスク（0..1）＝乗りやすさ targetSusceptibility(0..1)×不自然な乗り気 suspiciousEagerness(0..1)。
        /// 簡単に乗り、しかも不自然なほど前のめりな相手ほど、実は敵の差し向けた二重スパイの疑いが濃い。
        /// </summary>
        public static float DoubleAgentRisk(float targetSusceptibility, float suspiciousEagerness)
        {
            float s = Mathf.Clamp01(targetSusceptibility);
            float e = Mathf.Clamp01(suspiciousEagerness);
            return Mathf.Clamp01(s * e);
        }

        /// <summary>
        /// 勧誘工作が露見するリスク（0..1）＝接触の大胆さ recruitmentBoldness(0..1)×敵の防諜 enemyCounterintel(0..1)×exposureScale。
        /// 大胆に近づくほど、そして敵の防諜が強いほど工作が露見する＝慎重な接触や防諜の弱い敵なら露見しにくい。
        /// </summary>
        public static float ApproachExposure(float recruitmentBoldness, float enemyCounterintel, DefectionRecruitmentParams p)
        {
            float b = Mathf.Clamp01(recruitmentBoldness);
            float c = Mathf.Clamp01(enemyCounterintel);
            return Mathf.Clamp01(b * c * p.exposureScale);
        }

        public static float ApproachExposure(float recruitmentBoldness, float enemyCounterintel)
            => ApproachExposure(recruitmentBoldness, enemyCounterintel, DefectionRecruitmentParams.Default);

        /// <summary>
        /// 寝返った要人の価値（0..1）＝地位 targetRank(0..1)と機密アクセス targetAccess(0..1)の幾何平均。
        /// 高位で機密に深く触れる要人ほど価値が高い＝どちらか一方が乏しければ（幾何平均で）価値は落ちる。
        /// </summary>
        public static float DefectorValue(float targetRank, float targetAccess)
        {
            float r = Mathf.Clamp01(targetRank);
            float a = Mathf.Clamp01(targetAccess);
            return Mathf.Clamp01(Mathf.Sqrt(r * a));
        }

        /// <summary>
        /// 寝返るか（bool）＝寝返る確率 defectionProbability が閾値 threshold 以上。閾値を能動的に越えたときだけ引き抜きが成立。
        /// </summary>
        public static bool DoesDefect(float defectionProbability, float threshold)
        {
            return Mathf.Clamp01(defectionProbability) >= Mathf.Clamp01(threshold);
        }

        public static bool DoesDefect(float defectionProbability, DefectionRecruitmentParams p)
            => DoesDefect(defectionProbability, p.defectThreshold);

        public static bool DoesDefect(float defectionProbability)
            => DoesDefect(defectionProbability, DefectionRecruitmentParams.Default.defectThreshold);
    }
}
