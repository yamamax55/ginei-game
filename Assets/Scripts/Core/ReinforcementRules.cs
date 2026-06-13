using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 増援の時間差投入（#2182）の純ロジック。指定の到着遅延（game-time秒）に達したら戦場端から参戦させる。
    /// 戦略の「逐次投入 vs 集中」を会戦でも体感させる。到着判定と出現端の座標だけを担う（スポーンは Game 側）。test-first。
    /// </summary>
    public static class ReinforcementRules
    {
        /// <summary>到着済みか（経過 game-time が遅延以上）。</summary>
        public static bool IsDue(float arrivalDelay, float elapsed)
            => elapsed >= Mathf.Max(0f, arrivalDelay);

        /// <summary>戦場端の出現座標（自陣側の端＝帝国は左／同盟・その他は右。baseY は配置の高さ）。</summary>
        public static Vector2 EdgePosition(Faction faction, float baseY, float radius)
        {
            float r = Mathf.Abs(radius);
            float x = (faction == Faction.帝国) ? -r : r;
            return new Vector2(x, baseY);
        }
    }
}
