using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 一騎討ち・宿敵の純ロジック（CDR-6 #2316）。ネームド旗艦どうしが至近で対峙したときに起こりうる決着＝
    /// 武勇（攻撃/統率）＋武名（fame#2304）で勝敗が決まり、士気・武名が大きく揺れる。宿敵（低相性 ADM-4 の敵対ペア）は
    /// 発生率・効果が増す。決定論（roll を注入）・test-first。
    /// </summary>
    public static class DuelRules
    {
        /// <summary>これ以下の相性は宿敵（戦場で互いを狙う）。</summary>
        public const float NemesisThreshold = 0.2f;
        /// <summary>一騎討ちの勝敗が動かす士気の振れ幅。</summary>
        public const float DuelMoraleSwing = 0.2f;

        /// <summary>宿敵か（相性が極めて低い敵対ペア）。`AffinityRules.Affinity` の結果を渡す。</summary>
        public static bool IsNemesis(float affinity) => affinity <= NemesisThreshold;

        /// <summary>
        /// 一騎討ちの発生確率。果敢/激情な性格・高い功名心・宿敵関係で上がる。
        /// </summary>
        public static float DuelChance(CommanderPersonality personality, int ambition, bool isNemesis)
        {
            float c = 0.1f;
            if (personality == CommanderPersonality.果敢 || personality == CommanderPersonality.激情) c += 0.2f;
            c += Mathf.Max(0, ambition - 50) / 200f;
            if (isNemesis) c += 0.3f;
            return Mathf.Clamp01(c);
        }

        /// <summary>一騎討ちの強さ（武勇＝攻撃/統率の合算＋武名の重み）。</summary>
        public static float DuelStrength(int martial, int fame)
            => Mathf.Max(0, martial) + Mathf.Max(0, fame) * 0.3f;

        /// <summary>A が勝つ確率（双方の強さ比）。</summary>
        public static float WinProbability(int martialA, int fameA, int martialB, int fameB)
        {
            float a = DuelStrength(martialA, fameA);
            float b = DuelStrength(martialB, fameB);
            float sum = a + b;
            return sum <= 0f ? 0.5f : a / sum;
        }

        /// <summary>一騎討ちの決着（roll∈[0,1) を注入・true=A勝利）。</summary>
        public static bool ResolveDuel(int martialA, int fameA, int martialB, int fameB, float roll)
            => roll < WinProbability(martialA, fameA, martialB, fameB);
    }
}
