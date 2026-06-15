namespace Ginei
{
    /// <summary>
    /// 位階（<see cref="CourtRank"/>）の純ロジック（日本の律令制・官僚制基盤・史実参考）。
    /// 序列の比較、叙位（考課に基づく昇叙・貶位）、官位相当（位階と官職の対応）、そして
    /// <b>五位の壁</b>（六位以下から五位＝貴族へ通常の考課では昇れず勅授を要した史実の障壁）を扱う。
    /// 武官の <see cref="RankSystem"/> とは別の文官身分軸。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class JapaneseCourtRankRules
    {
        /// <summary>貴族の下限＝従五位下（これ以上が「貴族」。五位の壁の境界）。</summary>
        public const CourtRank NobilityFloor = CourtRank.従五位下;
        /// <summary>公卿の下限＝従三位（これ以上が「公卿」）。</summary>
        public const CourtRank KugyoFloor = CourtRank.従三位;

        /// <summary>位階の名称（enum 名がそのまま漢字表記）。</summary>
        public static string Name(CourtRank r) => r.ToString();

        /// <summary>位階を持つか（無位でない）。</summary>
        public static bool IsRanked(CourtRank r) => r != CourtRank.無位;

        /// <summary>貴族か（従五位下以上）。五位の壁の上側。</summary>
        public static bool IsNobility(CourtRank r) => IsRanked(r) && (int)r <= (int)NobilityFloor;

        /// <summary>公卿か（従三位以上）。</summary>
        public static bool IsKugyo(CourtRank r) => IsRanked(r) && (int)r <= (int)KugyoFloor;

        /// <summary>a が b より上位なら正・下位なら負・同位は0（無位は最下）。</summary>
        public static int Compare(CourtRank a, CourtRank b) => (int)b - (int)a;

        /// <summary>上位の位階を返す（同位は a）。</summary>
        public static CourtRank Higher(CourtRank a, CourtRank b) => (int)a <= (int)b ? a : b;

        /// <summary>一階上（正一位で頭打ち）。無位からは少初位下＝ラダーへの叙任。</summary>
        public static CourtRank Next(CourtRank r) => (int)r > 0 ? (CourtRank)((int)r - 1) : CourtRank.正一位;

        /// <summary>一階下（無位で底）。</summary>
        public static CourtRank Previous(CourtRank r)
            => (int)r < (int)CourtRank.無位 ? (CourtRank)((int)r + 1) : CourtRank.無位;

        /// <summary>
        /// <paramref name="from"/>→<paramref name="to"/> が五位の壁を上向きに跨ぐか
        /// （六位以下→五位以上＝貴族入り）。史実では通常の考課では越えられず勅授を要した。
        /// </summary>
        public static bool CrossesFifthRankWall(CourtRank from, CourtRank to)
            => (int)from > (int)NobilityFloor && (int)to <= (int)NobilityFloor;

        /// <summary>
        /// 叙位＝考課（<see cref="MeritRating"/>）に基づく位階の昇叙・貶位。上系で一階昇叙、下下で一階貶位、他は据置。
        /// <paramref name="allowBreakFifthWall"/> が false のとき、六位から五位へ跨ぐ昇叙は五位の壁で阻まれ据置になる
        /// （勅授・特進のときだけ true）。
        /// </summary>
        public static CourtRank AdvanceOnMerit(CourtRank current, MeritRating rating, bool allowBreakFifthWall = false)
        {
            if (MeritEvaluationRules.IsTop(rating))
            {
                CourtRank up = Next(current);
                if (!allowBreakFifthWall && CrossesFifthRankWall(current, up)) return current; // 五位の壁
                return up;
            }
            if (rating == MeritRating.下下) return Previous(current); // 貶位
            return current; // 据置
        }

        /// <summary>
        /// 位階を汎用 tier（<see cref="Office.requiredTier"/> 等と相互運用する整数序列）へ橋渡しする。
        /// 三位以上＝大臣/公卿級10–12、四位＝納言/参議級8、五位＝諸大夫/国守級6、六位4・七位3・八位2・初位1・無位0。
        /// </summary>
        public static int Tier(CourtRank r)
        {
            int s = (int)r;
            if (s >= (int)CourtRank.無位) return 0;
            if (s <= (int)CourtRank.従一位) return 12; // 一位
            if (s <= (int)CourtRank.従二位) return 11; // 二位
            if (s <= (int)CourtRank.従三位) return 10; // 三位（公卿）
            if (s <= (int)CourtRank.従四位下) return 8; // 四位
            if (s <= (int)CourtRank.従五位下) return 6; // 五位（貴族）
            if (s <= (int)CourtRank.従六位下) return 4; // 六位
            if (s <= (int)CourtRank.従七位下) return 3; // 七位
            if (s <= (int)CourtRank.従八位下) return 2; // 八位
            return 1;                                   // 初位
        }

        /// <summary>
        /// 官位相当＝役職が求める位階（<paramref name="officeRank"/>）に対する叙位者（<paramref name="holderRank"/>）の釣り合い。
        /// 上回れば格上（冗官）、下回れば格下（行・守の抜擢）、許容階差以内なら適任。<paramref name="toleranceSteps"/> 既定±2。
        /// </summary>
        public static AppointmentFit OfficeFitness(CourtRank holderRank, CourtRank officeRank, int toleranceSteps = 2)
        {
            if (toleranceSteps < 0) toleranceSteps = 0;
            int height = Compare(holderRank, officeRank); // 正＝holder が上位
            if (height > toleranceSteps) return AppointmentFit.格上;
            if (height < -toleranceSteps) return AppointmentFit.格下;
            return AppointmentFit.適任;
        }
    }
}
