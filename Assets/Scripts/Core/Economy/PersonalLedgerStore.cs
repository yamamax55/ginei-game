namespace Ginei
{
    /// <summary>
    /// プレイヤー（主人公）の個人仕訳帳の単一保管庫（<see cref="FleetPool"/> と同じ static ストアの作法）。
    /// 私財の操作（私兵購入・慰労など）が記帳し、<see cref="PersonalBookkeepingOverlay"/> が月次で観測する。
    /// 主人公は1人ゆえ単一インスタンスで足りる。会戦セットアップ/テストで <see cref="Clear"/>。
    /// </summary>
    public static class PersonalLedgerStore
    {
        private static PersonalLedger ledger;

        /// <summary>主人公の仕訳帳（無ければ生成）。</summary>
        public static PersonalLedger Ledger => ledger ?? (ledger = new PersonalLedger());

        /// <summary>記帳済みか（期首をまだ建てていないかの判定に使う）。</summary>
        public static bool HasEntries => ledger != null && ledger.Count > 0;

        /// <summary>仕訳帳を空にする。</summary>
        public static void Clear() => ledger?.Clear();
    }
}
