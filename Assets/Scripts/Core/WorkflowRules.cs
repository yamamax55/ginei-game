using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 稟議ワークフローの唯一の窓口（WF基盤＋MEYASU-1 #1297）。プレイヤー＝<b>序列外の目安箱</b>という制度として、
    /// 建白/注入を<b>越階</b>で受理し（権限ゲート無し＝箱は誰の下でもない）、官僚機構の伝播（<see cref="PetitionFlowRules"/>）を経て
    /// 権力者の決裁へ送り、承認なら執行する。<b>並行新設しない</b>＝WF の提案型は <see cref="Petition"/> を兼用（別 Proposal を作らない）。
    /// 執行時は官僚の忠実度（<see cref="PetitionFlowRules.ExecutionFidelity"/>）で骨抜きになり得る＝実効適用量を返す（効果レジストリは Data/Game 層）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class WorkflowRules
    {
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
    }
}
