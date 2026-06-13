using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 部品表BOM（#983・純データ）の1ノード＝木構造。1つの品目（艦・ブロック・部品・素材）を表す。
    /// <see cref="itemId"/>（品目ID）＋<see cref="quantityPer"/>（親1個あたりの所要数量）＋
    /// <see cref="children"/>（この品目を構成する下位部品の木）を持つ。子が空＝これ以上分解されない末端＝原材料（調達対象）。
    /// 「艦1隻 → 部品N個 → 素材M個」を再帰的に展開する＝資材所要計画の基礎。
    /// 展開ロジックは <see cref="BomRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class BomNode
    {
        /// <summary>品目ID（艦/ブロック/部品/素材を識別）。</summary>
        public string itemId;
        /// <summary>親1個あたりのこの品目の所要数量（負はクランプ）。ルート自身の係数は通常1。</summary>
        public float quantityPer;
        /// <summary>この品目を構成する下位部品の木（空＝末端素材）。</summary>
        public List<BomNode> children;

        public BomNode() { }

        public BomNode(string itemId, float quantityPer = 1f, List<BomNode> children = null)
        {
            this.itemId = itemId;
            this.quantityPer = quantityPer;
            this.children = children;
        }

        /// <summary>直下の子部品の点数。</summary>
        public int ChildCount => children == null ? 0 : children.Count;
        /// <summary>子を持たない＝末端素材か。</summary>
        public bool IsLeaf => children == null || children.Count == 0;
    }

    /// <summary>部品表展開の調整係数。</summary>
    public readonly struct BomParams
    {
        /// <summary>再帰展開の最大階層深さ（無限再帰＝循環BOMの暴走を防ぐ上限・既定32）。</summary>
        public readonly int maxDepth;

        public BomParams(int maxDepth)
        {
            this.maxDepth = Mathf.Max(1, maxDepth);
        }

        /// <summary>既定＝最大階層深さ32（艦→ブロック→部品→素材を十分にまかなう）。</summary>
        public static BomParams Default => new BomParams(32);
    }

    /// <summary>
    /// 部品表BOM＝艦→部品→素材の多層展開の純ロジック（#983・唯一の窓口）。
    /// 1つの製品（艦）を構成する部品の木を再帰的に展開し、最終的に必要な素材の総量を積算する＝資材所要計画の基礎。
    /// BOMは製品を素材まで分解する木＝1隻の艦は数千の素材へ展開される。各階層の所要数量を<b>掛け合わせて</b>積算する
    /// （艦1→部品3→素材2なら素材6＝<see cref="Explode"/>）。
    /// <see cref="CoupledProductionRules"/>（連産＝1工程が複数財を固定比で同時産出する<b>結合</b>）とは別＝こちらは<b>分解</b>の木。
    /// <see cref="ShipyardRules"/>（造船の進捗・就役）の前段で必要素材を割り出し、所要計算<c>MrpRules</c>（バックログ・在庫差引）は
    /// この <see cref="Explode"/> の結果を入力に取る。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BomRules
    {
        /// <summary>
        /// 多層展開＝木を再帰的にたどり、末端素材ごとの総所要量を辞書で返す（BOMの核）。
        /// ルートを quantity 個作るとき、各階層の所要数量を掛け合わせて末端（葉）まで積算する
        /// （艦1隻×部品3個×素材2個＝素材6）。子を持つ中間ノードは集計に積まず葉のみを合算する（＝調達対象は原材料）。
        /// 同じ素材IDが複数の上位部品から要求されればその素材は合算される。quantity 負・null はクランプ。
        /// </summary>
        public static Dictionary<string, float> Explode(BomNode root, float quantity, BomParams p)
        {
            var result = new Dictionary<string, float>();
            float q = Mathf.Max(0f, quantity);
            if (root == null || q <= 0f) return result;
            // ルート自身の係数も掛ける（通常1だが多めに数えるなら係数で吸収）。
            float rootQty = q * Mathf.Max(0f, root.quantityPer);
            ExplodeInto(root, rootQty, result, p.maxDepth);
            return result;
        }

        public static Dictionary<string, float> Explode(BomNode root, float quantity)
            => Explode(root, quantity, BomParams.Default);

        /// <summary>
        /// 内部再帰＝node を accumulated 個作るのに必要な末端素材を result へ積算する。
        /// node が葉なら itemId に accumulated を合算、中間なら各子へ accumulated×子係数 で降りる。
        /// depthRemaining が尽きたら（循環BOM）打ち切り＝そのノードを葉として扱い暴走を防ぐ。
        /// </summary>
        private static void ExplodeInto(BomNode node, float accumulated, Dictionary<string, float> result, int depthRemaining)
        {
            if (node == null || accumulated <= 0f) return;

            // 末端 or 深さ上限到達＝素材として計上。
            if (node.IsLeaf || depthRemaining <= 1)
            {
                if (!string.IsNullOrEmpty(node.itemId))
                {
                    if (result.TryGetValue(node.itemId, out float cur))
                        result[node.itemId] = cur + accumulated;
                    else
                        result[node.itemId] = accumulated;
                }
                return;
            }

            var kids = node.children;
            for (int i = 0; i < kids.Count; i++)
            {
                BomNode child = kids[i];
                if (child == null) continue;
                float childQty = accumulated * Mathf.Max(0f, child.quantityPer);
                ExplodeInto(child, childQty, result, depthRemaining - 1);
            }
        }

        /// <summary>
        /// 特定素材の総所要量＝木を全階層たどって materialId の総量を合算（quantity 個分）。
        /// <see cref="Explode"/> の結果から1素材を引くのと同値（個別問い合わせ用の簡易窓口）。
        /// 見つからなければ0。quantity・materialId 不正は0。
        /// </summary>
        public static float TotalMaterialRequirement(BomNode root, float quantity, string materialId, BomParams p)
        {
            if (string.IsNullOrEmpty(materialId)) return 0f;
            var all = Explode(root, quantity, p);
            return all.TryGetValue(materialId, out float v) ? v : 0f;
        }

        public static float TotalMaterialRequirement(BomNode root, float quantity, string materialId)
            => TotalMaterialRequirement(root, quantity, materialId, BomParams.Default);

        /// <summary>
        /// 部品表の階層の深さ（艦→ブロック→部品→素材＝何段か）。葉のみ＝深さ1、子を1段持てば2…。
        /// 循環BOMは <see cref="BomParams.maxDepth"/> で打ち切る。null は0。
        /// </summary>
        public static int BomDepth(BomNode root, BomParams p)
        {
            if (root == null) return 0;
            return DepthOf(root, p.maxDepth);
        }

        public static int BomDepth(BomNode root) => BomDepth(root, BomParams.Default);

        private static int DepthOf(BomNode node, int budget)
        {
            if (node == null) return 0;
            if (node.IsLeaf || budget <= 1) return 1;
            int deepest = 0;
            var kids = node.children;
            for (int i = 0; i < kids.Count; i++)
            {
                int d = DepthOf(kids[i], budget - 1);
                if (d > deepest) deepest = d;
            }
            return 1 + deepest;
        }

        /// <summary>
        /// 末端素材の一覧（これ以上分解されない原材料＝調達対象のIDの集合）。重複は1回。
        /// <see cref="Explode"/> のキー集合と同じ（数量は問わず種類だけ欲しいとき用）。null は空。
        /// </summary>
        public static List<string> LeafMaterials(BomNode root, BomParams p)
        {
            var seen = new HashSet<string>();
            var ordered = new List<string>();
            if (root != null)
                CollectLeaves(root, seen, ordered, p.maxDepth);
            return ordered;
        }

        public static List<string> LeafMaterials(BomNode root) => LeafMaterials(root, BomParams.Default);

        private static void CollectLeaves(BomNode node, HashSet<string> seen, List<string> ordered, int budget)
        {
            if (node == null) return;
            if (node.IsLeaf || budget <= 1)
            {
                if (!string.IsNullOrEmpty(node.itemId) && seen.Add(node.itemId))
                    ordered.Add(node.itemId);
                return;
            }
            var kids = node.children;
            for (int i = 0; i < kids.Count; i++)
                CollectLeaves(kids[i], seen, ordered, budget - 1);
        }

        /// <summary>
        /// 総部品点数＝木の全ノード数（ルート含む・素材も部品も1点ずつ数える）。
        /// 同一IDが複数箇所に現れても各出現を別々に数える（木の節点の数＝設計上の構成点数）。
        /// 循環BOMは深さ上限で打ち切る。null は0。
        /// </summary>
        public static int ComponentCount(BomNode root, BomParams p)
        {
            if (root == null) return 0;
            return CountNodes(root, p.maxDepth);
        }

        public static int ComponentCount(BomNode root) => ComponentCount(root, BomParams.Default);

        private static int CountNodes(BomNode node, int budget)
        {
            if (node == null) return 0;
            int total = 1; // 自分自身
            if (!node.IsLeaf && budget > 1)
            {
                var kids = node.children;
                for (int i = 0; i < kids.Count; i++)
                    total += CountNodes(kids[i], budget - 1);
            }
            return total;
        }

        /// <summary>
        /// 共通部品の標準化による節約（0..1）＝複数の上位部品が同じ部品IDを使うほど大きい。
        /// 木の延べ節点数（<see cref="ComponentCount"/>−ルート）に対し、ユニークな品目IDは何種類か。
        /// 重複が無ければ0（節約なし）、品目が1種に集約されるほど1へ近づく＝部品の共通化＝標準化の利益。
        /// 式＝1 −（ユニーク品目数 ÷ 延べ品目出現数）。延べ出現が1以下なら0。null は0。
        /// </summary>
        public static float SharedComponentSavings(BomNode root, BomParams p)
        {
            if (root == null) return 0f;
            var seen = new HashSet<string>();
            int occurrences = CountOccurrences(root, seen, p.maxDepth);
            if (occurrences <= 1) return 0f;
            int unique = seen.Count;
            // 重複が無ければ unique==occurrences で 0、共通化が進むほど 1 へ。
            return Mathf.Clamp01(1f - (float)unique / occurrences);
        }

        public static float SharedComponentSavings(BomNode root) => SharedComponentSavings(root, BomParams.Default);

        /// <summary>延べ品目出現数を数えつつ、ユニークIDを seen に集める（IDが空の節点は出現に数えない）。</summary>
        private static int CountOccurrences(BomNode node, HashSet<string> seen, int budget)
        {
            if (node == null) return 0;
            int total = 0;
            if (!string.IsNullOrEmpty(node.itemId))
            {
                total = 1;
                seen.Add(node.itemId);
            }
            if (!node.IsLeaf && budget > 1)
            {
                var kids = node.children;
                for (int i = 0; i < kids.Count; i++)
                    total += CountOccurrences(kids[i], seen, budget - 1);
            }
            return total;
        }
    }
}
