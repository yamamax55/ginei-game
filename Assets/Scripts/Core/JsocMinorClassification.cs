using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 職業の小分類台帳（<b>日本標準職業分類 JSOC 小分類を参考</b>・#110 標準化・純ロジック・唯一の窓口）。
    /// 中分類（<see cref="JsocMiddleClassification"/>＝73群）の下に小分類を被せた<b>参照用の分類辞書</b>。
    /// <b>全329小分類は網羅せず</b>、(1)現在この作品に存在する職業（POP6種・人物の職分・経済アーキタイプ#2024/#2025 の業種）と
    /// (2)この宇宙設定でありえる職業（<see cref="JsocMinorGroup.isSetting"/>＝本作固有＝宇宙船操縦士・テラフォーミング技師等）を curate して載せる。
    /// POP のシミュ状態ではない（lookup に徹する＝集約・タイクン回避）。各小分類は親中分類を持ち、台帳の整合はテストで担保。test-first。
    /// </summary>
    public static class JsocMinorClassification
    {
        // 小分類（中分類プレフィックスの3桁コード）。唯一の出所。F=JSOC由来 / T=本作固有（宇宙設定の拡張）。
        private static readonly JsocMinorGroup[] table = BuildTable();

        private static JsocMinorGroup[] BuildTable()
        {
            return new[]
            {
                // === 大分類A 管理 ===
                new JsocMinorGroup("011", "管理的国家公務員", 1),
                new JsocMinorGroup("012", "管理的地方公務員（星系総督・惑星知事等）", 1),
                new JsocMinorGroup("021", "会社役員", 2),
                new JsocMinorGroup("031", "管理的職員（部課長級）", 3),
                // === 大分類B 専門技術 ===
                new JsocMinorGroup("051", "自然科学系研究者", 5),
                new JsocMinorGroup("071", "電気・電子・情報開発技術者", 7),
                new JsocMinorGroup("072", "宇宙航行システム開発技術者", 7, true),
                new JsocMinorGroup("091", "建築・土木技術者", 9),
                new JsocMinorGroup("092", "テラフォーミング技師", 9, true),
                new JsocMinorGroup("101", "システムエンジニア", 10),
                new JsocMinorGroup("102", "通信ネットワーク技術者", 10),
                new JsocMinorGroup("111", "機械技術者", 11),
                new JsocMinorGroup("112", "艦艇設計技術者", 11, true),
                new JsocMinorGroup("121", "医師", 12),
                new JsocMinorGroup("161", "社会福祉専門職", 16),
                new JsocMinorGroup("171", "法務従事者", 17),
                new JsocMinorGroup("181", "金融・保険専門職", 18),
                new JsocMinorGroup("191", "学校教員", 19),
                new JsocMinorGroup("211", "記者・編集者", 21),
                new JsocMinorGroup("221", "デザイナー", 22),
                new JsocMinorGroup("231", "音楽家・芸能人", 23),
                // === 大分類C 事務 ===
                new JsocMinorGroup("251", "総務・人事事務員", 25),
                new JsocMinorGroup("252", "行政事務員（文官）", 25),
                new JsocMinorGroup("261", "会計・経理事務員", 26),
                new JsocMinorGroup("301", "運輸・郵便事務員", 30),
                // === 大分類D 販売 ===
                new JsocMinorGroup("321", "小売店販売員", 32),
                new JsocMinorGroup("341", "商社・卸売営業員", 34),
                // === 大分類E サービス ===
                new JsocMinorGroup("361", "介護職員", 36),
                new JsocMinorGroup("391", "調理人", 39),
                new JsocMinorGroup("401", "接客・給仕係", 40),
                // === 大分類F 保安 ===
                new JsocMinorGroup("431", "宇宙艦隊将兵（軍人）", 43, true),
                new JsocMinorGroup("432", "自衛官", 43),
                new JsocMinorGroup("441", "警察官", 44),
                new JsocMinorGroup("451", "警備員", 45),
                // === 大分類G 農林漁業 ===
                new JsocMinorGroup("461", "農耕従事者", 46),
                new JsocMinorGroup("462", "施設園芸・垂直農法従事者", 46, true),
                new JsocMinorGroup("481", "漁労従事者", 48),
                // === 大分類H 生産工程 ===
                new JsocMinorGroup("531", "製品製造・加工従事者", 53),
                new JsocMinorGroup("532", "兵器製造工", 53, true),
                new JsocMinorGroup("541", "機械組立工", 54),
                new JsocMinorGroup("542", "宇宙艦艇組立工", 54, true),
                new JsocMinorGroup("551", "機械整備・修理工", 55),
                new JsocMinorGroup("552", "艦艇整備工（補給廠）", 55, true),
                // === 大分類I 輸送・機械運転 ===
                new JsocMinorGroup("601", "鉄道運転士", 60),
                new JsocMinorGroup("602", "宇宙列車運転士", 60, true),
                new JsocMinorGroup("621", "船舶運航従事者", 62),
                new JsocMinorGroup("622", "宇宙船操縦士（航宙士）", 62, true),
                new JsocMinorGroup("623", "ワープ航法士", 62, true),
                new JsocMinorGroup("641", "建設機械運転士", 64),
                // === 大分類J 建設・採掘 ===
                new JsocMinorGroup("681", "土木作業員", 68),
                new JsocMinorGroup("691", "採掘従事者", 69),
                new JsocMinorGroup("692", "小惑星・宇宙採掘員", 69, true),
                // === 大分類K 運搬・清掃・包装 ===
                new JsocMinorGroup("701", "運搬作業員", 70),
                new JsocMinorGroup("702", "軌道港湾荷役員", 70, true),
                new JsocMinorGroup("711", "清掃員", 71),
                new JsocMinorGroup("721", "包装作業員", 72),
            };
        }

        /// <summary>小分類の総数（curate 済み）。</summary>
        public static int Count => table.Length;

        /// <summary>全小分類（コード順・読み取り専用）。</summary>
        public static IReadOnlyList<JsocMinorGroup> All => table;

        /// <summary>コードで小分類を引く。無ければ null。</summary>
        public static JsocMinorGroup ByCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return null;
            for (int i = 0; i < table.Length; i++)
                if (table[i].code == code) return table[i];
            return null;
        }

        /// <summary>コード→親中分類コード（1〜73）。無ければ0。</summary>
        public static int MiddleOf(string code)
        {
            var g = ByCode(code);
            return g == null ? 0 : g.middleCode;
        }

        /// <summary>コード→親大分類（中分類経由）。無ければ <see cref="OccupationCategory.無職"/>。</summary>
        public static OccupationCategory MajorOf(string code)
        {
            var g = ByCode(code);
            return g == null ? OccupationCategory.無職 : JsocMiddleClassification.MajorOf(g.middleCode);
        }

        /// <summary>コード→小分類名。無ければ ""。</summary>
        public static string Name(string code)
        {
            var g = ByCode(code);
            return g == null ? "" : g.name;
        }

        /// <summary>本作固有（宇宙設定で足した職業）か。</summary>
        public static bool IsSetting(string code)
        {
            var g = ByCode(code);
            return g != null && g.isSetting;
        }

        /// <summary>指定中分類に属する小分類の一覧（コード順）。</summary>
        public static List<JsocMinorGroup> InMiddle(int middleCode)
        {
            var list = new List<JsocMinorGroup>();
            for (int i = 0; i < table.Length; i++)
                if (table[i].middleCode == middleCode) list.Add(table[i]);
            return list;
        }

        /// <summary>本作固有の小分類だけ（宇宙設定の拡張＝宇宙船操縦士・テラフォーミング技師等）。</summary>
        public static List<JsocMinorGroup> SettingMinors()
        {
            var list = new List<JsocMinorGroup>();
            for (int i = 0; i < table.Length; i++)
                if (table[i].isSetting) list.Add(table[i]);
            return list;
        }

        /// <summary>本作固有の小分類の件数。</summary>
        public static int CountSetting
        {
            get { int n = 0; for (int i = 0; i < table.Length; i++) if (table[i].isSetting) n++; return n; }
        }

        /// <summary>
        /// POP の少数6種（<see cref="Occupation"/>）→ 代表的な小分類コード（親中分類は <see cref="JsocMiddleClassification.RepresentativeMiddle(Occupation)"/> と一致）。
        /// 農民→461農耕／工員→531製品製造加工／鉱員→691採掘／官吏→252行政事務員／軍属→431宇宙艦隊将兵／無職→""。
        /// </summary>
        public static string RepresentativeMinor(Occupation o)
        {
            switch (o)
            {
                case Occupation.農民: return "461";
                case Occupation.工員: return "531";
                case Occupation.鉱員: return "691";
                case Occupation.官吏: return "252";
                case Occupation.軍属: return "431";
                default:             return ""; // 無職＝小分類なし
            }
        }
    }
}
