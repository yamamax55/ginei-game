using UnityEngine;

namespace Ginei
{
    /// <summary>社会的不満（相対的剥奪と期待の管理）の調整係数。</summary>
    public readonly struct SocialDiscontentParams
    {
        /// <summary>相対的剥奪（期待−現実の落差）の感受性倍率（落差が不満へ変わる強さ）。</summary>
        public readonly float deprivationScale;
        /// <summary>格差×公正規範が不公正感に変わる倍率（公正を重んじる社会ほど格差が刺さる）。</summary>
        public readonly float inequityScale;
        /// <summary>世代間不公平（若者の閉塞×上の世代の既得権）の倍率。</summary>
        public readonly float generationalScale;
        /// <summary>上昇機会の閉塞（梯子が外された感）の倍率。</summary>
        public readonly float blockageScale;
        /// <summary>不満の蓄積に対する剥奪の重み（蓄積式の配分）。</summary>
        public readonly float deprivationWeight;
        /// <summary>不満の蓄積に対する不公正感の重み（蓄積式の配分）。</summary>
        public readonly float inequityWeight;
        /// <summary>不満の蓄積に対する機会閉塞の重み（蓄積式の配分）。</summary>
        public readonly float blockageWeight;
        /// <summary>ガス抜きに対する福祉の重み（実需の充足による緩和）。</summary>
        public readonly float welfareWeight;
        /// <summary>ガス抜きに対する改革応答の重み（是正される見込みによる緩和）。</summary>
        public readonly float reformWeight;
        /// <summary>ガス抜き全体の効き倍率。</summary>
        public readonly float valveScale;
        /// <summary>J字カーブ（改善の後の急反転）の臨界倍率。</summary>
        public readonly float jCurveScale;

        public SocialDiscontentParams(float deprivationScale, float inequityScale,
                                      float generationalScale, float blockageScale,
                                      float deprivationWeight, float inequityWeight, float blockageWeight,
                                      float welfareWeight, float reformWeight, float valveScale,
                                      float jCurveScale)
        {
            this.deprivationScale = Mathf.Max(0f, deprivationScale);
            this.inequityScale = Mathf.Max(0f, inequityScale);
            this.generationalScale = Mathf.Max(0f, generationalScale);
            this.blockageScale = Mathf.Max(0f, blockageScale);
            this.deprivationWeight = Mathf.Max(0f, deprivationWeight);
            this.inequityWeight = Mathf.Max(0f, inequityWeight);
            this.blockageWeight = Mathf.Max(0f, blockageWeight);
            this.welfareWeight = Mathf.Max(0f, welfareWeight);
            this.reformWeight = Mathf.Max(0f, reformWeight);
            this.valveScale = Mathf.Max(0f, valveScale);
            this.jCurveScale = Mathf.Max(0f, jCurveScale);
        }

        /// <summary>既定＝剥奪1/不公正1/世代1/閉塞1・蓄積重み(剥奪0.4/不公正0.3/閉塞0.3)・ガス抜き(福祉0.5/改革0.5×効き0.8)・J字1.2。</summary>
        public static SocialDiscontentParams Default =>
            new SocialDiscontentParams(1f, 1f, 1f, 1f, 0.4f, 0.3f, 0.3f, 0.5f, 0.5f, 0.8f, 1.2f);
    }

    /// <summary>
    /// 社会的不満の蓄積の純ロジック＝相対的剥奪と期待の管理。
    /// 人は絶対的な貧しさより<b>期待と現実の落差</b>（相対的剥奪）に怒り、格差そのものより
    /// それを<b>不公正</b>と感じる規範に火が付き、世代間の不公平（若者の閉塞×上の既得権）と
    /// 上昇機会の閉塞（梯子が外された感）が不満を蓄積させる。福祉と改革応答はガス抜きとして
    /// 不満を緩和するが、根を治さなければ蓄積は続く。臨界は <b>J字カーブ</b>＝革命は最悪期でなく
    /// 「改善が続いた後の急な悪化（上り調子が折れた瞬間）」に起きる。
    /// 不満水準は -1..1（負＝充足・正＝不満）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SocialDiscontentRules
    {
        /// <summary>
        /// 相対的剥奪（0..1）＝(期待−現実) の正の落差×感受性。
        /// 現実が期待を上回れば剥奪は0（落差は不満だけを生み、過充足は不満を負にしない）。
        /// </summary>
        public static float RelativeDeprivation(float expectations, float actualConditions, SocialDiscontentParams p)
        {
            float gap = Mathf.Clamp01(expectations) - Mathf.Clamp01(actualConditions);
            return Mathf.Clamp01(Mathf.Max(0f, gap) * p.deprivationScale);
        }

        public static float RelativeDeprivation(float expectations, float actualConditions)
            => RelativeDeprivation(expectations, actualConditions, SocialDiscontentParams.Default);

        /// <summary>
        /// 不公正感（0..1）＝格差×公正規範×倍率。
        /// 同じ格差でも公正を重んじる社会ほど刺さる（規範0なら格差は不満にならない）。
        /// </summary>
        public static float InequityPerception(float inequality, float fairnessNorm, SocialDiscontentParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(inequality) * Mathf.Clamp01(fairnessNorm) * p.inequityScale);
        }

        public static float InequityPerception(float inequality, float fairnessNorm)
            => InequityPerception(inequality, fairnessNorm, SocialDiscontentParams.Default);

        /// <summary>
        /// 世代間不公平（0..1）＝若者の見通しの悪さ(1−見通し)×上の世代の既得権×倍率。
        /// 若者の未来が暗いほど、かつ上の世代が逃げ切るほど不公平感が募る。
        /// </summary>
        public static float GenerationalGrievance(float youthProspects, float elderPrivilege, SocialDiscontentParams p)
        {
            float bleak = 1f - Mathf.Clamp01(youthProspects);
            return Mathf.Clamp01(bleak * Mathf.Clamp01(elderPrivilege) * p.generationalScale);
        }

        public static float GenerationalGrievance(float youthProspects, float elderPrivilege)
            => GenerationalGrievance(youthProspects, elderPrivilege, SocialDiscontentParams.Default);

        /// <summary>
        /// 機会の閉塞（0..1）＝(1−上昇機会)×倍率。
        /// 社会的流動性が低いほど「梯子が外された」感が強まる（流動性1なら閉塞0）。
        /// </summary>
        public static float MobilityBlockage(float socialMobility, SocialDiscontentParams p)
        {
            return Mathf.Clamp01((1f - Mathf.Clamp01(socialMobility)) * p.blockageScale);
        }

        public static float MobilityBlockage(float socialMobility)
            => MobilityBlockage(socialMobility, SocialDiscontentParams.Default);

        /// <summary>
        /// 不満の蓄積（0..1）＝剥奪×重み＋不公正×重み＋閉塞×重みの加重平均。
        /// 三つの源（落差・不公正・閉塞）が重なるほど不満が積み上がる。
        /// </summary>
        public static float DiscontentAccumulation(float relativeDeprivation, float inequityPerception,
                                                   float mobilityBlockage, SocialDiscontentParams p)
        {
            float sum = p.deprivationWeight + p.inequityWeight + p.blockageWeight;
            if (sum <= 0f) return 0f;
            float acc = Mathf.Clamp01(relativeDeprivation) * p.deprivationWeight
                      + Mathf.Clamp01(inequityPerception) * p.inequityWeight
                      + Mathf.Clamp01(mobilityBlockage) * p.blockageWeight;
            return Mathf.Clamp01(acc / sum);
        }

        public static float DiscontentAccumulation(float relativeDeprivation, float inequityPerception,
                                                   float mobilityBlockage)
            => DiscontentAccumulation(relativeDeprivation, inequityPerception, mobilityBlockage, SocialDiscontentParams.Default);

        /// <summary>
        /// ガス抜き（0..1）＝(福祉×重み＋改革応答×重み)の加重平均×効き倍率。
        /// 福祉が実需を満たし、改革応答が是正の見込みを与えるほど不満が逃げる（根は治さない）。
        /// </summary>
        public static float SafetyValve(float welfareProvision, float reformResponsiveness, SocialDiscontentParams p)
        {
            float sum = p.welfareWeight + p.reformWeight;
            if (sum <= 0f) return 0f;
            float relief = (Mathf.Clamp01(welfareProvision) * p.welfareWeight
                          + Mathf.Clamp01(reformResponsiveness) * p.reformWeight) / sum;
            return Mathf.Clamp01(relief * p.valveScale);
        }

        public static float SafetyValve(float welfareProvision, float reformResponsiveness)
            => SafetyValve(welfareProvision, reformResponsiveness, SocialDiscontentParams.Default);

        /// <summary>
        /// J字カーブの臨界リスク（0..1）＝過去の改善×急な悪化×倍率。
        /// 上り調子（改善の記憶＝高まった期待）の後に急反転すると爆発する＝革命は最悪期でなく
        /// 「良くなると思った矢先に折れた瞬間」に起きる（改善も悪化も無ければリスク0）。
        /// </summary>
        public static float JCurveRisk(float pastImprovement, float suddenReversal, SocialDiscontentParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(pastImprovement) * Mathf.Clamp01(suddenReversal) * p.jCurveScale);
        }

        public static float JCurveRisk(float pastImprovement, float suddenReversal)
            => JCurveRisk(pastImprovement, suddenReversal, SocialDiscontentParams.Default);

        /// <summary>
        /// 不満水準（-1..1）＝蓄積−ガス抜き＋J字リスク。
        /// 負＝充足（ガス抜きが蓄積を上回る）・正＝不満。J字リスクが乗ると臨界へ押し上げる。
        /// </summary>
        public static float DiscontentLevel(float discontentAccumulation, float safetyValve,
                                            float jCurveRisk, SocialDiscontentParams p)
        {
            float level = Mathf.Clamp01(discontentAccumulation)
                        - Mathf.Clamp01(safetyValve)
                        + Mathf.Clamp01(jCurveRisk);
            return Mathf.Clamp(level, -1f, 1f);
        }

        public static float DiscontentLevel(float discontentAccumulation, float safetyValve, float jCurveRisk)
            => DiscontentLevel(discontentAccumulation, safetyValve, jCurveRisk, SocialDiscontentParams.Default);
    }
}
