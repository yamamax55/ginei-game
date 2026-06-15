using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の離散技術ツリー研究の進行＝薄いオーケストレータ（#123-127／#1065・唯一の窓口・test-first）。
    /// 解禁判定（前提依存グラフ）は <see cref="TechTreeRules"/>、産出量は <see cref="ResearchRules"/>、得意分野の偏りは
    /// <see cref="ResearchRules.IdeologyBias"/> に委譲し、ここは<b>合成（次の技術を選び・進め・完成させる）だけ</b>＝
    /// 数式を二重実装しない。研究フロンティアの中から政体（思想）の得意分野を優先し低コスト順に1つ選んで研究力で進め、
    /// 完成したら習得済みへ加える（前提が積み上がり次の技術が解禁される）。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour）。
    /// </summary>
    public static class TechProgressRules
    {
        /// <summary>
        /// 次に研究する技術を選ぶ＝解禁可能（前提充足・未習得＝<see cref="TechTreeRules.AvailableTechs"/>）の中から
        /// 政体の得意分野を優先（<see cref="ResearchRules.IdeologyBias"/> が高い）→低コスト→techId 辞書順で1つ。
        /// 解禁可能が無ければ null（研究し尽くした／ツリーが空）。決定論（同条件なら常に同じ選択）。
        /// </summary>
        public static TechNode SelectNextTech(IList<TechNode> nodes, FactionTechProgress progress, string ideology)
        {
            if (nodes == null || progress == null) return null;
            List<TechNode> avail = TechTreeRules.AvailableTechs(nodes, progress.researchedTechs);
            if (avail.Count == 0) return null;

            TechNode best = null;
            float bestBias = float.NegativeInfinity;
            float bestCost = float.PositiveInfinity;
            for (int i = 0; i < avail.Count; i++)
            {
                TechNode n = avail[i];
                if (n == null || string.IsNullOrEmpty(n.techId)) continue;
                float bias = ResearchRules.IdeologyBias(TechCatalog.FieldOf(n.techId), ideology);
                float cost = Mathf.Max(0f, n.researchCost);
                if (best == null
                    || bias > bestBias + 0.0001f
                    || (Mathf.Abs(bias - bestBias) <= 0.0001f && cost < bestCost - 0.0001f)
                    || (Mathf.Abs(bias - bestBias) <= 0.0001f && Mathf.Abs(cost - bestCost) <= 0.0001f
                        && string.CompareOrdinal(n.techId, best.techId) < 0))
                {
                    best = n; bestBias = bias; bestCost = cost;
                }
            }
            return best;
        }

        /// <summary>現在研究中のノード（未選択／不正は null）。</summary>
        public static TechNode CurrentNode(IList<TechNode> nodes, FactionTechProgress progress)
        {
            if (nodes == null || progress == null || string.IsNullOrEmpty(progress.currentTechId)) return null;
            for (int i = 0; i < nodes.Count; i++)
                if (nodes[i] != null && nodes[i].techId == progress.currentTechId) return nodes[i];
            return null;
        }

        /// <summary>
        /// 研究中の技術を output×dt ぶん進める。完成（進捗が researchCost 到達）したら習得済みへ加え、研究中をクリアし、
        /// 完成した技術IDを返す。未選択／未完成／不正な入力は null。負の output/dt はクランプ。
        /// </summary>
        public static string Advance(IList<TechNode> nodes, FactionTechProgress progress, float output, float deltaTime)
        {
            TechNode cur = CurrentNode(nodes, progress);
            if (cur == null) return null;
            if (deltaTime <= 0f) return null;

            float cost = Mathf.Max(0f, cur.researchCost);
            progress.currentProgress = Mathf.Min(cost, progress.currentProgress + Mathf.Max(0f, output) * deltaTime);

            if (progress.currentProgress >= cost)
            {
                if (!progress.IsResearched(cur.techId))
                    progress.researchedTechs.Add(cur.techId);
                string done = cur.techId;
                progress.currentTechId = null;
                progress.currentProgress = 0f;
                return done;
            }
            return null;
        }

        /// <summary>
        /// 1tick の進行＝研究中が無ければ選び（<see cref="SelectNextTech"/>）、研究力で進め（<see cref="Advance"/>）、
        /// 完成した技術IDを返す（無ければ null）。完成すると次の Tick で自動的に次の技術を選ぶ。
        /// 研究し尽くした（解禁可能が無い）勢力は何もしない＝null。
        /// </summary>
        public static string Tick(IList<TechNode> nodes, FactionTechProgress progress, float output, float deltaTime, string ideology)
        {
            if (nodes == null || progress == null) return null;

            TechNode cur = CurrentNode(nodes, progress);
            // 研究中が無い／既に習得済みになっている（外部要因）なら次を選ぶ。
            if (cur == null || progress.IsResearched(progress.currentTechId))
            {
                TechNode next = SelectNextTech(nodes, progress, ideology);
                if (next == null) return null;
                progress.currentTechId = next.techId;
                progress.currentProgress = 0f;
            }
            return Advance(nodes, progress, output, deltaTime);
        }
    }
}
