namespace Ginei
{
    /// <summary>
    /// 主人公（アンカー提督・GON-6 #735）に関する判定の唯一の窓口（static）。
    /// 主人公＝「動かない光源」：常にプレイヤーが操作する（AI非制御）固定の提督。
    /// フラグ <see cref="AdmiralData.isProtagonist"/> は任意・既定 false＝従来どおり（後方互換）。
    /// 主人公判定を各所に直書きせず、必ずここを参照する。
    /// </summary>
    public static class ProtagonistRules
    {
        /// <summary>この提督が主人公（アンカー）か。null は false（後方互換）。</summary>
        public static bool IsProtagonist(AdmiralData admiral)
            => admiral != null && admiral.isProtagonist;

        /// <summary>
        /// この提督が率いる艦隊に FleetAI を有効化すべきか。
        /// 主人公は陣営に関わらず常にプレイヤー操作＝AI無効。
        /// それ以外は従来どおり「プレイヤー操作でなければ AI を有効化」。
        /// </summary>
        /// <param name="admiral">対象提督（null 可＝主人公上書き無し）。</param>
        /// <param name="isPlayerControlled">この艦隊がプレイヤー操作陣営か。</param>
        public static bool ShouldEnableAI(AdmiralData admiral, bool isPlayerControlled)
            => !isPlayerControlled && !IsProtagonist(admiral);
    }
}
