using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// LegalFitnessRules の調整値（マジックナンバー集約・基準非破壊）。既定は <see cref="Default"/>。
    /// 適合度→正統性ボーナスの強さ・不適合→反乱圧力の強さ・有機的発展の速さ・移植拒絶の強さなどを束ねる。
    /// </summary>
    public readonly struct LegalFitnessParams
    {
        /// <summary>法の適合度1あたりに正統性へ与える最大ボーナス（0..1）。</summary>
        public readonly float legitimacyGain;
        /// <summary>不適合(=1-適合度)1あたりに高まる反乱圧力の最大（0..1）。</summary>
        public readonly float rebellionGain;
        /// <summary>慣習に根ざした法が単位時間あたりに社会へ馴染んで適合度を育てる率（/時間）。</summary>
        public readonly float organicGrowthRate;
        /// <summary>文化的距離1あたりに移植法を拒絶する強さ（0..1）。</summary>
        public readonly float transplantRejectionWeight;
        /// <summary>正統性の基礎下駄（適合度0でも残る最低限の正統性）。</summary>
        public readonly float legitimacyFloor;
        /// <summary>よく適合した法と判定する適合度の既定しきい値。</summary>
        public readonly float wellFittedThreshold;

        public LegalFitnessParams(
            float legitimacyGain, float rebellionGain, float organicGrowthRate,
            float transplantRejectionWeight, float legitimacyFloor, float wellFittedThreshold)
        {
            this.legitimacyGain = legitimacyGain;
            this.rebellionGain = rebellionGain;
            this.organicGrowthRate = organicGrowthRate;
            this.transplantRejectionWeight = transplantRejectionWeight;
            this.legitimacyFloor = legitimacyFloor;
            this.wellFittedThreshold = wellFittedThreshold;
        }

        /// <summary>
        /// 既定（正統性ボーナス上限0.7／反乱ボーナス上限0.6／有機的発展率0.05／移植拒絶重み0.8／
        /// 正統性下駄0.1／よく適合した法の閾値0.6）。
        /// 法はその社会に適合するほど受け入れられ、不適合な輸入法は守られず反発を招く。
        /// </summary>
        public static LegalFitnessParams Default => new LegalFitnessParams(
            legitimacyGain: 0.7f, rebellionGain: 0.6f, organicGrowthRate: 0.05f,
            transplantRejectionWeight: 0.8f, legitimacyFloor: 0.1f, wellFittedThreshold: 0.6f);
    }

    /// <summary>
    /// 法の適合性の純ロジック（MONT-5 #1449・test-first）。モンテスキュー『法の精神』の
    /// 「法は、その国の風土・宗教・思想・産業・歴史・人口に適合せねばならず＝『ある国民の法が他の国民に
    /// 適することは大いなる偶然』。普遍的に正しい法でなく、その社会に適合した法こそが機能する。
    /// 輸入された不適合な法は機能不全と反乱を招く」をモデル化する唯一の窓口。
    /// 法が風土×思想×産業に整合する（<see cref="LegalFitness"/>）ほど正統性が高く
    /// （<see cref="LegitimacyFromFitness"/>）反乱圧力が低い（<see cref="RebellionPressureFromMisfit"/>）。
    /// 慣習に根ざした法は時間で社会に馴染み（<see cref="OrganicLawDevelopment"/>）、文化的に遠い社会へ
    /// 移植された法は拒絶される（<see cref="LegalTransplantRejection"/>）。
    /// 乱数なし（決定論）・全入力クランプ・調整値は <see cref="LegalFitnessParams"/> に集約。
    /// <para>
    /// 分担：<see cref="LegalGeneralityRules"/> は法の一般性（質＝ハイエクの rule OF law）、
    /// <see cref="GovernanceRules"/> は安定度（統治の数値）を扱うのに対し、ここは「法がその社会に
    /// 適合しているか（モンテスキューの法の精神＝適合性の係数）」を扱う。<c>CultureRules</c>（文化・同化）、
    /// <c>ClimatePolityFitRules</c>（風土と政体の適合・同 EPIC MONT）とも別系統。
    /// </para>
    /// </summary>
    public static class LegalFitnessRules
    {
        /// <summary>
        /// 法の適合度＝法が風土・思想・産業に整合する度合いの調和（三者の相乗平均、0..1）。
        /// 相乗平均ゆえどれか一つでも大きく外れると全体が痩せる（法は社会の全側面に合わねば機能しない）。
        /// </summary>
        public static float LegalFitness(float climateMatch, float ideologyMatch, float industryMatch)
        {
            float c = Mathf.Clamp01(climateMatch);
            float i = Mathf.Clamp01(ideologyMatch);
            float n = Mathf.Clamp01(industryMatch);
            return Mathf.Pow(c * i * n, 1f / 3f);
        }

        /// <summary>
        /// 法の適合度から得られる正統性（0..1）。基礎下駄＋適合度×ボーナス上限＝
        /// 法が社会に適合するほど馴染んで受け入れられる（その社会に適合した法こそが正統性を持つ）。
        /// </summary>
        public static float LegitimacyFromFitness(float legalFitness, LegalFitnessParams prm)
        {
            float f = Mathf.Clamp01(legalFitness);
            return Mathf.Clamp01(prm.legitimacyFloor + f * prm.legitimacyGain);
        }

        /// <summary>正統性（既定パラメータ）。</summary>
        public static float LegitimacyFromFitness(float legalFitness)
            => LegitimacyFromFitness(legalFitness, LegalFitnessParams.Default);

        /// <summary>
        /// 輸入された外国法の不適合度＝外国法の異質さ（foreignLaw）が現地の文脈（localContext）に
        /// 合わない度合い（0..1）。現地文脈が薄い（=外国法の前提が現地にない）ほど不適合が大きい
        /// ＝他国の法をそのまま移植する失敗（『他の国民に適することは大いなる偶然』）。
        /// </summary>
        public static float ImportedLawMisfit(float foreignLaw, float localContext)
        {
            float f = Mathf.Clamp01(foreignLaw);
            float l = Mathf.Clamp01(localContext);
            return Mathf.Clamp01(f * (1f - l));
        }

        /// <summary>
        /// 不適合(=1-適合度)から高まる反乱圧力（0..1）。法が社会に合わないほど守られず反発を招く
        /// ＝<c>GovernanceRules</c> の反乱圧へ加わる入力。適合度が高いほど 0 に近づく。
        /// </summary>
        public static float RebellionPressureFromMisfit(float legalFitness, LegalFitnessParams prm)
        {
            float misfit = 1f - Mathf.Clamp01(legalFitness);
            return Mathf.Clamp01(misfit * prm.rebellionGain);
        }

        /// <summary>反乱圧力（既定パラメータ）。</summary>
        public static float RebellionPressureFromMisfit(float legalFitness)
            => RebellionPressureFromMisfit(legalFitness, LegalFitnessParams.Default);

        /// <summary>
        /// 有機的な法の発展（時間追従）：慣習に根ざした法ほど時間で社会へ馴染み適合度が育つ。
        /// 適合度を 1 へ向けて慣習の根の深さ(customaryRoots)×成長率×dt だけ進めた新しい適合度を返す。
        /// 慣習の根が無い（=移植された法）ほど育たない。dt&lt;=0 はそのまま返す。
        /// </summary>
        public static float OrganicLawDevelopment(float legalFitness, float customaryRoots, float deltaTime, LegalFitnessParams prm)
        {
            float f = Mathf.Clamp01(legalFitness);
            if (deltaTime <= 0f) return f;
            float roots = Mathf.Clamp01(customaryRoots);
            float grow = roots * prm.organicGrowthRate * deltaTime;
            return Mathf.MoveTowards(f, 1f, Mathf.Max(0f, grow));
        }

        /// <summary>有機的な法の発展（既定パラメータ）。</summary>
        public static float OrganicLawDevelopment(float legalFitness, float customaryRoots, float deltaTime)
            => OrganicLawDevelopment(legalFitness, customaryRoots, deltaTime, LegalFitnessParams.Default);

        /// <summary>
        /// 法の移植拒絶＝文化的距離(culturalDistance)が大きいほど移植された外国法(foreignLaw)が
        /// 拒絶される度合い（0..1＝法の拒絶反応）。外国法の量と文化的距離の積に拒絶重みを掛ける
        /// ＝遠い文化へ持ち込まれた法ほど根付かず弾かれる。
        /// </summary>
        public static float LegalTransplantRejection(float foreignLaw, float culturalDistance, LegalFitnessParams prm)
        {
            float f = Mathf.Clamp01(foreignLaw);
            float d = Mathf.Clamp01(culturalDistance);
            return Mathf.Clamp01(f * d * prm.transplantRejectionWeight);
        }

        /// <summary>法の移植拒絶（既定パラメータ）。</summary>
        public static float LegalTransplantRejection(float foreignLaw, float culturalDistance)
            => LegalTransplantRejection(foreignLaw, culturalDistance, LegalFitnessParams.Default);

        /// <summary>
        /// 法の精神（モンテスキュー）＝法・政体形態・社会条件の総合的な整合（0..1）。
        /// 風土・思想・産業の適合度（<see cref="LegalFitness"/>）と政体形態の適合(govForm)を合わせた相乗平均
        /// ＝全要素が整合してはじめて法は精神を得る（どれか一つでも外れると全体が痩せる）。
        /// </summary>
        public static float SpiritOfLaws(float climateMatch, float ideologyMatch, float industryMatch, float govForm)
        {
            float fitness = LegalFitness(climateMatch, ideologyMatch, industryMatch);
            float g = Mathf.Clamp01(govForm);
            return Mathf.Pow(fitness * g, 0.5f);
        }

        /// <summary>
        /// 社会によく適合した（機能する）法か＝適合度がしきい値を超えたとき true。
        /// </summary>
        public static bool IsWellFittedLaw(float legalFitness, float threshold)
            => Mathf.Clamp01(legalFitness) >= Mathf.Clamp01(threshold);

        /// <summary>よく適合した法の判定（既定しきい値）。</summary>
        public static bool IsWellFittedLaw(float legalFitness)
            => IsWellFittedLaw(legalFitness, LegalFitnessParams.Default.wellFittedThreshold);
    }
}
