using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦局モメンタム（#2183）の純ロジック。両軍の戦力から「どちらが優勢か」を 0..1 で返す（読みやすさ・高揚の底上げ）。
    /// 0.5＝拮抗、基準勢力side が大きいほど1へ。戦力は呼び出し側が集計（生存戦闘旗艦の兵力等）。test-first。
    /// </summary>
    public static class BattleMomentumRules
    {
        /// <summary>基準勢力の優勢度（0..1）。両軍とも0なら拮抗0.5。</summary>
        public static float Advantage(float sidePower, float enemyPower)
        {
            float a = Mathf.Max(0f, sidePower);
            float b = Mathf.Max(0f, enemyPower);
            float sum = a + b;
            if (sum <= 0f) return 0.5f;
            return a / sum;
        }

        /// <summary>戦力指標＝生存数×weightCount＋兵力×weightStrength（モメンタム集計の標準式）。</summary>
        public static float Power(int aliveCount, float totalStrength, float weightCount = 1000f, float weightStrength = 1f)
            => Mathf.Max(0f, aliveCount) * Mathf.Max(0f, weightCount) + Mathf.Max(0f, totalStrength) * Mathf.Max(0f, weightStrength);
    }
}
