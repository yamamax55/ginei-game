using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 会戦の撤退・離脱の幾何（純ロジック・会戦改善）。敵と反対＝自勢力側の方向と、戦場外周（自勢力側の画面端）への到達判定。
    /// 敗走/撤退AIが「自勢力端を目指し、到達したら戦場から離脱」するのに使う。test-first。
    /// </summary>
    public static class BattleWithdrawalRules
    {
        /// <summary>敵と反対（自勢力側）への単位方向。重なり時は右向きにフォールバック。</summary>
        public static Vector2 AwayDirection(Vector2 self, Vector2 enemy)
        {
            Vector2 d = self - enemy;
            return d.sqrMagnitude < 1e-6f ? Vector2.right : d.normalized;
        }

        /// <summary>自勢力側へ distance だけ離れた逃走目標（敵不明＝enemy に原点を渡すと外周方向）。</summary>
        public static Vector2 WithdrawalTarget(Vector2 self, Vector2 enemy, float distance)
            => self + AwayDirection(self, enemy) * Mathf.Max(0f, distance);

        /// <summary>
        /// 自勢力側の戦場端（外周）に到達したか＝原点からの距離が fieldRadius 以上、かつ敵と反対側に出ている。
        /// （敵側へ突っ込んで外周に達した場合は離脱とみなさない）。
        /// </summary>
        public static bool IsAtWithdrawalEdge(Vector2 self, Vector2 enemy, float fieldRadius)
        {
            if (self.magnitude < Mathf.Max(0f, fieldRadius)) return false;
            return Vector2.Dot(self - enemy, self) >= 0f;
        }
    }
}
