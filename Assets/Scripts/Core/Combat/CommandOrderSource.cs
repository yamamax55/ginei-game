namespace Ginei
{
    /// <summary>
    /// 命令の<b>出どころ</b>（GitHub #67）。指揮権を判定するかどうかを、これで分ける。
    ///
    /// <b>既定は必ず <see cref="プレイヤー"/></b>＝公開APIを引数なしで呼ぶと権限判定が働く。
    /// AI・QA・内部の自動失効だけが、呼び出し側で<b>明示的に</b>素通しを選べる
    /// ＝通常入力から抜け道ができない。
    /// </summary>
    public enum CommandOrderSource
    {
        /// <summary>プレイヤーの操作（右クリックメニュー・HUD・キー）。指揮権を判定する。</summary>
        プレイヤー,

        /// <summary>
        /// AI／システムの内部処理（守備AIの自動編成・総退却の下令・軍団消滅による自動失効）。
        /// 盤面の都合で起きるものなので、プレイヤーの権限では止めない。
        /// </summary>
        AI,

        /// <summary>QA 専用の入口（Editor メニュー）。通常入力からは呼べない。</summary>
        QA,
    }

    /// <summary>命令の出どころに対する扱い（純ロジック・test-first）。</summary>
    public static class CommandOrderSourceRules
    {
        /// <summary>その出どころで指揮権の判定が要るか（プレイヤーだけ true）。</summary>
        public static bool RequiresAuthorityCheck(CommandOrderSource source)
            => source == CommandOrderSource.プレイヤー;

        /// <summary>既定の出どころ（＝プレイヤー）。公開APIの既定値はこれでなければならない。</summary>
        public static CommandOrderSource Default => CommandOrderSource.プレイヤー;

        /// <summary>その出どころの説明（ログ・通知に使う）。</summary>
        public static string Label(CommandOrderSource source)
        {
            switch (source)
            {
                case CommandOrderSource.プレイヤー: return "プレイヤーの命令";
                case CommandOrderSource.AI: return "AI・自動処理";
                case CommandOrderSource.QA: return "QA 専用";
                default: return "不明";
            }
        }
    }
}
