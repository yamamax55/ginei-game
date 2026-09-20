using UnityEngine;

namespace Ginei
{
    /// <summary>重大案件を目安箱へ諮問する閾値と、却下後の代償（MEYASU-4 #1300）。</summary>
    public readonly struct RatificationParams
    {
        public readonly float disagreementThreshold;
        public readonly float lowLegitimacyThreshold;
        public readonly float democraticAgreementThreshold;
        public readonly float democraticLegitimacyThreshold;
        public readonly float rejectionCredibilityLoss;
        public readonly float autocraticLegitimacyLoss;
        public readonly float constitutionalLegitimacyLoss;
        public readonly float democraticLegitimacyLoss;

        public RatificationParams(float disagreementThreshold, float lowLegitimacyThreshold,
            float democraticAgreementThreshold, float democraticLegitimacyThreshold,
            float rejectionCredibilityLoss, float autocraticLegitimacyLoss,
            float constitutionalLegitimacyLoss, float democraticLegitimacyLoss)
        {
            this.disagreementThreshold = Mathf.Clamp01(disagreementThreshold);
            this.lowLegitimacyThreshold = Mathf.Clamp01(lowLegitimacyThreshold);
            this.democraticAgreementThreshold = Mathf.Clamp01(democraticAgreementThreshold);
            this.democraticLegitimacyThreshold = Mathf.Clamp01(democraticLegitimacyThreshold);
            this.rejectionCredibilityLoss = Mathf.Max(0f, rejectionCredibilityLoss);
            this.autocraticLegitimacyLoss = Mathf.Max(0f, autocraticLegitimacyLoss);
            this.constitutionalLegitimacyLoss = Mathf.Max(0f, constitutionalLegitimacyLoss);
            this.democraticLegitimacyLoss = Mathf.Max(0f, democraticLegitimacyLoss);
        }

        public static RatificationParams Default => new RatificationParams(
            0.45f, 0.40f, 0.20f, 0.25f, 0.08f, 0.12f, 0.15f, 0.04f);
    }

    /// <summary>裁可/却下を適用した結果。数値は事後通知・検証用で、事前予測には使わない。</summary>
    public readonly struct RatificationResult
    {
        public readonly bool applied;
        public readonly bool approved;
        public readonly bool constitutionalCrisis;
        public readonly float credibilityDelta;
        public readonly float legitimacyDelta;
        public readonly string summary;

        public RatificationResult(bool applied, bool approved, bool constitutionalCrisis,
            float credibilityDelta, float legitimacyDelta, string summary)
        {
            this.applied = applied;
            this.approved = approved;
            this.constitutionalCrisis = constitutionalCrisis;
            this.credibilityDelta = credibilityDelta;
            this.legitimacyDelta = legitimacyDelta;
            this.summary = summary ?? "";
        }
    }

    /// <summary>
    /// 稟議ワークフローの唯一の窓口（WF基盤＋MEYASU-1 #1297）。プレイヤー＝<b>序列外の目安箱</b>という制度として、
    /// 建白/注入を<b>越階</b>で受理し（権限ゲート無し＝箱は誰の下でもない）、官僚機構の伝播（<see cref="PetitionFlowRules"/>）を経て
    /// 権力者の決裁へ送り、承認なら執行する。<b>並行新設しない</b>＝WF の提案型は <see cref="Petition"/> を兼用（別 Proposal を作らない）。
    /// 執行時は官僚の忠実度（<see cref="PetitionFlowRules.ExecutionFidelity"/>）で骨抜きになり得る＝実効適用量を返す（効果レジストリは Data/Game 層）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WorkflowRules
    {
        /// <summary>
        /// 最高権力者が重大案件を箱へ問い返すか。立憲/専制は閣内不一致または正統性低下、
        /// 民主政は合議が先に働くため両方が深刻な場合だけ諮問する。
        /// </summary>
        public static bool ShouldDeferToBox(Petition pet, FactionState government, float cabinetAgreement,
            RatificationParams prm)
        {
            if (pet == null || government == null || pet.severity != DecisionSeverity.重大) return false;
            float agreement = Mathf.Clamp01(cabinetAgreement);
            float legitimacy = government.regime != null ? Mathf.Clamp01(government.regime.legitimacy) : 1f;
            GovernmentAxes axes = GovernmentFormRules.Axes(government.governmentForm);
            if (axes.elections && !axes.sovereign)
                return agreement <= prm.democraticAgreementThreshold
                    && legitimacy <= prm.democraticLegitimacyThreshold;
            return agreement <= prm.disagreementThreshold || legitimacy <= prm.lowLegitimacyThreshold;
        }

        public static bool ShouldDeferToBox(Petition pet, FactionState government, float cabinetAgreement)
            => ShouldDeferToBox(pet, government, cabinetAgreement, RatificationParams.Default);

        /// <summary>
        /// 箱へ届いた諮問を裁可/却下する唯一の窓口。裁可は承認へ、却下は終端へ進める。
        /// 却下時だけ箱の信認と正統性を下げ、立憲君主制では憲政危機として結果へ残す。
        /// </summary>
        public static RatificationResult ApplyRatification(Petition pet, bool approved,
            FactionState government, RatificationParams prm)
        {
            if (pet == null || government == null || pet.origin != PetitionOrigin.諮問
                || pet.status != PetitionStatus.決裁待ち)
                return new RatificationResult(false, approved, false, 0f, 0f, "裁可対象の諮問ではありません");

            if (approved)
            {
                pet.status = PetitionStatus.承認;
                return new RatificationResult(true, true, false, 0f, 0f, "諮問を裁可し、執行へ回しました");
            }

            pet.status = PetitionStatus.却下;
            float credibilityBefore = CredibilityRules.Of(government.credibility, pet.box, pet.regionKey);
            CredibilityRules.Adjust(government.credibility, pet.box, -prm.rejectionCredibilityLoss, pet.regionKey);
            float credibilityAfter = CredibilityRules.Of(government.credibility, pet.box, pet.regionKey);

            GovernmentForm form = government.governmentForm;
            bool crisis = form == GovernmentForm.立憲君主制;
            float loss = crisis ? prm.constitutionalLegitimacyLoss
                : (GovernmentFormRules.Axes(form).elections
                    ? prm.democraticLegitimacyLoss : prm.autocraticLegitimacyLoss);
            float legitimacyBefore = government.regime != null ? government.regime.legitimacy : 0f;
            if (government.regime != null)
                government.regime.legitimacy = Mathf.Clamp01(government.regime.legitimacy - loss);
            float legitimacyAfter = government.regime != null ? government.regime.legitimacy : legitimacyBefore;

            return new RatificationResult(true, false, crisis,
                credibilityAfter - credibilityBefore, legitimacyAfter - legitimacyBefore,
                crisis ? "諮問を却下し、憲政危機が発生しました" : "諮問を却下し、箱への信認と正統性が低下しました");
        }

        public static RatificationResult ApplyRatification(Petition pet, bool approved, FactionState government)
            => ApplyRatification(pet, approved, government, RatificationParams.Default);

        /// <summary>箱が越階で受理できるか＝建白/注入（プレイヤー発）の起案。諮問（上→箱）はここを通らない。</summary>
        public static bool CanSubmit(Petition pet)
            => pet != null
               && pet.status == PetitionStatus.起案
               && (pet.origin == PetitionOrigin.建白 || pet.origin == PetitionOrigin.注入);

        /// <summary>越階で官僚機構へ投じる（起案→伝播中）。以後は <see cref="PetitionFlowRules.Step"/> が伝播を解決。</summary>
        public static bool Submit(Petition pet)
        {
            if (!CanSubmit(pet)) return false;
            pet.status = PetitionStatus.伝播中;
            return true;
        }

        /// <summary>決裁＝決裁待ちの陳情を権力者（諮問ならプレイヤー）が承認/却下する。</summary>
        public static bool Decide(Petition pet, bool approve)
        {
            if (pet == null || pet.status != PetitionStatus.決裁待ち) return false;
            pet.status = approve ? PetitionStatus.承認 : PetitionStatus.却下;
            return true;
        }

        /// <summary>
        /// 執行＝承認された陳情を効果適用する（承認→執行済）。官僚の忠実度で骨抜きになる＝<b>実効適用量(0..1)</b>を返す。
        /// 効果レジストリ（effectKey→CampaignState 操作）はこの戻り値を倍率に適用する（Data/Game 層・基準値非破壊）。
        /// 承認状態でなければ 0 を返し遷移しない。
        /// </summary>
        public static float Execute(Petition pet, float fidelity)
        {
            if (pet == null || pet.status != PetitionStatus.承認) return 0f;
            pet.status = PetitionStatus.執行済;
            return Mathf.Clamp01(fidelity);
        }

        /// <summary>進行中か（起案/伝播中/決裁待ち/承認/再浮上）。却下・黙殺・執行済は非アクティブ。</summary>
        public static bool IsActive(Petition pet)
        {
            if (pet == null) return false;
            switch (pet.status)
            {
                case PetitionStatus.起案:
                case PetitionStatus.伝播中:
                case PetitionStatus.決裁待ち:
                case PetitionStatus.承認:
                case PetitionStatus.再浮上:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>決着したか（執行済 or 却下＝終端）。</summary>
        public static bool IsResolved(Petition pet)
            => pet != null && (pet.status == PetitionStatus.執行済 || pet.status == PetitionStatus.却下);

        /// <summary>
        /// 台帳の容量超過時に履歴打切りしてよい黙殺か（黙殺かつ正しさ未判明）。<see cref="IsResolved"/> の意味は変えない＝黙殺は終端ではない。
        /// 正しさが判明した黙殺（vindicated）は再浮上候補として保護する。
        /// </summary>
        public static bool IsPrunableDormant(Petition pet)
            => pet != null && pet.status == PetitionStatus.黙殺 && !pet.vindicated;
    }
}
