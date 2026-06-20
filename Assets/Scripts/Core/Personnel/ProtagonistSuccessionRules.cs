namespace Ginei
{
    /// <summary>主人公の死後の帰結（採用「主人公の死で詰まない」・#907 の解答）。
    /// 一代記完結＝平民の叩き上げは潔く一代を終え新たな主人公で次のプレイ／継承＝貴族・王家は世継ぎへ操作座を引き継ぐ。</summary>
    public enum ProtagonistDeathOutcome { 一代記完結_新規, 継承_世継ぎ }

    /// <summary>
    /// 主人公の死後の操作座をどう継ぐかの純ロジック（採用「プレイヤー＝いち提督」・#907 の凍結理由〔操作継承の肥大化〕への解答・
    /// 唯一の窓口）。出自（<see cref="OriginRules.InheritsOnDeath"/>）で分岐＝平民は<b>一代記の完結→新規</b>、貴族/王家は
    /// <b>継承(#646)で世継ぎへ</b>。継承の実体（後継者選定）は <c>SuccessionRules</c>/#646 へ委譲し、本ルールは<b>分岐の決定のみ</b>
    /// （無制限の人物乗り換えはしない＝肥大化防止・本線#794）。決定論・test-first。
    /// </summary>
    public static class ProtagonistSuccessionRules
    {
        /// <summary>
        /// 主人公の死後の帰結を決める。出自が継承する身分（貴族/王家）<b>かつ</b>後継者が居れば「継承_世継ぎ」、
        /// それ以外（平民、または継承身分でも後継者不在＝家系の断絶）は「一代記完結_新規」。
        /// </summary>
        public static ProtagonistDeathOutcome OnDeath(PersonOrigin origin, bool hasHeir)
            => (OriginRules.InheritsOnDeath(origin) && hasHeir)
                ? ProtagonistDeathOutcome.継承_世継ぎ
                : ProtagonistDeathOutcome.一代記完結_新規;

        /// <summary>継承で操作座を引き継ぐか（true＝世継ぎへ／false＝新規プレイ）。</summary>
        public static bool ContinuesAsHeir(PersonOrigin origin, bool hasHeir)
            => OnDeath(origin, hasHeir) == ProtagonistDeathOutcome.継承_世継ぎ;
    }
}
