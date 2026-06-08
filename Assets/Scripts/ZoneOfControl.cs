using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// ZOC（Zone of Control＝支配領域）の判定を集約する静的ヘルパー（#81）。
    /// 「敵艦隊の脇を素通りできない／防衛線を張れる」を成立させるための共通窓口。
    ///
    /// 設計方針（プロジェクト規約）：
    /// - 敵味方判定は必ず `FactionRelations.IsHostile` 経由（敵対の直書きをしない）。
    /// - 列挙は `FleetRegistry.AllFlagships`（陣営非依存の単一在庫）。
    /// - 調整値（半径倍率など）は各艦の `FleetMovement` に持たせ、ここはそれを読むだけ
    ///   （唯一の出所＝FleetMovement、ZOC独自の重複パラメータを増やさない）。
    /// - 移動本体は変更しない＝呼び出し側（FleetMovement の実効値／FleetAI の目標補正）で使う。
    /// </summary>
    public static class ZoneOfControl
    {
        /// <summary>
        /// 部隊がZOCを張れるか。生存（退却中でない）かつ敗走中でない戦闘可能な部隊のみ。
        /// 退却(IsRetreating＝IsAlive=false)・敗走(IsRouted)ではZOCが消え、戦線に穴が開く。
        /// </summary>
        public static bool CanProject(FleetStrength fs)
        {
            if (fs == null || !fs.IsAlive) return false;          // 退却中は IsAlive=false
            FleetMovement move = fs.GetComponent<FleetMovement>();
            if (move == null || !move.enableZoc) return false;     // ZOC無効艦は張らない
            FleetMorale morale = fs.GetComponent<FleetMorale>();
            if (morale != null && morale.IsRouted) return false;   // 敗走中はZOC消失
            return true;
        }

        /// <summary>
        /// 部隊のZOC半径＝部隊の外接円半径×`FleetMovement.zocRadiusScale`。
        /// ZOCを張れない部隊は 0。
        /// </summary>
        public static float GetRadius(FleetStrength fs)
        {
            if (!CanProject(fs)) return 0f;
            Squadron sq = fs.GetComponent<Squadron>();
            if (sq == null) return 0f;
            sq.GetBoundingCircle(out _, out float boundingRadius);
            FleetMovement move = fs.GetComponent<FleetMovement>();
            float scale = (move != null) ? move.zocRadiusScale : 1.5f;
            return boundingRadius * scale;
        }

        /// <summary>
        /// pos における「self に敵対する部隊のZOC」の最大侵入度を返す（0=圏外, 1=ZOC中心）。
        /// 複数の敵ZOCが重なる場合は最も深いものを採用。
        /// </summary>
        public static float HostileIntensityAt(FleetStrength self, Vector3 pos)
        {
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            float maxIntensity = 0f;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || fs == self) continue;
                if (!FactionRelations.IsHostile(self, fs)) continue;  // 敵対のみ
                float r = GetRadius(fs);
                if (r <= 0f) continue;
                float d = Vector2.Distance(pos, fs.transform.position);
                if (d < r)
                {
                    float intensity = 1f - d / r;
                    if (intensity > maxIntensity) maxIntensity = intensity;
                }
            }
            return maxIntensity;
        }

        /// <summary>
        /// pos→desired の進路上に self に敵対する部隊のZOCがあれば、横へ回り込むよう
        /// 目標方向を曲げた「ステアリング済みの目標座標」を返す（FleetAI の回避用）。
        /// BlackHole 回避（SteerAroundBlackHoles）と同方針：移動本体は変えず目標座標を補正する。
        /// ignore に交戦対象を渡すと、その部隊のZOCは避けない（＝意図して踏み込む）。
        /// </summary>
        public static Vector2 SteerAround(FleetStrength self, Vector2 pos, Vector2 desired,
            float avoidStrength, FleetStrength ignore)
        {
            Vector2 toTarget = desired - pos;
            float targetDist = toTarget.magnitude;
            if (targetDist < 0.001f) return desired;
            Vector2 dir = toTarget / targetDist;

            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            Vector2 steer = Vector2.zero;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || fs == self || fs == ignore) continue;
                if (!FactionRelations.IsHostile(self, fs)) continue;
                float danger = GetRadius(fs);
                if (danger <= 0f) continue;

                Vector2 center = fs.transform.position;
                float along = Vector2.Dot(center - pos, dir);
                if (along <= 0f || along > targetDist + danger) continue;   // 後方・遠すぎは無視

                Vector2 closest = pos + dir * Mathf.Clamp(along, 0f, targetDist);
                float perpDist = Vector2.Distance(closest, center);
                if (perpDist >= danger) continue;                            // 進路から十分離れている

                Vector2 perp = closest - center;
                if (perp.sqrMagnitude < 0.0001f) perp = new Vector2(-dir.y, dir.x); // 正面衝突構図
                perp.Normalize();

                float push = (danger - perpDist) / danger;                   // 近い/手前ほど強く曲げる
                steer += perp * (push * avoidStrength);
            }

            if (steer == Vector2.zero) return desired;
            Vector2 steeredDir = (dir + steer).normalized;
            return pos + steeredDir * targetDist;
        }
    }
}
