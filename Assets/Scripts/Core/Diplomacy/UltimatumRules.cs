using UnityEngine;

namespace Ginei
{
    /// <summary>最後通牒・瀬戸際外交の調整係数。</summary>
    public readonly struct UltimatumParams
    {
        /// <summary>通牒の厳しさに占める「要求水準」の重み。</summary>
        public readonly float demandWeight;
        /// <summary>通牒の厳しさに占める「期限圧力」の重み。</summary>
        public readonly float deadlineWeight;
        /// <summary>信憑性に占める軍事準備の重み。</summary>
        public readonly float readinessWeight;
        /// <summary>信憑性に占める決意の重み。</summary>
        public readonly float resolveWeight;
        /// <summary>信憑性に占める実行実績（前科）の重み。</summary>
        public readonly float reputationWeight;
        /// <summary>受諾見込みに対する屈服度の効き（高いほど屈服しやすい層に偏る）。</summary>
        public readonly float capitulationScale;
        /// <summary>拒否（反発）に占める矜持の重み。</summary>
        public readonly float prideWeight;
        /// <summary>拒否（反発）に占める戦力の重み。</summary>
        public readonly float strengthWeight;
        /// <summary>誤算リスクの増幅係数（瀬戸際×意思疎通の悪さ）。</summary>
        public readonly float miscalculationScale;
        /// <summary>帰結に対する誤算（意図せぬ戦争）の引き下げ係数。</summary>
        public readonly float miscalculationPenalty;

        public UltimatumParams(
            float demandWeight, float deadlineWeight,
            float readinessWeight, float resolveWeight, float reputationWeight,
            float capitulationScale, float prideWeight, float strengthWeight,
            float miscalculationScale, float miscalculationPenalty)
        {
            this.demandWeight = Mathf.Clamp01(demandWeight);
            this.deadlineWeight = Mathf.Clamp01(deadlineWeight);
            this.readinessWeight = Mathf.Clamp01(readinessWeight);
            this.resolveWeight = Mathf.Clamp01(resolveWeight);
            this.reputationWeight = Mathf.Clamp01(reputationWeight);
            this.capitulationScale = Mathf.Clamp01(capitulationScale);
            this.prideWeight = Mathf.Clamp01(prideWeight);
            this.strengthWeight = Mathf.Clamp01(strengthWeight);
            this.miscalculationScale = Mathf.Max(0f, miscalculationScale);
            this.miscalculationPenalty = Mathf.Clamp01(miscalculationPenalty);
        }

        /// <summary>
        /// 既定＝要求重み0.5/期限重み0.5・準備0.4/決意0.35/実績0.25・屈服0.6・矜持0.55/戦力0.45・
        /// 誤算増幅1.0・誤算ペナルティ0.5。
        /// </summary>
        public static UltimatumParams Default => new UltimatumParams(
            0.5f, 0.5f,
            0.4f, 0.35f, 0.25f,
            0.6f, 0.55f, 0.45f,
            1f, 0.5f);
    }

    /// <summary>
    /// 最後通牒（アルティメイタム）・瀬戸際外交の純ロジック。脅迫外交の力学を解く：
    /// 通牒の厳しさ（要求水準×期限圧力）は屈服を迫るが、相手が呑むかは脅しの信憑性
    /// （軍事準備×決意×前科）と相手の屈服度に懸かる。矜持高く戦力ある相手は反発で拒否し、
    /// 要求を曖昧にし調停余地を残せば「メンツを保った退路」が開いて衝突を避けられる。
    /// だが公の場で賭け金を吊り上げ評判を担保にすると「引くに引けない罠」にはまり、
    /// 瀬戸際で意思疎通を欠けば「誤算」＝意図せぬ戦争が起きる。帰結は受諾−反発−誤算で
    /// -1..1（＋は屈服を勝ち取る成功、−は瀬戸際の破綻＝戦争）。抑止 DeterrenceRules
    /// （平時に開戦を思いとどまらせる）に対し、こちらは「期限を切って屈服を強要する」局面を解く。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class UltimatumRules
    {
        /// <summary>
        /// 通牒の厳しさ（0..1）＝要求水準×要求重み＋期限圧力×期限重み。
        /// 過大な要求と短い期限が重なるほど相手を追い詰める。
        /// </summary>
        public static float UltimatumSeverity(float demandLevel, float deadlinePressure, UltimatumParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(demandLevel) * p.demandWeight +
                Mathf.Clamp01(deadlinePressure) * p.deadlineWeight);
        }

        public static float UltimatumSeverity(float demandLevel, float deadlinePressure)
            => UltimatumSeverity(demandLevel, deadlinePressure, UltimatumParams.Default);

        /// <summary>
        /// 信憑性（0..1）＝軍事準備×準備重み＋決意×決意重み＋実行実績×実績重み。
        /// 動かせる艦隊・固い意志・「やると言ったらやる」前科がないと、通牒は空脅しに堕す。
        /// </summary>
        public static float Credibility(float militaryReadiness, float resolve, float reputationForFollowThrough, UltimatumParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(militaryReadiness) * p.readinessWeight +
                Mathf.Clamp01(resolve) * p.resolveWeight +
                Mathf.Clamp01(reputationForFollowThrough) * p.reputationWeight);
        }

        public static float Credibility(float militaryReadiness, float resolve, float reputationForFollowThrough)
            => Credibility(militaryReadiness, resolve, reputationForFollowThrough, UltimatumParams.Default);

        /// <summary>
        /// 受諾の見込み（0..1）＝厳しさ×信憑×屈服度。掛け算＝どれかゼロなら呑まない
        /// （信じられない脅しも、戦う気満々の相手も、屈服しない）。屈服度は係数で効きを調整。
        /// </summary>
        public static float ComplianceLikelihood(float ultimatumSeverity, float credibility, float targetCapitulation, UltimatumParams p)
        {
            float cap = Mathf.Lerp(1f - p.capitulationScale, 1f, Mathf.Clamp01(targetCapitulation));
            return Mathf.Clamp01(Mathf.Clamp01(ultimatumSeverity) * Mathf.Clamp01(credibility) * cap);
        }

        public static float ComplianceLikelihood(float ultimatumSeverity, float credibility, float targetCapitulation)
            => ComplianceLikelihood(ultimatumSeverity, credibility, targetCapitulation, UltimatumParams.Default);

        /// <summary>
        /// 拒否（反発）の度合い（0..1）＝相手の矜持×矜持重み＋戦力×戦力重み。
        /// 誇り高く力ある相手ほど突きつけられた通牒に逆らう。
        /// </summary>
        public static float DefianceResponse(float targetPride, float targetStrength, UltimatumParams p)
        {
            return Mathf.Clamp01(
                Mathf.Clamp01(targetPride) * p.prideWeight +
                Mathf.Clamp01(targetStrength) * p.strengthWeight);
        }

        public static float DefianceResponse(float targetPride, float targetStrength)
            => DefianceResponse(targetPride, targetStrength, UltimatumParams.Default);

        /// <summary>
        /// メンツを保った退路（0..1）＝要求の曖昧さ×調停余地。掛け算＝どちらかゼロなら退路なし
        /// （白黒つく要求も、仲介者なき直接対決も、引き下がる口実を与えない）。
        /// </summary>
        public static float FaceSavingExit(float ambiguity, float mediationAvailable, UltimatumParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(ambiguity) * Mathf.Clamp01(mediationAvailable));
        }

        public static float FaceSavingExit(float ambiguity, float mediationAvailable)
            => FaceSavingExit(ambiguity, mediationAvailable, UltimatumParams.Default);

        /// <summary>
        /// 誤算（意図せぬ戦争）のリスク（0..1）＝瀬戸際度×(1-意思疎通の明瞭さ)×増幅係数。
        /// 崖っぷちで突っ張り合い、かつ意図が伝わらないほど、引き金を引くつもりがなくても戦争になる。
        /// </summary>
        public static float MiscalculationRisk(float brinkmanship, float communicationClarity, UltimatumParams p)
        {
            float fog = 1f - Mathf.Clamp01(communicationClarity);
            return Mathf.Clamp01(Mathf.Clamp01(brinkmanship) * fog * p.miscalculationScale);
        }

        public static float MiscalculationRisk(float brinkmanship, float communicationClarity)
            => MiscalculationRisk(brinkmanship, communicationClarity, UltimatumParams.Default);

        /// <summary>
        /// 引くに引けない罠（0..1）＝公の賭け金×評判コスト。掛け算＝公言し評判を担保にするほど
        /// 撤回が高くつき、退路が焼ける（瀬戸際から降りられなくなる）。
        /// </summary>
        public static float CommitmentTrap(float publicStakes, float reputationCost, UltimatumParams p)
        {
            return Mathf.Clamp01(Mathf.Clamp01(publicStakes) * Mathf.Clamp01(reputationCost));
        }

        public static float CommitmentTrap(float publicStakes, float reputationCost)
            => CommitmentTrap(publicStakes, reputationCost, UltimatumParams.Default);

        /// <summary>
        /// 通牒の帰結（-1..1）＝受諾見込み−反発−誤算×ペナルティ。
        /// ＋は屈服を勝ち取る成功、−は瀬戸際の破綻（拒否・意図せぬ戦争）。
        /// </summary>
        public static float UltimatumOutcome(float complianceLikelihood, float defianceResponse, float miscalculationRisk, UltimatumParams p)
        {
            float outcome =
                Mathf.Clamp01(complianceLikelihood) -
                Mathf.Clamp01(defianceResponse) -
                Mathf.Clamp01(miscalculationRisk) * p.miscalculationPenalty;
            return Mathf.Clamp(outcome, -1f, 1f);
        }

        public static float UltimatumOutcome(float complianceLikelihood, float defianceResponse, float miscalculationRisk)
            => UltimatumOutcome(complianceLikelihood, defianceResponse, miscalculationRisk, UltimatumParams.Default);
    }
}
