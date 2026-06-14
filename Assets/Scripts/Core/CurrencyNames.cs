namespace Ginei
{
    /// <summary>
    /// 勢力通貨の名前プール（#通貨）。各勢力に固有名の通貨を決定論で割り当てる唯一の窓口。
    /// 旗艦名 <see cref="HeritageShipNames"/> の通貨版＝固定の真実の源。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CurrencyNames
    {
        /// <summary>通貨名プール（重複なし・並び順がインデックス割り当ての基準）。</summary>
        public static readonly string[] Names =
        {
            "アランド", "ドル", "マルク", "フラン", "ルピア", "ルーブル",
            "リラ", "ペソ", "ポンド", "バーツ", "レアル",
        };

        /// <summary>プールの件数。</summary>
        public static int Count => Names.Length;

        /// <summary>勢力に対応する通貨名（決定論＝勢力 enum の序数でプールを割り当て・循環）。</summary>
        public static string For(Faction faction)
        {
            int i = (int)faction;
            if (i < 0) i = -i;
            return Names.Length == 0 ? "" : Names[i % Names.Length];
        }

        /// <summary>インデックス指定の通貨名（範囲外は循環・負も安全）。</summary>
        public static string At(int index)
        {
            if (Names.Length == 0) return "";
            int i = index % Names.Length;
            if (i < 0) i += Names.Length;
            return Names[i];
        }
    }
}
