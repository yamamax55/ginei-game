using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 勢力の<b>離散技術ツリーの研究状態</b>（純データ）。どの技術を習得済みか（<see cref="researchedTechs"/>）と、
    /// いま研究中の技術（<see cref="currentTechId"/>）とその進捗（<see cref="currentProgress"/>）を持つ。
    /// 集約スカラの技術水準（<see cref="ResearchState.techLevel"/>＝建艦/戦力の質へ効く既存マクロ）とは別＝
    /// こちらは<b>どの技術を解禁したか</b>を辿る木の状態。解決は <see cref="TechProgressRules"/> が唯一の窓口。
    /// 個体粒度・通貨経済へ降りない（タイクン回避）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class FactionTechProgress
    {
        /// <summary>習得済みの技術ID（解禁の前提を満たす集合）。</summary>
        public List<string> researchedTechs = new List<string>();

        /// <summary>現在研究中の技術ID（空＝未選択＝次の Tick で選ぶ）。</summary>
        public string currentTechId;

        /// <summary>現在研究中の技術の進捗（0..そのノードの researchCost）。</summary>
        public float currentProgress;

        /// <summary>習得済み技術の数。</summary>
        public int ResearchedCount => researchedTechs != null ? researchedTechs.Count : 0;

        /// <summary>その技術を習得済みか。</summary>
        public bool IsResearched(string techId)
        {
            if (researchedTechs == null || string.IsNullOrEmpty(techId)) return false;
            for (int i = 0; i < researchedTechs.Count; i++)
                if (researchedTechs[i] == techId) return true;
            return false;
        }
    }
}
