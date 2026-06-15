using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の研究プログラム状態（純データ・薄いオーケストレータ <see cref="ResearchProgramRules"/> の器）。
    /// 個別の <see cref="ResearchProject"/>（1技術を時間で研究する単位）とは別＝こちらは勢力レベルの
    /// <b>集約された技術水準</b>（techLevel）と現在の研究進捗（progress）を持つマクロ状態。
    /// 個体粒度・通貨経済へ降りない（タイクン回避・スケーラビリティ規律＝集約に留める）。
    /// 数値の解決は <see cref="ResearchProgramRules"/>(static) が唯一の窓口（基準値非破壊・委譲先は既存の純ロジック）。
    /// </summary>
    [System.Serializable]
    public class ResearchState
    {
        /// <summary>勢力の集約技術水準（0..上限なし＝段が上がるほど技術が進む）。閾値ごとに +1 する離散段。</summary>
        public float techLevel;

        /// <summary>現在の段（次の techLevel へ向けた研究進捗・0..levelCost）。閾値で techLevel↑・progress 繰り越し。</summary>
        public float progress;

        /// <summary>次の段へ上がるのに要する研究量（1段あたりのコスト・負/0は既定でクランプ）。</summary>
        public float levelCost;

        public ResearchState() { levelCost = 1f; }

        public ResearchState(float techLevel, float levelCost)
        {
            this.techLevel = Mathf.Max(0f, techLevel);
            this.progress = 0f;
            this.levelCost = Mathf.Max(0f, levelCost);
        }
    }
}
