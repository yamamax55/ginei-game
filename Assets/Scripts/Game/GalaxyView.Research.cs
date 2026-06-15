using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ（<see cref="GalaxyView"/>）の<b>離散技術ツリー</b>配線（#123-127／#1065）。
    /// 既存のスカラ技術水準（<see cref="ResearchState.techLevel"/>＝建艦/戦力の質に効くマクロ）に対し、
    /// こちらは <see cref="TechCatalog"/> の依存ツリーを勢力ごとに辿る＝研究予算×平均労働技能の研究力で
    /// フロンティアの技術を1つずつ習得していく（前提を積み上げて応用が解禁される）。数式・解禁判定・選択は
    /// すべて Core（<see cref="TechProgressRules"/>/<see cref="TechTreeRules"/>/<see cref="ResearchRules"/>）へ委譲し、
    /// ここは年次に1tick回して完成を通知するだけ＝二重実装しない。観測は `ResearchObserverOverlay`（Alt+R）。
    /// </summary>
    public partial class GalaxyView
    {
        /// <summary>予算ゼロの勢力でもツリーが少しずつ進むよう確保する最小研究力（年あたり）。</summary>
        private const float MinAnnualResearchPower = 1f;

        /// <summary>勢力ごとの離散技術ツリー研究状態（在席・年次 Tick で進む。観測層が read-only で読む）。</summary>
        private readonly Dictionary<Faction, FactionTechProgress> techProgress
            = new Dictionary<Faction, FactionTechProgress>();

        /// <summary>勢力の離散技術ツリー研究状態（未生成は null）。観測層専用＝read-only。</summary>
        public FactionTechProgress GetTechProgress(Faction faction)
            => techProgress.TryGetValue(faction, out var p) ? p : null;

        /// <summary>
        /// 1勢力の離散技術ツリーを1年ぶん進める（年次 <see cref="RunStateConsumptionTick"/> から呼ぶ）。
        /// 研究力＝max(研究予算, 最小研究力)、効率＝平均労働技能（教育→技能→技術）。完成は通知へ。
        /// </summary>
        private void RunTechTreeTickFor(Faction fac, float researchFunding, float skill)
        {
            if (!techProgress.TryGetValue(fac, out var p) || p == null)
            {
                p = new FactionTechProgress();
                techProgress[fac] = p;
            }

            float power = Mathf.Max(MinAnnualResearchPower, researchFunding);
            float factor = 0.3f + 0.7f * Mathf.Clamp01(skill); // 技能ゼロでも止まらない基礎効率＋技能寄与
            float output = ResearchRules.ResearchOutput(power, factor);

            string done = TechProgressRules.Tick(TechCatalog.Nodes, p, output, 1f, IdeologyOf(fac));
            if (!string.IsNullOrEmpty(done))
                NotificationCenter.Push(NotificationCategory.建艦, NotificationSeverity.情報,
                    $"{fac} 技術『{TechCatalog.DisplayName(done)}』の研究完了");
        }
    }
}
