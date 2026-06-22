namespace Ginei
{
    /// <summary>
    /// 主人公の出自（生まれ・採用「出自選択」）。平民＝叩き上げ（士官学校から身を立てる）／貴族＝門閥・地方権力の家／
    /// 王家＝継承者（いずれ君主）。視点は一人称固定のまま“スタートの段と絡む政治”が変わる＝どれを選んでも god にならない。
    /// </summary>
    public enum PersonOrigin { 平民, 貴族, 王家 }

    /// <summary>出自ごとの軍歴の入口（採用「出自選択」）。平民＝士官学校／貴族＝幼年学校（→士官学校）／王家＝王室教育。
    /// 実体の選抜・修業は既存（<c>MilitaryAcademyRules</c>/<c>RoyalEducationRules</c>/#155）へ接続する＝本列挙は入口の分類のみ。</summary>
    public enum EnrollPath { 士官学校, 幼年学校, 王室教育 }

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

        /// <summary>
        /// 出自による初任等級の下限（採用「出自選択」＝“スタートの段”が変わる）。平民0＝士官学校の結果のまま（叩き上げ）／
        /// 貴族3＝大尉相当（幼年学校・門閥の下駄）／王家4＝少佐相当（王室教育）。実際の任官は<b>この下限と学校結果の高い方</b>。
        /// 数値は控えめな既定＝<b>要・作者調整</b>（門地開放#169 で平民も上り、貴族/王家は入口で先行する程度）。
        /// </summary>
        public static int CommissionFloor(PersonOrigin o)
        {
            switch (o)
            {
                case PersonOrigin.貴族: return 3;
                case PersonOrigin.王家: return 4;
                default: return 0;
            }
        }

        /// <summary>出自の軍歴の入口（学校）。平民→士官学校／貴族→幼年学校／王家→王室教育（実体の選抜は既存へ接続）。</summary>
        public static EnrollPath PathFor(PersonOrigin o)
        {
            switch (o)
            {
                case PersonOrigin.貴族: return EnrollPath.幼年学校;
                case PersonOrigin.王家: return EnrollPath.王室教育;
                default: return EnrollPath.士官学校;
            }
        }

        /// <summary>入口の学校名（表示・一代記）。</summary>
        public static string SchoolName(EnrollPath p)
        {
            switch (p)
            {
                case EnrollPath.幼年学校: return "幼年学校";
                case EnrollPath.王室教育: return "王室教育課程";
                default: return "士官学校";
            }
        }

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
