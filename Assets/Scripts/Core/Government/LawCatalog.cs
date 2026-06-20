using System.Collections.Generic;

namespace Ginei
{
    /// <summary>法令の種別（#法令タブ）。憲法（最高法規）⊃ 法律（議会/勅定）⊃ 政令（行政命令）の階層。</summary>
    public enum LawCategory { 憲法, 法律, 政令 }

    /// <summary>1件の法令（種別＋題目＋要旨＋適用思想）。観測用の content（read-only）。</summary>
    public readonly struct LawArticle
    {
        public readonly LawCategory category;
        public readonly string title;
        public readonly string summary;
        /// <summary>適用される政体思想（""＝共通／"民主"／"専制"）。</summary>
        public readonly string ideology;

        public LawArticle(LawCategory category, string title, string summary, string ideology)
        {
            this.category = category;
            this.title = title;
            this.summary = summary;
            this.ideology = ideology;
        }

        /// <summary>共通法（どの政体にも適用）か。</summary>
        public bool IsCommon => string.IsNullOrEmpty(ideology);
    }

    /// <summary>
    /// 法令カタログ（#法令タブ・content 層）。<see cref="LawCategory"/>（憲法/法律/政令）ごとの条文一覧を curate して持つ
    /// （`TechCatalog`/`MythicPlanetNames` と同型の static カタログ）。共通法＋政体（民主/専制）固有の法を分けて保持し、
    /// `LawObserverOverlay` の憲法/法律/政令タブが勢力の政体に応じて表示する。法の支配指数・治安の数値ロジック
    /// （<see cref="RuleOfLawRules"/>/<see cref="LawTickRules"/>）とは別＝こちらは「どんな法があるか」の content。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class LawCatalog
    {
        private static List<LawArticle> _all;

        /// <summary>全法令（全種別・全思想）。読み取り専用。</summary>
        public static IReadOnlyList<LawArticle> All { get { EnsureBuilt(); return _all; } }

        /// <summary>
        /// 指定の種別・政体思想に適用される法令を返す（共通法＋その思想固有法＝出現順）。
        /// <paramref name="ideology"/> が空なら共通法のみ。
        /// </summary>
        public static List<LawArticle> For(LawCategory category, string ideology)
        {
            EnsureBuilt();
            var result = new List<LawArticle>();
            for (int i = 0; i < _all.Count; i++)
            {
                LawArticle a = _all[i];
                if (a.category != category) continue;
                if (a.IsCommon || a.ideology == ideology) result.Add(a);
            }
            return result;
        }

        // ===== 構築（一度だけ・キャッシュ） =====

        private static void EnsureBuilt()
        {
            if (_all != null) return;
            _all = new List<LawArticle>();

            // ── 憲法（最高法規）──
            Add(LawCategory.憲法, "宇宙航行の自由", "恒星間航路は万民に開かれ、何人もこれを独占できない。", "");
            // 民主（自由惑星同盟）
            Add(LawCategory.憲法, "自由惑星同盟憲章", "自由と民主主義を国是とし、専制への抵抗権をうたう前文。", "民主");
            Add(LawCategory.憲法, "基本的人権の保障", "言論・信教・結社の自由と適正手続を全市民に保障する。", "民主");
            Add(LawCategory.憲法, "主権在民・三権分立", "主権は市民にあり、立法・行政・司法を分立させる。", "民主");
            Add(LawCategory.憲法, "文民統制条項", "軍は文民政府の統制に服し、政治へ介入してはならない。", "民主");
            // 専制（銀河帝国）
            Add(LawCategory.憲法, "銀河帝国国体法", "皇帝を国家の元首と定め、帝室の神聖不可侵を宣する。", "専制");
            Add(LawCategory.憲法, "皇帝大権", "統治権・統帥権を皇帝が総攬する（万機親裁）。", "専制");
            Add(LawCategory.憲法, "門閥貴族の特権", "貴族の世襲・領地・爵位の特権を認める。", "専制");
            Add(LawCategory.憲法, "臣民の忠誠義務", "臣民は皇帝と帝国に忠誠を尽くす義務を負う。", "専制");

            // ── 法律（議会の制定法／勅定法）──
            Add(LawCategory.法律, "刑法", "犯罪と刑罰を定める一般法。", "");
            Add(LawCategory.法律, "民法", "財産・契約・家族など私人間の関係を定める。", "");
            Add(LawCategory.法律, "通商航行法", "星系間の交易と航行の規律。関税・検疫を含む。", "");
            Add(LawCategory.法律, "兵役法", "徴募・服役・動員の要件を定める。", "");
            Add(LawCategory.法律, "情報公開法", "政府の保有情報を市民へ開示する義務を課す。", "民主");
            Add(LawCategory.法律, "公職選挙法", "評議会・公職の選出手続を定める。", "民主");
            Add(LawCategory.法律, "不敬罪法", "皇帝・帝室への不敬を処罰する。", "専制");
            Add(LawCategory.法律, "身分制度法", "貴族・平民・農奴の身分と権利義務を定める。", "専制");

            // ── 政令（行政命令）──
            Add(LawCategory.政令, "物資統制令", "戦時の重要物資の配給・価格を統制する。", "");
            Add(LawCategory.政令, "戒厳令", "非常時に軍が治安・行政を一時掌握する。", "");
            Add(LawCategory.政令, "徴用令", "労働力・船舶を国家目的に徴用する。", "");
            Add(LawCategory.政令, "非常事態宣言手続令", "議会の承認を要する非常大権の発動手続。", "民主");
            Add(LawCategory.政令, "勅令", "皇帝の名で発する命令。法律に準じる効力を持つ。", "専制");
        }

        private static void Add(LawCategory cat, string title, string summary, string ideology)
            => _all.Add(new LawArticle(cat, title, summary, ideology));
    }
}
