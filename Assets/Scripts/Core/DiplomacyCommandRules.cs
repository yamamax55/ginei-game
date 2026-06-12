namespace Ginei
{
    /// <summary>プレイヤーが発令できる外交行動（DIPLO-PLAYER・#2119 拡張）。</summary>
    public enum DiplomaticAction { 宣戦布告, 講和, 同盟, 不可侵, 破棄 }

    /// <summary>
    /// プレイヤーの外交コマンドの純ロジック（#2119 拡張・プレイヤー操作化の核）。
    /// 「自勢力が対象勢力へ外交行動を発令できるか／発令する」を検証・適用する単一窓口。数値/状態遷移は `DiplomacyRules` へ委譲。
    /// UI（キー/パネル）はこの窓口を呼ぶだけにする＝操作の正当性を一箇所に集約。test-first。
    /// </summary>
    public static class DiplomacyCommandRules
    {
        /// <summary>その外交行動を今の状態で発令できるか（前提条件チェック）。</summary>
        public static bool CanIssue(DiplomacyState state, string actor, string target, DiplomaticAction action)
        {
            if (state == null || string.IsNullOrEmpty(actor) || string.IsNullOrEmpty(target) || actor == target) return false;
            var s = state.Status(actor, target);
            switch (action)
            {
                case DiplomaticAction.宣戦布告: return s != DiplomacyState.DiplomaticStatus.交戦;
                case DiplomaticAction.講和:     return s == DiplomacyState.DiplomaticStatus.交戦;
                case DiplomaticAction.同盟:     return s != DiplomacyState.DiplomaticStatus.交戦 && s != DiplomacyState.DiplomaticStatus.同盟;
                case DiplomaticAction.不可侵:   return s != DiplomacyState.DiplomaticStatus.交戦 && s != DiplomacyState.DiplomaticStatus.同盟 && s != DiplomacyState.DiplomaticStatus.不可侵;
                case DiplomaticAction.破棄:
                    return s == DiplomacyState.DiplomaticStatus.同盟 || s == DiplomacyState.DiplomaticStatus.不可侵 || s == DiplomacyState.DiplomaticStatus.属国;
                default: return false;
            }
        }

        /// <summary>外交行動を発令（前提を満たさなければ false）。状態遷移は `DiplomacyRules` へ委譲。</summary>
        public static bool Issue(DiplomacyState state, string actor, string target, DiplomaticAction action, DiplomacyRules.DiplomacyParams p)
        {
            if (!CanIssue(state, actor, target, action)) return false;
            switch (action)
            {
                case DiplomaticAction.宣戦布告: return DiplomacyRules.DeclareWar(state, actor, target, p);
                case DiplomaticAction.講和:     return DiplomacyRules.MakePeace(state, actor, target);
                case DiplomaticAction.同盟:     return DiplomacyRules.SignTreaty(state, actor, target, DiplomacyState.DiplomaticStatus.同盟);
                case DiplomaticAction.不可侵:   return DiplomacyRules.SignTreaty(state, actor, target, DiplomacyState.DiplomaticStatus.不可侵);
                case DiplomaticAction.破棄:     return DiplomacyRules.BreakTreaty(state, actor, target, p);
                default: return false;
            }
        }
    }
}
