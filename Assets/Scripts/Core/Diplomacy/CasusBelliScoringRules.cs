using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 開戦理由（<see cref="CasusBelli"/>）の文脈スコアリング（DIPLO・#2119／#192 拡張）。基礎正当性
    /// （<see cref="WarGoalRules.GoalLegitimacy"/>）に、攻め手の膨張志向・標的の不安定・国力比・直近の裏切り等の
    /// 文脈ボーナスを足し、AI が「どの大義で攻めるか」を選べるようにする。基準式は <see cref="WarGoalRules"/> へ委譲。
    /// 文脈は float で受ける（FactionState 依存を避け純ロジック化）。決定論・test-first。
    /// </summary>
    public static class CasusBelliScoringRules
    {
        /// <summary>
        /// 大義 <paramref name="cb"/> のスコア（0..1）。<paramref name="opinion"/>=関係値(-100..100)、
        /// <paramref name="attackerExpansionism"/>=膨張志向(0..1・帝国型ほど高)、<paramref name="targetInstability"/>=標的の不安定(0..1)、
        /// <paramref name="powerRatio"/>=攻め手/標的の国力比、<paramref name="recentBetrayal"/>=直近の被裏切り(0..1)。
        /// </summary>
        public static float Score(CasusBelli cb, float opinion, float attackerExpansionism,
            float targetInstability, float powerRatio, float recentBetrayal, WarGoalRules.WarGoalParams p)
        {
            float s = WarGoalRules.GoalLegitimacy(cb, opinion, p);
            switch (cb)
            {
                case CasusBelli.征服: // 膨張志向が高いほど正当化しやすい・低いと国内圧力で減点
                    s += 0.2f * Mathf.Clamp01(attackerExpansionism) - 0.1f * (1f - Mathf.Clamp01(attackerExpansionism));
                    break;
                case CasusBelli.解放: // 標的が不安定（圧政）なほど解放の名分が立つ
                    s += 0.3f * Mathf.Clamp01(targetInstability);
                    break;
                case CasusBelli.従属: // 国力で圧倒できるほど属国化を維持できる
                    s += 0.15f * Mathf.Clamp01(powerRatio - 1f);
                    break;
                case CasusBelli.懲罰: // 直近に裏切られたほど懲罰の大義
                    s += 0.2f * Mathf.Clamp01(recentBetrayal);
                    break;
            }
            return Mathf.Clamp01(s);
        }

        /// <summary>既定パラメータ版。</summary>
        public static float Score(CasusBelli cb, float opinion, float attackerExpansionism,
            float targetInstability, float powerRatio, float recentBetrayal)
            => Score(cb, opinion, attackerExpansionism, targetInstability, powerRatio, recentBetrayal,
                WarGoalRules.WarGoalParams.Default);

        /// <summary>最も高スコアの大義を選ぶ（AI の開戦理由選択＝関係値だけでなく文脈で決める）。</summary>
        public static CasusBelli Best(float opinion, float attackerExpansionism, float targetInstability,
            float powerRatio, float recentBetrayal, WarGoalRules.WarGoalParams p)
        {
            CasusBelli best = CasusBelli.征服;
            float bestScore = -1f;
            foreach (CasusBelli cb in System.Enum.GetValues(typeof(CasusBelli)))
            {
                float sc = Score(cb, opinion, attackerExpansionism, targetInstability, powerRatio, recentBetrayal, p);
                if (sc > bestScore) { bestScore = sc; best = cb; }
            }
            return best;
        }

        /// <summary>既定パラメータ版。</summary>
        public static CasusBelli Best(float opinion, float attackerExpansionism, float targetInstability,
            float powerRatio, float recentBetrayal)
            => Best(opinion, attackerExpansionism, targetInstability, powerRatio, recentBetrayal,
                WarGoalRules.WarGoalParams.Default);
    }
}
