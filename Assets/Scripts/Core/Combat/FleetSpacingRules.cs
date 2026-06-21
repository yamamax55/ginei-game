using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 味方艦隊どうしの間隔保持（純ロジック・test-first）。MonoBehaviour 非依存の幾何のみ。
    /// ・<see cref="SeparationPush"/>＝重なり回避（近すぎる味方から離れる押し出しベクトル）。
    /// ・<see cref="LimitOvertake"/>＝前方の味方を追い越さない（移動先の前方成分を手前でクランプ）。
    /// 数値は呼び出し側（<see cref="FleetMovement"/>/<see cref="FleetAI"/>）が占有半径・前進方向を与える。
    /// </summary>
    public static class FleetSpacingRules
    {
        /// <summary>近傍の味方1隊ぶんの位置と占有半径。</summary>
        public readonly struct Neighbor
        {
            public readonly Vector2 pos;
            public readonly float radius;
            public Neighbor(Vector2 pos, float radius) { this.pos = pos; this.radius = radius; }
        }

        /// <summary>
        /// 味方との重なりを解消する押し出し（速度的）ベクトルを返す＝self を味方から離す方向。
        /// 各味方について「最小間隔(=半径和+margin) − 実距離」が正なら、その重なり量×strength を離れる方向へ合成。
        /// 完全重複（距離≈0）は selfId 由来の決定論方向で散らす（左右対称の膠着を防ぐ）。重なりが無ければゼロ。
        /// </summary>
        public static Vector2 SeparationPush(Vector2 self, float selfRadius, IReadOnlyList<Neighbor> friends,
            float margin, float strength, int selfId = 0)
        {
            Vector2 push = Vector2.zero;
            if (friends == null) return push;
            for (int i = 0; i < friends.Count; i++)
            {
                Vector2 d = self - friends[i].pos;
                float minSep = selfRadius + friends[i].radius + margin;
                float dist = d.magnitude;
                if (dist >= minSep) continue; // 触れていない
                Vector2 dir;
                if (dist > 1e-4f) dir = d / dist;
                else
                {
                    float a = selfId * 12.9898f; // 決定論的な散らし方向
                    dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                }
                push += dir * ((minSep - dist) * Mathf.Max(0f, strength));
            }
            return push;
        }

        /// <summary>
        /// 前方の味方を追い越さないよう移動先 <paramref name="dest"/> をクランプする。
        /// <paramref name="forwardDir"/>（前進方向）の成分について、前方にいる最寄りの味方の位置から
        /// <paramref name="keepBehind"/> だけ手前を上限とし、それを超える前進ぶんだけ引き戻す。
        /// 横方向成分は保持（回り込み・展開は妨げない）。前方に味方が無ければ dest をそのまま返す。
        /// </summary>
        public static Vector2 LimitOvertake(Vector2 self, Vector2 dest, Vector2 forwardDir,
            IReadOnlyList<Vector2> friends, float keepBehind)
        {
            if (friends == null || forwardDir.sqrMagnitude < 1e-6f) return dest;
            Vector2 fwd = forwardDir.normalized;
            float selfFwd = Vector2.Dot(self, fwd);

            float limit = float.MaxValue;
            for (int i = 0; i < friends.Count; i++)
            {
                float f = Vector2.Dot(friends[i], fwd);
                if (f <= selfFwd) continue;       // 自分より後ろ・同列は対象外
                float cap = f - keepBehind;        // その味方の手前で止まる
                if (cap < limit) limit = cap;
            }
            if (limit == float.MaxValue) return dest; // 前方に味方なし

            float destFwd = Vector2.Dot(dest, fwd);
            if (destFwd <= limit) return dest;        // 既に上限内
            return dest + fwd * (limit - destFwd);    // 前方成分のみクランプ（横は保持）
        }
    }
}
