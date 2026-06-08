using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ上の艦隊（C-1 #34）。星系に停泊、または回廊上を「時間制ワープ」で移動する。
    /// 移動は回廊コスト length を warpSpeed で消化＝ワープに時間がかかる（援軍要素の前提）。
    /// 回廊以外への移動はできない（GalaxyMap で接続を判定）。純データ＋時間進行。
    /// </summary>
    public class StrategicFleet
    {
        public int id;
        public Faction faction;

        /// <summary>戦闘力（回廊戦闘の勝敗・消耗に使う抽象兵力）。</summary>
        public int strength = 100;

        /// <summary>ワープ速度（コスト/秒）。回廊 length をこの速度で消化する。</summary>
        public float warpSpeed = 1f;

        public int currentSystemId;       // 停泊中の星系（移動中は出発元）
        public int destinationSystemId;   // 移動中の目的地

        public bool IsMoving { get; private set; }

        private float corridorLength;
        private float traveled;
        private List<int> route;   // 多ホップ経路の残り（次の目的地より先の星系ID列）

        /// <summary>現在の回廊での進行度（0..1）。停泊中は1。</summary>
        public float Progress => corridorLength > 0f ? Mathf.Clamp01(traveled / corridorLength) : 1f;

        /// <summary>到着までの推定時間（秒）。停泊中・速度0は0。</summary>
        public float Eta => (IsMoving && warpSpeed > 0f) ? Mathf.Max(0f, (corridorLength - traveled) / warpSpeed) : 0f;

        /// <summary>多ホップ経路がまだ残っているか（途中星系を経由中）。</summary>
        public bool HasRoute => route != null && route.Count > 0;

        /// <summary>最終目的地の星系ID（経路があればその終点／移動中なら現在の目的地／停泊中は現在地）。</summary>
        public int FinalDestinationId =>
            (route != null && route.Count > 0) ? route[route.Count - 1]
            : (IsMoving ? destinationSystemId : currentSystemId);

        public StrategicFleet() { }

        public StrategicFleet(int id, int startSystemId, Faction faction = Faction.帝国, float warpSpeed = 1f)
        {
            this.id = id;
            this.currentSystemId = startSystemId;
            this.destinationSystemId = startSystemId;
            this.faction = faction;
            this.warpSpeed = warpSpeed;
        }

        /// <summary>
        /// 隣接星系 destId へワープを開始する。回廊が無い／移動中／同一星系なら失敗(false)。
        /// </summary>
        public bool BeginWarp(GalaxyMap map, int destId)
        {
            if (map == null || IsMoving || destId == currentSystemId) return false;
            Corridor c = map.GetCorridor(currentSystemId, destId);
            if (c == null) return false;                       // 回廊以外＝移動不可
            if (StrategyRules.IsFtlBlocked(map, c)) return false; // 前線回廊はFTL不可（回廊内戦闘＝C-3）
            destinationSystemId = destId;
            corridorLength = Mathf.Max(0.0001f, c.length);
            traveled = 0f;
            IsMoving = true;
            return true;
        }

        /// <summary>
        /// goalId まで最短経路（回廊 length 合計が最小）でワープを開始する。到達不能／同一星系なら false。
        /// 移動中でも受理し、その場合は現在のホップ（到達予定の星系まで）は維持したまま、到達予定星系から
        /// goalId への経路に引き直す＝「次の星系に着いてから新しい目的地へ向かう」。
        /// 経由星系は到着ごとに自動で次へ継続する（Tick(map,dt) を使うこと）。
        /// </summary>
        public bool WarpTo(GalaxyMap map, int goalId)
        {
            if (map == null) return false;

            // 移動中：現在のホップは維持し、到達予定星系から goalId への経路に引き直す（前線回避）。
            if (IsMoving)
            {
                List<int> p = GalaxyPathfinder.FindPath(map, destinationSystemId, goalId, avoidFtlBlocked: true);
                if (p.Count == 0) return false; // 到達不能（前線越しのみ等）
                route = (p.Count > 1) ? p.GetRange(1, p.Count - 1) : new List<int>();
                return true;
            }

            if (goalId == currentSystemId) return false;
            List<int> path = GalaxyPathfinder.FindPath(map, currentSystemId, goalId, avoidFtlBlocked: true);
            if (path == null || path.Count < 2) return false; // 到達不能（前線越しのみ等）

            int firstHop = path[1];
            route = (path.Count > 2) ? path.GetRange(2, path.Count - 2) : new List<int>();
            return BeginWarp(map, firstHop);
        }

        /// <summary>
        /// 銀河時間を deltaTime 進める（単一ホップ用・経路の自動継続なし・後方互換）。
        /// 回廊上を warpSpeed で前進し、到着したら true。
        /// </summary>
        public bool Tick(float deltaTime) => TickInternal(null, deltaTime);

        /// <summary>
        /// 銀河時間を deltaTime 進める（経路追従用）。到着時に残り経路があれば map を使って
        /// 次のホップへ自動継続する。各ホップ到着で true を返す。
        /// </summary>
        public bool Tick(GalaxyMap map, float deltaTime) => TickInternal(map, deltaTime);

        private bool TickInternal(GalaxyMap map, float deltaTime)
        {
            if (!IsMoving) return false;
            traveled += warpSpeed * deltaTime;
            if (traveled >= corridorLength)
            {
                currentSystemId = destinationSystemId;
                IsMoving = false;
                traveled = 0f;
                corridorLength = 0f;

                // 残り経路があれば次のホップへ自動継続（map が要る）
                if (map != null && route != null && route.Count > 0)
                {
                    int next = route[0];
                    route.RemoveAt(0);
                    BeginWarp(map, next);
                }
                return true; // このホップに到着
            }
            return false;
        }

        /// <summary>
        /// 別艦隊と同じ回廊上にいるか（両者とも移動中で、回廊エッジ{出発元,目的地}が一致）。
        /// 回廊での会戦トリガー判定に使う（StrategyRules.FindEncounters）。
        /// </summary>
        public bool IsOnSameCorridor(StrategicFleet other)
        {
            if (other == null || !IsMoving || !other.IsMoving) return false;
            int aMin = Mathf.Min(currentSystemId, destinationSystemId);
            int aMax = Mathf.Max(currentSystemId, destinationSystemId);
            int bMin = Mathf.Min(other.currentSystemId, other.destinationSystemId);
            int bMax = Mathf.Max(other.currentSystemId, other.destinationSystemId);
            return aMin == bMin && aMax == bMax;
        }
    }
}
