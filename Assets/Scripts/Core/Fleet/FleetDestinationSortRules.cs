using System.Collections.Generic;

namespace Ginei
{
    /// <summary>艦隊メニュー③「目的の星系」の並べ替え条件。</summary>
    public enum DestinationSortKey
    {
        距離,
        星系名,
        所属,
        可否,
    }

    /// <summary>発令できるかどうかの三段階（並べ替えの順序に使う）。</summary>
    public enum DestinationVerdict
    {
        /// <summary>そのまま発令でき、目的地まで一気に行ける。</summary>
        可 = 0,
        /// <summary>発令はできるが注意がある（要塞封鎖・飛び石禁止で途中停止）。</summary>
        警告 = 1,
        /// <summary>発令できない（到達不能・現在地・交戦中など）。</summary>
        不可 = 2,
    }

    /// <summary>
    /// 並べ替えのための1行ぶんの素データ。表示文字列（理由の全文など）は Game 側が別に持ち、
    /// <see cref="order"/>（元の並び）で引き当てる＝Core は<b>並べ方だけ</b>を決める。
    /// </summary>
    public readonly struct DestinationRow
    {
        /// <summary>目的地の星系ID（選択の同定はこれで行う＝行番号で持たない）。</summary>
        public readonly int systemId;
        /// <summary>星系名（並べ替えの比較に使う。順序をロケールに依らせないため序数比較する）。</summary>
        public readonly string name;
        /// <summary>
        /// 所属の並び順。小さいほど手前に置く。呼び手が
        /// <see cref="FactionRelations"/> で決める（例：0=自勢力 1=非敵対 2=敵対 3=不明）
        /// ＝Core に「勢力が違えば敵」を直書きしない。
        /// </summary>
        public readonly int ownerRank;
        /// <summary>
        /// 実際に取る航路のホップ数。<see cref="Unreachable"/>（-1）＝到達不能。
        /// <b>到達不能を 0 ホップ（＝近い）として扱わない</b>ための番兵。
        /// </summary>
        public readonly int hops;
        /// <summary>発令の可否。</summary>
        public readonly DestinationVerdict verdict;
        /// <summary>元の並び（安定化と、Game 側の表示データの引き当てに使う）。</summary>
        public readonly int order;

        public DestinationRow(int systemId, string name, int ownerRank, int hops,
                              DestinationVerdict verdict, int order)
        {
            this.systemId = systemId;
            this.name = name ?? "";
            this.ownerRank = ownerRank;
            this.hops = hops;
            this.verdict = verdict;
            this.order = order;
        }

        /// <summary>到達不能か。</summary>
        public bool IsUnreachable => hops < 0;
    }

    /// <summary>
    /// 艦隊メニュー③「目的の星系」の並べ替え（純ロジック・test-first）。
    ///
    /// <b>決めごと</b>
    /// <list type="bullet">
    ///   <item><b>到達不能は距離の並べ替えで常に末尾</b>（昇順でも降順でも）。距離が「無い」ものを
    ///   0 ホップ＝最も近い、あるいは無限遠＝最も遠い、のどちらに寄せても嘘になるため、
    ///   順序の外に置く。ほかの条件（星系名・所属・可否）では到達不能でも普通に並べる
    ///   ＝名前順で探しているのに一部だけ下へ落ちる、という驚きを作らない。</item>
    ///   <item><b>同値は元の並び順</b>（安定ソート）。並べ替えを押すたびに同じ順になる＝決定論。</item>
    ///   <item>星系名の比較は<b>序数</b>（<see cref="string.CompareOrdinal"/>）＝実行環境のロケールで
    ///   順序が変わらない。</item>
    /// </list>
    ///
    /// 距離（<see cref="DestinationRow.hops"/>）は<b>実際に取る航路</b>から数えたものを渡す
    /// （<see cref="FleetOrderRules.PlannedRoute"/>）＝表示と発令が別の経路を指さない。
    /// </summary>
    public static class FleetDestinationSortRules
    {
        /// <summary>到達不能を表すホップ数。</summary>
        public const int Unreachable = -1;

        /// <summary>並べ替え条件の一覧（UI のボタンを作る順）。</summary>
        public static readonly DestinationSortKey[] AllKeys =
        {
            DestinationSortKey.距離,
            DestinationSortKey.星系名,
            DestinationSortKey.所属,
            DestinationSortKey.可否,
        };

        /// <summary>いまの並べ替えを1行で（例「距離 昇順」）。</summary>
        public static string HeaderLabel(DestinationSortKey key, bool ascending)
            => key + (ascending ? " 昇順" : " 降順");

        /// <summary>その条件で昇順にしたときの意味（ボタンの説明）。</summary>
        public static string AscendingMeaning(DestinationSortKey key)
        {
            switch (key)
            {
                case DestinationSortKey.距離: return "近い順";
                case DestinationSortKey.星系名: return "名前順";
                case DestinationSortKey.所属: return "自軍から";
                case DestinationSortKey.可否: return "発令できる順";
                default: return "";
            }
        }

        /// <summary>
        /// 並べ替える（その場で並び替え・安定）。<paramref name="rows"/> が null／1件以下なら何もしない。
        /// </summary>
        public static void Sort(List<DestinationRow> rows, DestinationSortKey key, bool ascending)
        {
            if (rows == null || rows.Count < 2) return;

            // 挿入ソート＝比較が安定でなくても順序が壊れないうえ、実装が短い。
            // ③の行数は上限つき（既定300）なので O(n^2) でも実用上問題にならない。
            // List.Sort は不安定（同値の順序が保証されない＝押すたびに並びが変わる）ので使わない。
            for (int i = 1; i < rows.Count; i++)
            {
                DestinationRow cur = rows[i];
                int j = i - 1;
                while (j >= 0 && Compare(rows[j], cur, key, ascending) > 0)
                {
                    rows[j + 1] = rows[j];
                    j--;
                }
                rows[j + 1] = cur;
            }
        }

        /// <summary>
        /// 2行の順序（&lt;0＝a が先）。<paramref name="ascending"/> は<b>主キーだけ</b>を反転させる。
        /// 同値のときの並び（<see cref="DestinationRow.order"/>）と、距離での到達不能の扱いは反転しない。
        /// </summary>
        public static int Compare(in DestinationRow a, in DestinationRow b, DestinationSortKey key, bool ascending)
        {
            int primary = ComparePrimary(a, b, key, ascending);
            if (primary != 0) return primary;
            return a.order.CompareTo(b.order);   // 安定化（昇降順で反転させない）
        }

        private static int ComparePrimary(in DestinationRow a, in DestinationRow b, DestinationSortKey key, bool ascending)
        {
            int sign = ascending ? 1 : -1;

            switch (key)
            {
                case DestinationSortKey.距離:
                    // 到達不能は昇順・降順のどちらでも末尾（距離の順序の外に置く）。
                    if (a.IsUnreachable != b.IsUnreachable) return a.IsUnreachable ? 1 : -1;
                    if (a.IsUnreachable) return 0;                     // どちらも到達不能＝同値
                    return sign * a.hops.CompareTo(b.hops);

                case DestinationSortKey.星系名:
                    return sign * string.CompareOrdinal(a.name, b.name);

                case DestinationSortKey.所属:
                    {
                        int byRank = a.ownerRank.CompareTo(b.ownerRank);
                        if (byRank != 0) return sign * byRank;
                        // 同じ所属のなかは名前順（見つけやすさ）。ここも主キーと同じ向きに倒す。
                        return sign * string.CompareOrdinal(a.name, b.name);
                    }

                case DestinationSortKey.可否:
                    return sign * ((int)a.verdict).CompareTo((int)b.verdict);

                default:
                    return 0;
            }
        }

        /// <summary>
        /// 発令できない理由から可否の三段階を出す唯一の窓口。
        /// <see cref="MoveOrderRejection.要塞封鎖"/> は<b>発令できる</b>（回り道が無いだけ）ので警告に落とす。
        /// <paramref name="stopsShort"/>＝飛び石禁止で途中の星系に一旦停止する（目的地まで一気に行かない）。
        /// </summary>
        public static DestinationVerdict VerdictOf(MoveOrderRejection reason, bool stopsShort)
        {
            if (reason == MoveOrderRejection.要塞封鎖) return DestinationVerdict.警告;
            if (reason != MoveOrderRejection.なし) return DestinationVerdict.不可;
            return stopsShort ? DestinationVerdict.警告 : DestinationVerdict.可;
        }
    }
}
