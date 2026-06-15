using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 尚賢（しょうけん＝賢者を能力本位で登用する）が体制正統性を保全する係数（MOZI-4 #1565・墨子参考）。
    /// 「能力本位度」「縁故主義の侵食」「無能登用のペナルティ」「人材誘引」の調整値をまとめる。
    /// 既定 <see cref="Default"/>。全入力クランプ・乱数なし決定論・基準値非破壊（実効値はローカル算出）。
    /// </summary>
    public readonly struct CompetenceLegitimacyParams
    {
        /// <summary>能力本位度が正統性を保全する倍率の最大ボーナス（尚賢が満点で得る上乗せ＝1+これが上限倍率）。</summary>
        public readonly float preservationBonusMax;
        /// <summary>無能登用ペナルティの最大値（重要ポストへ無能を就けた時に正統性へ与える最大の損ない）。</summary>
        public readonly float incompetencePenaltyMax;
        /// <summary>能力本位度が賢才を呼び込む最大倍率（尚賢の体制ほど人材が集まる上限）。</summary>
        public readonly float talentAttractionMax;
        /// <summary>民の信頼における能力本位度の重み（残りは統治結果の重み＝合計1）。</summary>
        public readonly float meritWeightInConfidence;
        /// <summary>縁故主義が正統性を蝕む時定数（この時間で縁故が正統性を削る＝大きいほどゆっくり）。</summary>
        public readonly float nepotismDecayTime;
        /// <summary>尚賢の体制と判定する能力本位度の既定閾値。</summary>
        public readonly float meritocraticThreshold;

        public CompetenceLegitimacyParams(float preservationBonusMax, float incompetencePenaltyMax,
            float talentAttractionMax, float meritWeightInConfidence, float nepotismDecayTime,
            float meritocraticThreshold)
        {
            this.preservationBonusMax = preservationBonusMax;
            this.incompetencePenaltyMax = incompetencePenaltyMax;
            this.talentAttractionMax = talentAttractionMax;
            this.meritWeightInConfidence = meritWeightInConfidence;
            this.nepotismDecayTime = nepotismDecayTime;
            this.meritocraticThreshold = meritocraticThreshold;
        }

        /// <summary>
        /// 既定係数：正統性保全ボーナス最大0.3（尚賢満点で1.3倍）・無能登用ペナルティ最大0.4・
        /// 人材誘引最大2倍・民の信頼の能力本位重み0.5（残り0.5が統治結果）・
        /// 縁故侵食の時定数120（戦略秒）・尚賢判定閾値0.6。
        /// </summary>
        public static CompetenceLegitimacyParams Default => new CompetenceLegitimacyParams(
            0.3f, 0.4f, 2f, 0.5f, 120f, 0.6f);
    }

    /// <summary>
    /// 尚賢の正統性直結（MOZI-4 #1565・墨子「尚賢＝身分でなく能力で人を用いれば政治が治まり民が従う」・
    /// 純ロジック test-first）。賢者を能力本位で登用すれば体制の正統性が保たれ賢才が集まり、
    /// 無能な縁故者を高位に就ければ正統性が蝕まれる、を一連の倍率で表す：
    /// 能力本位度(<see cref="MeritocracyIndex"/>)・正統性保全倍率(<see cref="LegitimacyPreservation"/>)・
    /// 無能登用ペナルティ(<see cref="IncompetencePenalty"/>)・人材誘引(<see cref="TalentAttraction"/>)・
    /// 民の信頼(<see cref="PopularConfidence"/>)・縁故の侵食(<see cref="NepotismDecayTick"/>)・
    /// 賢者登用の選抜(<see cref="ElevateWorthy"/>)・尚賢判定(<see cref="IsMeritocraticRegime"/>)。
    /// すべて決定論・乱数なし・基準値非破壊。
    /// 戦功による昇進閾値は <see cref="MeritPromotionRules"/>、役割×役職の適材適所の効果倍率は PersonRules、
    /// 席次vs実力（卒業席次が実力に追い越される）は <see cref="SeniorityRules"/> が担う。
    /// 本ルールは「賢者を能力本位で登用すること自体が体制正統性を保全する直結倍率」のみを扱い、それらの並行系を作らない。
    /// </summary>
    public static class CompetenceLegitimacyRules
    {
        /// <summary>
        /// 能力本位度＝登用された者の能力(0..1) × （縁故でない度＝1−nepotism）。
        /// 有能でも縁故登用なら能力本位度は下がり、賢者を縁故抜きで登用すれば高くなる。0..1。
        /// </summary>
        public static float MeritocracyIndex(float appointeeCompetence, float nepotism)
        {
            float comp = Mathf.Clamp01(appointeeCompetence);
            float nonNepotism = 1f - Mathf.Clamp01(nepotism);
            return Mathf.Clamp01(comp * nonNepotism);
        }

        /// <summary>
        /// 能力本位度が体制正統性を保全する倍率（実効値）。尚賢ほど1を超え、縁故偏重ほど1に近い。
        /// 1 + meritocracyIndex × preservationBonusMax（賢者登用＝正統性維持）。1以上。
        /// </summary>
        public static float LegitimacyPreservation(float meritocracyIndex, CompetenceLegitimacyParams prm)
        {
            float idx = Mathf.Clamp01(meritocracyIndex);
            return 1f + idx * Mathf.Max(0f, prm.preservationBonusMax);
        }

        /// <summary>
        /// 無能登用が正統性を損なう量。重要な地位(postImportance 0..1)ほど、能力(0..1)が低いほど大きい：
        /// (1−appointeeCompetence) × postImportance × incompetencePenaltyMax。0..penaltyMax。
        /// （重要ポストへ賢者を就ければほぼ0、末端へ凡庸を就けても小さい。）
        /// </summary>
        public static float IncompetencePenalty(float appointeeCompetence, float postImportance,
            CompetenceLegitimacyParams prm)
        {
            float incompetence = 1f - Mathf.Clamp01(appointeeCompetence);
            float importance = Mathf.Clamp01(postImportance);
            return incompetence * importance * Mathf.Max(0f, prm.incompetencePenaltyMax);
        }

        /// <summary>
        /// 能力本位の体制ほど賢才が集まる倍率（尚賢が人材を呼ぶ）。
        /// 1 + meritocracyIndex × (talentAttractionMax − 1)。1..talentAttractionMax。
        /// </summary>
        public static float TalentAttraction(float meritocracyIndex, CompetenceLegitimacyParams prm)
        {
            float idx = Mathf.Clamp01(meritocracyIndex);
            float max = Mathf.Max(1f, prm.talentAttractionMax);
            return 1f + idx * (max - 1f);
        }

        /// <summary>
        /// 民の信頼＝能力本位度と良い統治結果(governanceResult 0..1)の加重平均（0..1）。
        /// 能力本位の登用だけでなく、その有能さが実際に良い統治を生んでこそ民が従う（墨子＝治まれば民従う）。
        /// 重みは meritWeightInConfidence（残りが統治結果）。
        /// </summary>
        public static float PopularConfidence(float meritocracyIndex, float governanceResult,
            CompetenceLegitimacyParams prm)
        {
            float idx = Mathf.Clamp01(meritocracyIndex);
            float gov = Mathf.Clamp01(governanceResult);
            float w = Mathf.Clamp01(prm.meritWeightInConfidence);
            return Mathf.Clamp01(w * idx + (1f - w) * gov);
        }

        /// <summary>
        /// 縁故主義が時間で正統性を蝕む（無能な縁故登用は放置するほど正統性を削る）。
        /// nepotism(0..1)に比例して legitimacy を dt ぶん減らす。時定数 nepotismDecayTime が短いほど速く蝕む。
        /// 返り値は侵食後の正統性（0..1にクランプ）。
        /// </summary>
        public static float NepotismDecayTick(float legitimacy, float nepotism, float dt,
            CompetenceLegitimacyParams prm)
        {
            float leg = Mathf.Clamp01(legitimacy);
            float nep = Mathf.Clamp01(nepotism);
            float step = Mathf.Max(0f, dt);
            if (prm.nepotismDecayTime <= 0f) return leg;
            float drop = nep * (step / prm.nepotismDecayTime);
            return Mathf.Clamp01(leg - drop);
        }

        /// <summary>
        /// 賢者を能力で選ぶか身分で選ぶか（尚賢の選抜スコア）。
        /// useMerit＝true（尚賢）なら能力(candidateCompetence)を、false なら門地(candidateBirth＝家柄の高さ)を
        /// 選抜の評価値とする。大きいほど選ばれる。0..1。
        /// </summary>
        public static float ElevateWorthy(float candidateCompetence, float candidateBirth, bool useMerit)
        {
            return useMerit ? Mathf.Clamp01(candidateCompetence) : Mathf.Clamp01(candidateBirth);
        }

        /// <summary>
        /// 尚賢の体制か（能力本位度が閾値以上か）。threshold が負なら既定値で判定。
        /// </summary>
        public static bool IsMeritocraticRegime(float meritocracyIndex, float threshold,
            CompetenceLegitimacyParams prm)
        {
            float idx = Mathf.Clamp01(meritocracyIndex);
            float th = threshold < 0f ? prm.meritocraticThreshold : Mathf.Clamp01(threshold);
            return idx >= th;
        }
    }
}
