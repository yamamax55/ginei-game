using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 技術ツリー（Almagest・#1065・純データ）の1ノード＝1つの技術。
    /// <see cref="techId"/>（技術ID）＋<see cref="prerequisites"/>（前提技術IDリスト＝これらを全て習得して初めて解禁）＋
    /// <see cref="researchCost"/>（この技術の研究コスト）＋<see cref="tier"/>（ツリー上のティア＝段の目安）を持つ。
    /// 前提が空＝基礎技術（最初から研究可能な木の根）。技術は前提依存のグラフを成す＝基礎を積まねば応用に届かない。
    /// 配線・解禁ロジックは <see cref="TechTreeRules"/> が唯一の窓口。
    /// </summary>
    [System.Serializable]
    public class TechNode
    {
        /// <summary>技術ID（技術を識別）。</summary>
        public string techId;
        /// <summary>前提技術IDリスト（全て習得済みで解禁＝1つでも欠けると解禁されない）。空＝基礎技術。</summary>
        public List<string> prerequisites;
        /// <summary>この技術の研究コスト（負はクランプ）。</summary>
        public float researchCost;
        /// <summary>ツリー上のティア（段の目安・表示用。実際の深さは <see cref="TechTreeRules.TechDepth"/> が計算）。</summary>
        public int tier;

        public TechNode() { }

        public TechNode(string techId, List<string> prerequisites = null, float researchCost = 1f, int tier = 0)
        {
            this.techId = techId;
            this.prerequisites = prerequisites;
            this.researchCost = researchCost;
            this.tier = tier;
        }

        /// <summary>前提技術の点数（0＝基礎技術）。</summary>
        public int PrerequisiteCount => prerequisites == null ? 0 : prerequisites.Count;
        /// <summary>前提を持たない＝基礎技術（木の根）か。</summary>
        public bool IsRoot => prerequisites == null || prerequisites.Count == 0;
    }

    /// <summary>技術ツリー配線の調整係数。</summary>
    public readonly struct TechTreeParams
    {
        /// <summary>深さ計算の再帰の最大階層（循環依存＝暴走を防ぐ上限・既定32）。</summary>
        public readonly int maxDepth;

        public TechTreeParams(int maxDepth)
        {
            this.maxDepth = Mathf.Max(1, maxDepth);
        }

        /// <summary>既定＝最大階層深さ32（基礎→応用を十分にまかなう）。</summary>
        public static TechTreeParams Default => new TechTreeParams(32);
    }

    /// <summary>
    /// 技術ツリー配線＝基礎技術→前提充足で新技術が出現する純ロジック（Almagest・#1065・唯一の窓口）。
    /// 技術は前提依存のグラフを成す＝前提技術を全て習得すると次の技術が解禁される（技術の依存ツリー）。
    /// 「技術は前提依存のツリー＝基礎を積まねば応用に届かない」を式・テストで担保する＝飛び級（前提飛ばし）は不可。
    /// <see cref="ResearchRules"/>（研究進捗＝1技術を時間で研究する出力計算）とは別＝こちらは<b>技術間の前提依存グラフ</b>（どれが解禁されるか）。
    /// <see cref="DisclosureRules"/>（秘史の開示連鎖）・<see cref="BomRules"/>（部品の依存木＝分解）とも別系統だが、
    /// 「前提が揃って初めて次へ」という連鎖の骨格は同型。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class TechTreeRules
    {
        /// <summary>
        /// 解禁可能か＝前提技術を<b>全て</b>習得済みか（1つでも欠けると解禁されない）。
        /// 既習得（researchedTechs に techId が含まれる）の技術はそれ以上解禁対象でない＝false。
        /// 基礎技術（前提なし）は未習得なら常に解禁可能。node・researchedTechs 不正は false。
        /// </summary>
        public static bool IsUnlockable(TechNode node, IList<string> researchedTechs)
        {
            if (node == null || string.IsNullOrEmpty(node.techId)) return false;
            // 既に習得済みなら解禁対象でない。
            if (Contains(researchedTechs, node.techId)) return false;
            // 前提を全て習得しているか。
            var pre = node.prerequisites;
            if (pre == null) return true;
            for (int i = 0; i < pre.Count; i++)
            {
                string need = pre[i];
                if (string.IsNullOrEmpty(need)) continue;
                if (!Contains(researchedTechs, need)) return false;
            }
            return true;
        }

        /// <summary>
        /// 前提充足＝この技術の前提のうち、あといくつ習得が必要か（0＝前提が揃った＝解禁直前）。
        /// 既に習得済みの前提は数えない。前提なし（基礎技術）は常に0。node 不正は0扱い（解禁を阻む前提が無い）。
        /// </summary>
        public static int PrerequisitesMissing(TechNode node, IList<string> researchedTechs)
        {
            if (node == null || node.prerequisites == null) return 0;
            int missing = 0;
            var pre = node.prerequisites;
            for (int i = 0; i < pre.Count; i++)
            {
                string need = pre[i];
                if (string.IsNullOrEmpty(need)) continue;
                if (!Contains(researchedTechs, need)) missing++;
            }
            return missing;
        }

        /// <summary>
        /// 前提充足率（0..1＝習得済み前提数 ÷ 前提総数）。1＝前提が全て揃った（解禁可能）。
        /// 前提なし（基礎技術）は1（最初から充足）。node 不正は0。
        /// </summary>
        public static float PrerequisitesMet(TechNode node, IList<string> researchedTechs)
        {
            if (node == null) return 0f;
            var pre = node.prerequisites;
            if (pre == null || pre.Count == 0) return 1f;
            int total = 0;
            int met = 0;
            for (int i = 0; i < pre.Count; i++)
            {
                string need = pre[i];
                if (string.IsNullOrEmpty(need)) continue;
                total++;
                if (Contains(researchedTechs, need)) met++;
            }
            if (total == 0) return 1f;
            return Mathf.Clamp01((float)met / total);
        }

        /// <summary>
        /// 今研究できる技術一覧＝前提が揃った最前線（未習得かつ全前提を習得済み）。
        /// <see cref="IsUnlockable"/> が true のノードを集める（重複IDは1回・出現順）。allNodes 不正は空。
        /// </summary>
        public static List<TechNode> AvailableTechs(IList<TechNode> allNodes, IList<string> researchedTechs)
        {
            var result = new List<TechNode>();
            if (allNodes == null) return result;
            var seen = new HashSet<string>();
            for (int i = 0; i < allNodes.Count; i++)
            {
                TechNode n = allNodes[i];
                if (n == null || string.IsNullOrEmpty(n.techId)) continue;
                if (!IsUnlockable(n, researchedTechs)) continue;
                if (!seen.Add(n.techId)) continue;
                result.Add(n);
            }
            return result;
        }

        /// <summary>
        /// 技術の深さ＝基礎（前提なし）から何段か（基礎技術＝深さ1、前提を1段持てば2…）。
        /// 前提の中で最も深いものに +1。循環依存・未定義の前提は深さ上限 <see cref="TechTreeParams.maxDepth"/> で打ち切る。
        /// allNodes に無い前提IDは基礎扱い（深さ0として寄与しない＝その前提分は段に数えない）。node 不正は0。
        /// </summary>
        public static int TechDepth(TechNode node, IList<TechNode> allNodes, TechTreeParams p)
        {
            if (node == null || string.IsNullOrEmpty(node.techId)) return 0;
            var index = BuildIndex(allNodes);
            return DepthOf(node, index, p.maxDepth);
        }

        public static int TechDepth(TechNode node, IList<TechNode> allNodes)
            => TechDepth(node, allNodes, TechTreeParams.Default);

        private static int DepthOf(TechNode node, Dictionary<string, TechNode> index, int budget)
        {
            if (node == null) return 0;
            var pre = node.prerequisites;
            if (pre == null || pre.Count == 0 || budget <= 1) return 1;
            int deepest = 0;
            for (int i = 0; i < pre.Count; i++)
            {
                string need = pre[i];
                if (string.IsNullOrEmpty(need)) continue;
                if (!index.TryGetValue(need, out TechNode pn) || pn == null) continue;
                int d = DepthOf(pn, index, budget - 1);
                if (d > deepest) deepest = d;
            }
            return 1 + deepest;
        }

        /// <summary>
        /// 研究フロンティア＝解禁直前の技術一覧（未習得・解禁可能で、かつ前提を1つ以上持つ＝基礎を超えた最前線）。
        /// <see cref="AvailableTechs"/> のうち基礎技術（前提なし）を除いたもの＝「次に手が届く応用技術」。
        /// 基礎技術しか研究できない初期局面では空になりうる。allNodes 不正は空。
        /// </summary>
        public static List<TechNode> ResearchableFrontier(IList<TechNode> allNodes, IList<string> researchedTechs)
        {
            var result = new List<TechNode>();
            if (allNodes == null) return result;
            var seen = new HashSet<string>();
            for (int i = 0; i < allNodes.Count; i++)
            {
                TechNode n = allNodes[i];
                if (n == null || string.IsNullOrEmpty(n.techId)) continue;
                if (n.IsRoot) continue; // 基礎技術はフロンティアでない
                if (!IsUnlockable(n, researchedTechs)) continue;
                if (!seen.Add(n.techId)) continue;
                result.Add(n);
            }
            return result;
        }

        /// <summary>
        /// 目標技術までの総コスト＝未習得の前提を辿って必要な研究コストを積算（目標への道のり）。
        /// 目標自身＋未習得の全先行前提のコストを合算する（習得済みの技術は0＝もう払わない）。
        /// 同じ前提を複数経路が要求しても1回だけ数える（技術は一度習得すれば全派生に効く）。
        /// 目標が既に習得済みなら0。循環依存は深さ上限で打ち切る。target・allNodes 不正は0。
        /// </summary>
        public static float TotalCostToReach(TechNode targetNode, IList<TechNode> allNodes, IList<string> researchedTechs, TechTreeParams p)
        {
            if (targetNode == null || string.IsNullOrEmpty(targetNode.techId)) return 0f;
            if (Contains(researchedTechs, targetNode.techId)) return 0f;
            var index = BuildIndex(allNodes);
            // 目標自身もまだ index に無い場合に備えて加える。
            if (!index.ContainsKey(targetNode.techId))
                index[targetNode.techId] = targetNode;
            var counted = new HashSet<string>();
            return AccumulateCost(targetNode, index, researchedTechs, counted, p.maxDepth);
        }

        public static float TotalCostToReach(TechNode targetNode, IList<TechNode> allNodes, IList<string> researchedTechs)
            => TotalCostToReach(targetNode, allNodes, researchedTechs, TechTreeParams.Default);

        private static float AccumulateCost(TechNode node, Dictionary<string, TechNode> index, IList<string> researched, HashSet<string> counted, int budget)
        {
            if (node == null || string.IsNullOrEmpty(node.techId)) return 0f;
            // 習得済みは払わない。
            if (Contains(researched, node.techId)) return 0f;
            // 既に積算済みは二重計上しない（合流する依存）。
            if (!counted.Add(node.techId)) return 0f;

            float total = Mathf.Max(0f, node.researchCost);
            var pre = node.prerequisites;
            if (pre != null && budget > 1)
            {
                for (int i = 0; i < pre.Count; i++)
                {
                    string need = pre[i];
                    if (string.IsNullOrEmpty(need)) continue;
                    if (Contains(researched, need)) continue;
                    if (!index.TryGetValue(need, out TechNode pn) || pn == null) continue;
                    total += AccumulateCost(pn, index, researched, counted, budget - 1);
                }
            }
            return total;
        }

        /// <summary>
        /// 飛び級の可否＝前提を飛ばして解禁できるか。技術は積み上げ＝<b>常に false</b>（前提が欠けたまま解禁できない）。
        /// 前提が全て揃っている場合は飛び級でなく正規の解禁なので、ここでも false（飛び級＝前提未充足での解禁を指す）。
        /// 「技術は積み上げ＝基礎を飛ばせない」を明示する窓口（呼び出し側が前提充足を確認する代わりに使える）。
        /// 前提なしの基礎技術も false（飛ばす前提が無い＝そもそも飛び級ではない）。
        /// </summary>
        public static bool Leapfrog(TechNode node, IList<string> researchedTechs)
        {
            // 技術は前提依存の積み上げ＝前提を飛ばして解禁することはできない。常に不可。
            return false;
        }

        // --- 内部ヘルパ ---

        /// <summary>IList に value（非null・非空想定）が含まれるか（順序リストの手書き走査・LINQ不可）。</summary>
        private static bool Contains(IList<string> list, string value)
        {
            if (list == null || string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] == value) return true;
            return false;
        }

        /// <summary>techId → TechNode の索引を作る（同一IDは先勝ち）。null 要素・空IDは無視。</summary>
        private static Dictionary<string, TechNode> BuildIndex(IList<TechNode> allNodes)
        {
            var index = new Dictionary<string, TechNode>();
            if (allNodes == null) return index;
            for (int i = 0; i < allNodes.Count; i++)
            {
                TechNode n = allNodes[i];
                if (n == null || string.IsNullOrEmpty(n.techId)) continue;
                if (!index.ContainsKey(n.techId)) index[n.techId] = n;
            }
            return index;
        }
    }
}
