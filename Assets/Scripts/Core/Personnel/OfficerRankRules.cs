namespace Ginei
{
    /// <summary>
    /// 武官の<b>階級が入っていない</b>ときの扱い（純ロジック・test-first）。
    ///
    /// <b>なぜ要るか</b>：世代交代の種として置く若手（20〜23歳）や旧セーブの人物は
    /// <see cref="Person.rankTier"/> が 0（未設定）のまま盤面に居る。0 は
    /// <see cref="RankSystem.CareerRankName"/> が空文字を返すので、画面では
    /// 「階級のない指揮官」に見える（実機報告：軍団編成で "同盟の士1" だけ階級が出ない）。
    ///
    /// <b>方針</b>
    /// <list type="bullet">
    ///   <item>足りないものを<b>埋めるだけ</b>＝すでに階級がある人物は絶対に書き換えない
    ///   （一律昇格も降格もしない）。</item>
    ///   <item>埋める値は立身出世ラダーの<b>最初の段</b>（<see cref="EntryTier"/>＝少尉）。
    ///   表示のためだけの架空の階級ではなく、人物データに実際に入れる値。</item>
    ///   <item>武官（<see cref="PersonRole.軍人"/>）だけが対象。文民へ軍の階級を与えない。</item>
    ///   <item>階級と<b>任命条件</b>は別物＝階級を埋めても指揮できる規模は変わらない
    ///   （艦隊司令は <see cref="CommandCapacityRules.Tier艦隊"/> が要る）。足りなければ
    ///   <see cref="CommandGateNote"/> でそう表示する＝黙って昇進させない。</item>
    /// </list>
    /// </summary>
    public static class OfficerRankRules
    {
        /// <summary>武官の最初の階級（少尉）。<see cref="RankSystem.FullLadderName"/> の1段目。</summary>
        public const int EntryTier = 1;

        /// <summary>その人物に階級を入れる必要があるか（武官・存命・未設定のときだけ true）。</summary>
        public static bool NeedsInitialRank(PersonRole role, int rankTier, bool isDeceased)
            => role == PersonRole.軍人 && !isDeceased && rankTier <= 0;

        /// <summary>
        /// 保存すべき階級 tier を返す。すでに階級があればそのまま（<b>上書きしない</b>）、
        /// 武官で未設定なら <see cref="EntryTier"/>、文民の未設定は 0 のまま。
        /// </summary>
        public static int FillMissing(PersonRole role, int rankTier, bool isDeceased = false)
            => NeedsInitialRank(role, rankTier, isDeceased) ? EntryTier : rankTier;

        /// <summary>
        /// その人物に階級を入れる（必要なときだけ）。入れたら true。
        /// 名前・能力・所属は触らない＝階級だけを補う。
        /// </summary>
        public static bool EnsureRank(Person person)
        {
            if (person == null) return false;
            if (!NeedsInitialRank(person.role, person.rankTier, person.IsDeceased)) return false;
            person.rankTier = EntryTier;
            return true;
        }

        /// <summary>
        /// 名簿全体の欠落を補う（旧セーブ対策）。補った人数を返す。
        /// すでに階級のある人物は素通りするので、何度呼んでも結果は同じ（冪等・決定論）。
        /// </summary>
        public static int EnsureRanks(System.Collections.Generic.IReadOnlyList<Person> people)
        {
            if (people == null) return 0;
            int filled = 0;
            for (int i = 0; i < people.Count; i++)
                if (EnsureRank(people[i])) filled++;
            return filled;
        }

        // ===== 任命条件（階級を埋めても指揮できる規模は変わらない） =====

        /// <summary>その階級で当該梯団の指揮官になれるか（<see cref="OrderOfBattle.RequiredTier"/> と同じ基準）。</summary>
        public static bool MeetsCommandGate(int rankTier, EchelonType echelon)
            => rankTier >= CommandCapacityRules.CommanderTierFor(echelon);

        /// <summary>
        /// 任命条件に足りないときの但し書き（足りていれば空文字）。例：「（要 中将）」。
        /// 人手が足りず下位の士官が艦隊を預かっている、という状態を<b>隠さずに出す</b>ための表示
        /// ＝勝手に昇進させて辻褄を合わせない。
        /// </summary>
        public static string CommandGateNote(int rankTier, EchelonType echelon, FactionData faction = null)
        {
            if (MeetsCommandGate(rankTier, echelon)) return "";
            int need = CommandCapacityRules.CommanderTierFor(echelon);
            string needName = RankSystem.CareerRankName(faction, need);
            return string.IsNullOrEmpty(needName) ? "" : "（要 " + needName + "）";
        }
    }
}
