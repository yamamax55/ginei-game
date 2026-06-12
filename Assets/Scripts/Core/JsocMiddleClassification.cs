using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 職業の中分類台帳（<b>日本標準職業分類 JSOC 中分類73群を参考</b>・#110 標準化・純ロジック・唯一の窓口）。
    /// 大分類（<see cref="OccupationClassificationRules"/>＝11群）の下に、JSOC の中分類（コード01〜73）を被せた<b>参照用の分類辞書</b>。
    /// <b>POP のシミュレーション状態ではない</b>（Province に 73 幅配列を持たせない＝集約・タイクン回避＝シミュは6種のまま大分類で回す）＝
    /// 分類・表示・照会のための lookup に徹する。各中分類は親大分類を持ち、台帳の整合（73件・各件が有効な大分類）はテストで担保。test-first。
    /// </summary>
    /// <remarks>
    /// 件数の内訳（合計73）：管理4／専門技術20／事務7／販売3／サービス8／保安3／農林漁業3／生産工程11／輸送機械運転5／建設採掘5／運搬清掃包装4。
    /// JSOC 大分類L「分類不能の職業（99）」は職業の中分類ではないため含めない（<see cref="OccupationCategory.無職"/> に中分類は無い）。
    /// </remarks>
    public static class JsocMiddleClassification
    {
        // JSOC 中分類 01〜73（コード順）。唯一の出所（二重定義しない）。親大分類は OccupationCategory（11群）へ写像。
        private static readonly JsocMiddleGroup[] table = BuildTable();

        private static JsocMiddleGroup[] BuildTable()
        {
            var A = OccupationCategory.管理;
            var B = OccupationCategory.専門技術;
            var C = OccupationCategory.事務;
            var D = OccupationCategory.販売;
            var E = OccupationCategory.サービス;
            var F = OccupationCategory.保安;
            var G = OccupationCategory.農林漁業;
            var H = OccupationCategory.生産工程;
            var I = OccupationCategory.輸送機械運転;
            var J = OccupationCategory.建設採掘;
            var K = OccupationCategory.運搬清掃包装;
            return new[]
            {
                // 大分類A 管理的職業従事者
                new JsocMiddleGroup( 1, "管理的公務員", A),
                new JsocMiddleGroup( 2, "法人・団体の役員", A),
                new JsocMiddleGroup( 3, "法人・団体の管理職員", A),
                new JsocMiddleGroup( 4, "その他の管理的職業従事者", A),
                // 大分類B 専門的・技術的職業従事者
                new JsocMiddleGroup( 5, "研究者", B),
                new JsocMiddleGroup( 6, "農林水産技術者", B),
                new JsocMiddleGroup( 7, "開発技術者", B),
                new JsocMiddleGroup( 8, "製造技術者（開発を除く）", B),
                new JsocMiddleGroup( 9, "建築・土木・測量技術者", B),
                new JsocMiddleGroup(10, "情報処理・通信技術者", B),
                new JsocMiddleGroup(11, "その他の技術者", B),
                new JsocMiddleGroup(12, "医師・歯科医師・獣医師・薬剤師", B),
                new JsocMiddleGroup(13, "保健師・助産師・看護師", B),
                new JsocMiddleGroup(14, "医療技術者", B),
                new JsocMiddleGroup(15, "その他の保健医療従事者", B),
                new JsocMiddleGroup(16, "社会福祉専門職業従事者", B),
                new JsocMiddleGroup(17, "法務従事者", B),
                new JsocMiddleGroup(18, "経営・金融・保険専門職業従事者", B),
                new JsocMiddleGroup(19, "教員", B),
                new JsocMiddleGroup(20, "宗教家", B),
                new JsocMiddleGroup(21, "著述家・記者・編集者", B),
                new JsocMiddleGroup(22, "美術家・デザイナー・写真家・映像撮影者", B),
                new JsocMiddleGroup(23, "音楽家・舞台芸術家", B),
                new JsocMiddleGroup(24, "その他の専門的職業従事者", B),
                // 大分類C 事務従事者
                new JsocMiddleGroup(25, "一般事務従事者", C),
                new JsocMiddleGroup(26, "会計事務従事者", C),
                new JsocMiddleGroup(27, "生産関連事務従事者", C),
                new JsocMiddleGroup(28, "営業・販売事務従事者", C),
                new JsocMiddleGroup(29, "外勤事務従事者", C),
                new JsocMiddleGroup(30, "運輸・郵便事務従事者", C),
                new JsocMiddleGroup(31, "事務用機器操作員", C),
                // 大分類D 販売従事者
                new JsocMiddleGroup(32, "商品販売従事者", D),
                new JsocMiddleGroup(33, "販売類似職業従事者", D),
                new JsocMiddleGroup(34, "営業職業従事者", D),
                // 大分類E サービス職業従事者
                new JsocMiddleGroup(35, "家庭生活支援サービス職業従事者", E),
                new JsocMiddleGroup(36, "介護サービス職業従事者", E),
                new JsocMiddleGroup(37, "保健医療サービス職業従事者", E),
                new JsocMiddleGroup(38, "生活衛生サービス職業従事者", E),
                new JsocMiddleGroup(39, "飲食物調理従事者", E),
                new JsocMiddleGroup(40, "接客・給仕職業従事者", E),
                new JsocMiddleGroup(41, "居住施設・ビル等管理人", E),
                new JsocMiddleGroup(42, "その他のサービス職業従事者", E),
                // 大分類F 保安職業従事者
                new JsocMiddleGroup(43, "自衛官", F),
                new JsocMiddleGroup(44, "司法警察職員", F),
                new JsocMiddleGroup(45, "その他の保安職業従事者", F),
                // 大分類G 農林漁業従事者
                new JsocMiddleGroup(46, "農業従事者", G),
                new JsocMiddleGroup(47, "林業従事者", G),
                new JsocMiddleGroup(48, "漁業従事者", G),
                // 大分類H 生産工程従事者
                new JsocMiddleGroup(49, "生産設備制御・監視従事者（金属製品）", H),
                new JsocMiddleGroup(50, "生産設備制御・監視従事者（金属製品を除く）", H),
                new JsocMiddleGroup(51, "機械組立設備制御・監視従事者", H),
                new JsocMiddleGroup(52, "金属材料製造・金属加工・金属溶接溶断従事者", H),
                new JsocMiddleGroup(53, "製品製造・加工処理従事者（金属製品を除く）", H),
                new JsocMiddleGroup(54, "機械組立従事者", H),
                new JsocMiddleGroup(55, "機械整備・修理従事者", H),
                new JsocMiddleGroup(56, "製品検査従事者（金属製品）", H),
                new JsocMiddleGroup(57, "製品検査従事者（金属製品を除く）", H),
                new JsocMiddleGroup(58, "機械検査従事者", H),
                new JsocMiddleGroup(59, "生産関連・生産類似作業従事者", H),
                // 大分類I 輸送・機械運転従事者
                new JsocMiddleGroup(60, "鉄道運転従事者", I),
                new JsocMiddleGroup(61, "自動車運転従事者", I),
                new JsocMiddleGroup(62, "船舶・航空機運転従事者", I),
                new JsocMiddleGroup(63, "その他の輸送従事者", I),
                new JsocMiddleGroup(64, "定置・建設機械運転従事者", I),
                // 大分類J 建設・採掘従事者
                new JsocMiddleGroup(65, "建設躯体工事従事者", J),
                new JsocMiddleGroup(66, "建設従事者（建設躯体工事従事者を除く）", J),
                new JsocMiddleGroup(67, "電気工事従事者", J),
                new JsocMiddleGroup(68, "土木作業従事者", J),
                new JsocMiddleGroup(69, "採掘従事者", J),
                // 大分類K 運搬・清掃・包装等従事者
                new JsocMiddleGroup(70, "運搬従事者", K),
                new JsocMiddleGroup(71, "清掃従事者", K),
                new JsocMiddleGroup(72, "包装従事者", K),
                new JsocMiddleGroup(73, "その他の運搬・清掃・包装等従事者", K),
            };
        }

        /// <summary>中分類の総数（JSOC＝73）。</summary>
        public static int Count => table.Length;

        /// <summary>全中分類（コード順・読み取り専用）。</summary>
        public static IReadOnlyList<JsocMiddleGroup> All => table;

        /// <summary>コード（1〜73）で中分類を引く。範囲外は null。</summary>
        public static JsocMiddleGroup ByCode(int code)
        {
            if (code < 1 || code > table.Length) return null;
            return table[code - 1]; // コードは1始まりの連番＝添字-1
        }

        /// <summary>コード→親大分類。範囲外は <see cref="OccupationCategory.無職"/>。</summary>
        public static OccupationCategory MajorOf(int code)
        {
            var g = ByCode(code);
            return g == null ? OccupationCategory.無職 : g.major;
        }

        /// <summary>コード→中分類名。範囲外は ""。</summary>
        public static string Name(int code)
        {
            var g = ByCode(code);
            return g == null ? "" : g.name;
        }

        /// <summary>コード→ゼロ詰め2桁文字列（"01"〜"73"）。範囲外は "—"。</summary>
        public static string FormatCode(int code)
        {
            var g = ByCode(code);
            return g == null ? "—" : g.CodeString;
        }

        /// <summary>指定した大分類に属する中分類の一覧（コード順）。</summary>
        public static List<JsocMiddleGroup> InMajor(OccupationCategory major)
        {
            var list = new List<JsocMiddleGroup>();
            for (int i = 0; i < table.Length; i++)
                if (table[i].major == major) list.Add(table[i]);
            return list;
        }

        /// <summary>指定した大分類に属する中分類の件数。</summary>
        public static int CountInMajor(OccupationCategory major)
        {
            int n = 0;
            for (int i = 0; i < table.Length; i++)
                if (table[i].major == major) n++;
            return n;
        }

        /// <summary>
        /// POP の少数6種（<see cref="Occupation"/>）→ 代表的な中分類コード（最も典型的な1件・大分類は <see cref="OccupationClassificationRules.MajorGroupOf(Occupation)"/> と一致）。
        /// 農民→46農業従事者／工員→53製品製造・加工処理従事者／鉱員→69採掘従事者／官吏→25一般事務従事者／軍属→43自衛官／無職→0（なし）。
        /// </summary>
        public static int RepresentativeMiddle(Occupation o)
        {
            switch (o)
            {
                case Occupation.農民: return 46;
                case Occupation.工員: return 53;
                case Occupation.鉱員: return 69;
                case Occupation.官吏: return 25;
                case Occupation.軍属: return 43;
                default:             return 0; // 無職＝中分類なし
            }
        }

        // ※ネームド人物（Person）は POP 職業分類に押し込まず別管理（PersonVocationRules）。中分類の人物オーバーロードは持たない。
    }
}
