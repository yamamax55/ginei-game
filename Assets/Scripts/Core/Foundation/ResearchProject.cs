using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 研究ツリー（#123-127）の純データ：1件の研究プロジェクト。
    /// 分野(<see cref="field"/>)ごとに必要研究量(<see cref="cost"/>)を抱え、研究力で進捗(<see cref="progress"/>)を積み、
    /// 完成で得られる効果量(<see cref="strengthYield"/>＝兵力/性能ボーナス)を持つ。
    /// 数値の解決は <see cref="ResearchRules"/>(static) が唯一の窓口（建設マイクロ・通貨経済は持たない＝タイクン回避）。
    /// </summary>
    [System.Serializable]
    public class ResearchProject
    {
        /// <summary>研究分野（軍事/生産/情報/社会）。政体で研究効率が偏る。</summary>
        public ResearchField field;

        /// <summary>完成に必要な研究量（研究ポイント）。</summary>
        public float cost;

        /// <summary>現在の進捗（0..cost）。<see cref="ResearchRules.Tick"/> で積む。</summary>
        public float progress;

        /// <summary>完成で得られる効果量（兵力/性能ボーナス）。</summary>
        public int strengthYield;

        public ResearchProject() { }

        public ResearchProject(ResearchField field, float cost, int strengthYield)
        {
            this.field = field;
            this.cost = Mathf.Max(0f, cost);
            this.progress = 0f;
            this.strengthYield = strengthYield;
        }
    }
}
