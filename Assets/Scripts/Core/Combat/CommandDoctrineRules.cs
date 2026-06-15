using UnityEngine;

namespace Ginei
{
    /// <summary>提督の性格類型（CDR-1 #2311）。AIの采配傾向を決める＝果敢/慎重/冷静/激情/堅実。</summary>
    public enum CommanderPersonality { 果敢, 慎重, 冷静, 激情, 堅実 }

    /// <summary>
    /// 性格→AI采配の純ロジック（CDR-1 #2311）。これまで深化させた能力（ADM-1〜6/特技/軍神）を<b>会戦AIに反映</b>する橋。
    /// 性格と功名心（`ambition`）から、交戦/撤退の積極性・特殊指揮(#2175)の選好・陣形(#5)の選好を返す。
    /// 数式は係数を返すだけで、実際の采配は `FleetAI`/`FormationDoctrineRules`/`ActiveCommandRules` が消費（配線は後段）。
    /// 実効値パターン・test-first。
    /// </summary>
    public static class CommandDoctrineRules
    {
        /// <summary>性格ごとの基準積極性（1.0=標準）。</summary>
        public static float BaseAggression(CommanderPersonality p)
        {
            switch (p)
            {
                case CommanderPersonality.果敢: return 1.3f;
                case CommanderPersonality.慎重: return 0.7f;
                case CommanderPersonality.激情: return 1.4f;
                case CommanderPersonality.堅実: return 0.85f;
                default: return 1.0f; // 冷静
            }
        }

        /// <summary>積極性＝性格基準×功名心補正（功名心50で等倍・100で+25%・0で-25%）。交戦を仕掛けやすさ。</summary>
        public static float AggressionFactor(CommanderPersonality p, int ambition)
            => BaseAggression(p) * (1f + (Mathf.Clamp(ambition, 0, 100) - 50f) / 200f);

        /// <summary>撤退閾値倍率（>1で早めに退く・&lt;1で粘る）。慎重/堅実は早く退き、果敢/激情は粘る。</summary>
        public static float RetreatThresholdFactor(CommanderPersonality p)
        {
            switch (p)
            {
                case CommanderPersonality.果敢: return 0.6f;
                case CommanderPersonality.慎重: return 1.3f;
                case CommanderPersonality.激情: return 0.5f;
                case CommanderPersonality.堅実: return 1.2f;
                default: return 1.0f; // 冷静
            }
        }

        /// <summary>性格が好む特殊指揮（#2175）。果敢/激情＝突撃、慎重/堅実＝不退転、冷静＝一斉砲撃。</summary>
        public static ActiveCommand PreferredCommand(CommanderPersonality p)
        {
            switch (p)
            {
                case CommanderPersonality.果敢:
                case CommanderPersonality.激情: return ActiveCommand.突撃;
                case CommanderPersonality.慎重:
                case CommanderPersonality.堅実: return ActiveCommand.不退転;
                default: return ActiveCommand.一斉砲撃; // 冷静
            }
        }

        /// <summary>性格が好む陣形（#5 FormationDoctrine の選好バイアス）。</summary>
        public static Formation FormationBias(CommanderPersonality p)
        {
            switch (p)
            {
                case CommanderPersonality.果敢: return Formation.紡錘陣; // 攻撃突破
                case CommanderPersonality.慎重: return Formation.円陣;   // 守勢
                case CommanderPersonality.激情: return Formation.横陣;   // 最大火力・脆い
                case CommanderPersonality.堅実: return Formation.方陣;   // 堅い
                default: return Formation.鶴翼陣; // 冷静＝包み込む
            }
        }
    }
}
