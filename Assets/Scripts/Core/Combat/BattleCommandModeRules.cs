namespace Ginei
{
    /// <summary>会戦の操作モード（誰の権限で部隊を動かしているか）。</summary>
    public enum BattleCommandMode
    {
        /// <summary>
        /// <b>自由操作</b>＝戦役に属さない単体シナリオ／演習。プレイヤーは観戦者ではなく
        /// 「盤面の操作者」として全部を動かす。<b>戦役では使わない</b>。
        /// </summary>
        自由操作,

        /// <summary>
        /// <b>戦役</b>＝プレイヤーはいち人物。役職・委任・指揮系統の内側だけを直接動かし、
        /// 外側へは支援要請にする（GitHub #67）。
        /// </summary>
        戦役,
    }

    /// <summary>
    /// 会戦でどこまで直接操作できるかを決める<b>モードと指揮系統</b>（GitHub #67）。
    ///
    /// <b>これが要る理由</b>：「主人公が会戦に居ないから全部動かす」という当て推量のフォールバックは、
    /// 採用仕様（プレイヤーはいち人物）と噛み合わない。<b>不在や不明を全権限に読み替えない</b>。
    /// 全部動かしてよいのは<b>戦役に属さない単体シナリオ</b>だけで、それはモードとして明示的に分ける。
    ///
    /// 戦役の会戦では、プレイヤーの<b>役職</b>（国家規模の軍事所掌＝総司令官）か、
    /// <b>指揮する軍団</b>か、<b>自分の乗艦</b>だけが直接操作の範囲になる。
    /// どれも無ければ<b>動かせるのは自分の艦隊だけ</b>（それも無ければ何も動かせない）。
    /// </summary>
    public static class BattleCommandModeRules
    {
        /// <summary>
        /// その会戦の操作モード。<paramref name="fromCampaign"/>＝戦略レイヤーから潜行した会戦か
        /// （<see cref="BattleHandoff"/> 経由）。単体シナリオ（タイトルから直接始めた会戦）は自由操作。
        /// </summary>
        public static BattleCommandMode ModeOf(bool fromCampaign)
            => fromCampaign ? BattleCommandMode.戦役 : BattleCommandMode.自由操作;

        /// <summary>
        /// 戦役の会戦で、その人物の指揮系統を組み立てる。
        ///
        /// <paramref name="commandsWholeFleet"/>＝国家規模の軍事所掌（総司令官）を持つか＝全軍を直接動かせる。
        /// <paramref name="corpsName"/>＝指揮する軍団（空＝軍団を持たない）。
        /// <b>不明なときは全権限にしない</b>＝軍団も役職も無ければ自分の乗艦だけになる。
        /// </summary>
        public static BattleChain CampaignChain(bool commandsWholeFleet, string corpsName)
            => new BattleChain(corpsName ?? "", commandsWholeFleet);

        /// <summary>単体シナリオ（自由操作）の系統＝全部を直接動かす。</summary>
        public static BattleChain FreePlayChain() => BattleChain.Everything;

        /// <summary>
        /// モードと素材から系統を決める唯一の窓口。
        /// 戦役なのに人物が特定できない場合も<b>全権限へ落とさない</b>（自分の乗艦だけ）。
        /// </summary>
        public static BattleChain ChainFor(BattleCommandMode mode, bool commandsWholeFleet, string corpsName)
            => mode == BattleCommandMode.自由操作
                ? FreePlayChain()
                : CampaignChain(commandsWholeFleet, corpsName);

        /// <summary>いまのモードを画面へ出す1行（何が直接動かせるのかを説明する）。</summary>
        public static string ModeText(BattleCommandMode mode, in BattleChain chain)
        {
            if (mode == BattleCommandMode.自由操作) return "自由操作（戦役外の演習：全部隊を直接操作）";
            if (chain.commandsAll) return "戦役：総司令官（全部隊を直接操作）";
            if (!string.IsNullOrEmpty(chain.corpsName)) return $"戦役：{chain.corpsName} の指揮（他系統へは支援要請）";
            return "戦役：自艦隊のみ直接操作（他は支援要請）";
        }
    }
}
