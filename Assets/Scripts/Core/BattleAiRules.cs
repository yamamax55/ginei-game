using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦AIの戦術判断（#2253）の純ロジック。フェーズ1の新システムをAIから使うための意思決定窓口。
    /// 陣形相性のカウンター選択・特殊指揮の発動判断・難易度（目利き）ゲートを担う。数値ロジックは
    /// `FormationMatchupRules`/`ActiveCommandRules` 等へ委譲し、ここは「どれを選ぶか」だけ。test-first。
    /// </summary>
    public static class BattleAiRules
    {
        /// <summary>敵陣形をカウンターする陣形（三すくみの逆引き）。守勢陣形（円陣/方陣）にはカウンター無し＝据え置き。</summary>
        public static Formation CounterFormation(Formation enemy)
        {
            if (enemy == Formation.横陣) return Formation.紡錘陣;   // 紡錘＞横陣
            if (enemy == Formation.鶴翼陣) return Formation.横陣;   // 横陣＞鶴翼
            if (enemy == Formation.紡錘陣) return Formation.鶴翼陣; // 鶴翼＞紡錘
            return enemy;
        }

        /// <summary>
        /// 状況から発動すべき特殊指揮を選ぶ。低士気→不退転、優勢→突撃、交戦中→一斉砲撃、いずれでもなければ false。
        /// advantage＝自/敵の戦力比。
        /// </summary>
        public static bool TryChooseCommand(bool engaged, float moraleRatio, float advantage, out ActiveCommand cmd)
        {
            if (moraleRatio < 0.4f) { cmd = ActiveCommand.不退転; return true; }   // 崩れそう＝踏みとどまる
            if (advantage >= 1.2f) { cmd = ActiveCommand.突撃; return true; }       // 優勢＝攻め込む
            if (engaged) { cmd = ActiveCommand.一斉砲撃; return true; }             // 交戦中＝火力集中
            cmd = ActiveCommand.一斉砲撃;
            return false;
        }

        /// <summary>難易度（目利き 0..1）ゲート。roll ≤ skill のとき高度な行動を取る（弱AIは取りこぼす）。</summary>
        public static bool ShouldAct(float skill, float roll) => roll <= Mathf.Clamp01(skill);
    }
}
