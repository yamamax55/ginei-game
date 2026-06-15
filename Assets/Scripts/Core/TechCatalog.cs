using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 技術ノードの表示メタデータ（カタログ専用・読み取り）。前提依存グラフ（<see cref="TechNode"/>）は
    /// 数値しか持たないため、表示名・研究分野（<see cref="ResearchField"/>）・説明をここで添える。
    /// </summary>
    public readonly struct TechInfo
    {
        public readonly string techId;
        public readonly string displayName;
        public readonly ResearchField field;
        public readonly string description;

        public TechInfo(string techId, string displayName, ResearchField field, string description)
        {
            this.techId = techId;
            this.displayName = displayName;
            this.field = field;
            this.description = description;
        }

        public bool IsValid => !string.IsNullOrEmpty(techId);
    }

    /// <summary>
    /// 研究ツリーの具体カタログ（#123-127／Almagest #1065 の content 層・唯一の真実）。
    /// <see cref="TechTreeRules"/>（前提依存の純グラフ）が動く <b>実際の技術一覧</b>を curate して持つ＝
    /// 軍事/生産/情報/社会の4分野（<see cref="ResearchField"/>）× 各3ティアの少数ノード（タイクン回避＝
    /// 全 SKU 展開しない・集約）。基礎技術（前提なし＝木の根）→前提充足で応用が解禁される依存ツリーを成す。
    /// `CommodityCatalog`/`RecipeBook` と同型の static カタログ＝一度組んでキャッシュ（決定論・冪等）。
    /// 数式・解禁判定は <see cref="TechTreeRules"/>、進捗は <see cref="TechProgressRules"/> が窓口（ここは content のみ）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TechCatalog
    {
        private static List<TechNode> _nodes;
        private static Dictionary<string, TechInfo> _info;

        /// <summary>技術ノード一覧（前提依存グラフ＝<see cref="TechTreeRules"/> に渡す）。読み取り専用で扱う（破壊しない）。</summary>
        public static List<TechNode> Nodes
        {
            get { EnsureBuilt(); return _nodes; }
        }

        /// <summary>登録技術の総数。</summary>
        public static int Count
        {
            get { EnsureBuilt(); return _nodes.Count; }
        }

        /// <summary>技術IDの表示メタデータ（未登録は無効な <see cref="TechInfo"/>）。</summary>
        public static TechInfo Info(string techId)
        {
            EnsureBuilt();
            if (!string.IsNullOrEmpty(techId) && _info.TryGetValue(techId, out var ti)) return ti;
            return default;
        }

        /// <summary>技術の表示名（未登録は techId をそのまま返す＝表示を止めない）。</summary>
        public static string DisplayName(string techId)
        {
            TechInfo ti = Info(techId);
            return ti.IsValid ? ti.displayName : (techId ?? "");
        }

        /// <summary>技術の研究分野（未登録は <see cref="ResearchField.軍事"/> 既定）。</summary>
        public static ResearchField FieldOf(string techId)
        {
            TechInfo ti = Info(techId);
            return ti.IsValid ? ti.field : ResearchField.軍事;
        }

        /// <summary>その分野の技術ノードだけ（出現順）。</summary>
        public static List<TechNode> NodesInField(ResearchField field)
        {
            EnsureBuilt();
            var result = new List<TechNode>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                TechNode n = _nodes[i];
                if (n != null && FieldOf(n.techId) == field) result.Add(n);
            }
            return result;
        }

        // ===== 構築（一度だけ・キャッシュ） =====

        private static void EnsureBuilt()
        {
            if (_nodes != null) return;
            _nodes = new List<TechNode>();
            _info = new Dictionary<string, TechInfo>();

            // 軍事（専制が得意）：火力・装甲・ミサイルの系統。
            Add("粒子ビーム",        ResearchField.軍事, 2f, 1, null, "艦砲の基礎＝指向性エネルギー兵器の礎。");
            Add("集束ビーム",        ResearchField.軍事, 4f, 2, P("粒子ビーム"), "ビームを集束し貫通力を上げる。");
            Add("指向性ゼッフル粒子", ResearchField.軍事, 6f, 3, P("集束ビーム"), "戦域を満たす粒子で一斉砲撃の威力を極める。");
            Add("装甲技術",          ResearchField.軍事, 2f, 1, null, "艦体防御の基礎＝多層装甲板。");
            Add("複合装甲",          ResearchField.軍事, 4f, 2, P("装甲技術"), "新素材の複合装甲で被弾耐性を高める。");
            Add("対艦ミサイル",      ResearchField.軍事, 4f, 2, P("粒子ビーム"), "射程外からの飽和攻撃を可能にする。");

            // 生産（商業が得意）：造船・資源精錬の系統。
            Add("規格化生産",        ResearchField.生産, 2f, 1, null, "部品の規格統一で建艦の土台を作る。");
            Add("自動化造船",        ResearchField.生産, 4f, 2, P("規格化生産"), "船渠の自動化で建艦速度を上げる。");
            Add("大量生産方式",      ResearchField.生産, 6f, 3, P("自動化造船"), "ライン生産で艦隊を一挙に揃える。");
            Add("資源精錬",          ResearchField.生産, 2f, 1, null, "希少資源の精錬効率を高める。");
            Add("反応炉技術",        ResearchField.生産, 4f, 2, P("資源精錬"), "高出力反応炉で艦の動力と産出を底上げ。");

            // 情報（技術志向が得意）：索敵・通信・電子戦の系統。
            Add("索敵レーダー",      ResearchField.情報, 2f, 1, null, "戦域の索敵の基礎。");
            Add("長距離索敵",        ResearchField.情報, 4f, 2, P("索敵レーダー"), "遠距離の敵艦隊を先んじて捉える。");
            Add("電子戦システム",    ResearchField.情報, 6f, 3, P("長距離索敵"), "妨害と欺瞞で敵の指揮を乱す。");
            Add("量子通信",          ResearchField.情報, 2f, 1, null, "遅延なき艦隊間通信の基礎。");
            Add("暗号解読",          ResearchField.情報, 4f, 2, P("量子通信"), "敵通信の傍受・解読で先手を取る。");

            // 社会（民主が得意）：軍政・教育・兵站の系統。
            Add("軍政改革",          ResearchField.社会, 2f, 1, null, "指揮系統と人事の近代化の土台。");
            Add("士官教育制度",      ResearchField.社会, 4f, 2, P("軍政改革"), "士官学校の体系化で将才を育てる。");
            Add("用兵思想",          ResearchField.社会, 6f, 3, P("士官教育制度"), "機動と集中の用兵ドクトリンを確立する。");
            Add("兵站学",            ResearchField.社会, 2f, 1, null, "補給線維持の学問的基礎。");
            Add("補給艦隊編制",      ResearchField.社会, 4f, 2, P("兵站学"), "前線を支える補給艦隊を編制する。");
        }

        private static List<string> P(params string[] prereqs) => new List<string>(prereqs);

        private static void Add(string techId, ResearchField field, float cost, int tier, List<string> prereqs, string desc)
        {
            _nodes.Add(new TechNode(techId, prereqs, cost, tier));
            _info[techId] = new TechInfo(techId, techId, field, desc);
        }
    }
}
