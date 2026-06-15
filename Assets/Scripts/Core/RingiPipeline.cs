namespace Ginei
{
    /// <summary>
    /// 稟議（建白→伝播→決裁→執行→効果）の通しを、<b>既存窓口を組み合わせるだけ</b>で1段ずつ駆動する薄い
    /// オーケストレータ（稟議基盤整備）。状態遷移・数式は一切持たず <see cref="WorkflowRules"/>（状態機械）・
    /// <see cref="PetitionFlowRules"/>（伝播/執行忠実度）・<see cref="PetitionEffects"/>（効果レジストリ）へ委譲し、
    /// あわせて <see cref="PetitionLedger"/>（在庫）を一貫させる＝消費側（GalaxyView/受信箱UI）が毎回ハンドワイヤ
    /// していた手順（<c>Submit→Step→決裁待ち→Decide→Execute×忠実度→Apply</c>）を一本化する。<b>並行新設しない</b>
    /// （WF の状態機械を作り直さない・係数を二重実装しない）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class RingiPipeline
    {
        /// <summary>越階で受理し在庫へ載せる（<see cref="WorkflowRules.Submit"/>＋<see cref="PetitionLedger.Add"/>）。
        /// 受理できない（起案でない/諮問など）なら在庫に載せず false。</summary>
        public static bool Submit(PetitionLedger ledger, Petition pet)
        {
            if (ledger == null || pet == null) return false;
            if (!WorkflowRules.Submit(pet)) return false;
            ledger.Add(pet);
            return true;
        }

        /// <summary>官僚機構を1階ぶん伝播させる（<see cref="PetitionFlowRules.Step"/> へ委譲）。</summary>
        public static PetitionStep Propagate(Petition pet, float heed, float friction, float legitimacy,
            float roll, PetitionFlowParams prm)
            => PetitionFlowRules.Step(pet, heed, friction, legitimacy, roll, prm);

        public static PetitionStep Propagate(Petition pet, float heed, float friction, float legitimacy, float roll)
            => PetitionFlowRules.Step(pet, heed, friction, legitimacy, roll);

        /// <summary>官僚機構を抜けて権力者の決裁待ちへ送る（<see cref="PetitionFlowRules.MarkAwaitingDecision"/>）。</summary>
        public static void SendToDecision(Petition pet) => PetitionFlowRules.MarkAwaitingDecision(pet);

        /// <summary>決裁＝権力者（諮問ならプレイヤー）が承認/却下する（<see cref="WorkflowRules.Decide"/>）。</summary>
        public static bool Decide(Petition pet, bool approve) => WorkflowRules.Decide(pet, approve);

        /// <summary>
        /// 承認済みの陳情を執行し、官僚の執行忠実度（<see cref="PetitionFlowRules.ExecutionFidelity"/>＝
        /// 省益/現状維持の摩擦で骨抜き）で割り引いた<b>実効適用量(0..1)</b>を世界（<see cref="CampaignState"/>）へ反映する。
        /// 承認状態でなければ何もせず 0 を返す。＝<see cref="WorkflowRules.Execute"/>×<see cref="PetitionEffects.Apply"/> の合成。
        /// </summary>
        public static float ExecuteAndApply(Petition pet, CampaignState campaign, float executionFriction,
            PetitionFlowParams prm)
        {
            if (pet == null) return 0f;
            float fidelity = PetitionFlowRules.ExecutionFidelity(executionFriction, prm);
            float applied = WorkflowRules.Execute(pet, fidelity);
            if (applied > 0f)
                PetitionEffects.Apply(pet.effectKey, campaign, pet.faction, applied);
            return applied;
        }

        public static float ExecuteAndApply(Petition pet, CampaignState campaign, float executionFriction)
            => ExecuteAndApply(pet, campaign, executionFriction, PetitionFlowParams.Default);
    }
}
