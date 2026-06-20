namespace Ginei
{
    /// <summary>
    /// 主人公の出自（生まれ・採用「出自選択」）。平民＝叩き上げ（士官学校から身を立てる）／貴族＝門閥・地方権力の家／
    /// 王家＝継承者（いずれ君主）。視点は一人称固定のまま“スタートの段と絡む政治”が変わる＝どれを選んでも god にならない。
    /// </summary>
    public enum PersonOrigin { 平民, 貴族, 王家 }

    /// <summary>
    /// 主人公の出自の純ロジック（採用「出自選択」・唯一の窓口）。出自は<b>身分の分類と入口</b>だけを持ち、実際の効果は
    /// 既存システムへ“接続のみ”＝貴族/王家は封建・貴族制(#168)・地方自治(#1306)、王家は王室(#188)・王室教育
    /// (<c>RoyalEducationRules</c>)・継承(#646)へ繋ぐ（数値は各窓口・本ルールは分類のみ＝additive・並行系を作らない）。
    /// 平民は門地開放(#169)で士官学校から登用される。決定論・test-first。
    /// </summary>
    public static class OriginRules
    {
        /// <summary>既定の出自（後方互換＝従来どおり平民の叩き上げ）。</summary>
        public const PersonOrigin Default = PersonOrigin.平民;

        /// <summary>貴族身分か（貴族・王家）＝爵位/門閥(#168)・地方権力(#1306)に連なる。</summary>
        public static bool IsNoble(PersonOrigin o) => o == PersonOrigin.貴族 || o == PersonOrigin.王家;

        /// <summary>王族か（王家）＝王室(#188)・王室教育・継承(#646)の対象＝いずれ君主になりうる。</summary>
        public static bool IsRoyal(PersonOrigin o) => o == PersonOrigin.王家;

        /// <summary>平民（叩き上げ）か＝門地開放(#169)で士官学校から身を立てる。</summary>
        public static bool IsCommoner(PersonOrigin o) => o == PersonOrigin.平民;

        /// <summary>主人公の死で操作座をどう継ぐか＝貴族/王家は継承(#646)で世継ぎへ、平民は一代記の完結→新規（#907 の解答）。</summary>
        public static bool InheritsOnDeath(PersonOrigin o) => IsNoble(o);

        /// <summary>表示用の短い肩書き（一代記・執務机）。</summary>
        public static string Title(PersonOrigin o)
        {
            switch (o)
            {
                case PersonOrigin.貴族: return "貴族の出";
                case PersonOrigin.王家: return "王家の出";
                default: return "平民の出（叩き上げ）";
            }
        }
    }
}
