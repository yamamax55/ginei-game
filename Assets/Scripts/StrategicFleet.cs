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

        /// <summary>ワープ速度（コスト/秒）。回廊 length をこの速度で消化する。</summary>
        public float warpSpeed = 1f;

        public int currentSystemId;       // 停泊中の星系（移動中は出発元）
        public int destinationSystemId;   // 移動中の目的地

        public bool IsMoving { get; private set; }

        private float corridorLength;
        private float traveled;

        /// <summary>現在の回廊での進行度（0..1）。停泊中は1。</summary>
        public float Progress => corridorLength > 0f ? Mathf.Clamp01(traveled / corridorLength) : 1f;

        /// <summary>到着までの推定時間（秒）。停泊中・速度0は0。</summary>
        public float Eta => (IsMoving && warpSpeed > 0f) ? Mathf.Max(0f, (corridorLength - traveled) / warpSpeed) : 0f;

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
            if (c == null) return false;       // 回廊以外＝移動不可
            destinationSystemId = destId;
            corridorLength = Mathf.Max(0.0001f, c.length);
            traveled = 0f;
            IsMoving = true;
            return true;
        }

        /// <summary>
        /// 銀河時間を deltaTime 進める。回廊上を warpSpeed で前進し、到着したら true を返す。
        /// </summary>
        public bool Tick(float deltaTime)
        {
            if (!IsMoving) return false;
            traveled += warpSpeed * deltaTime;
            if (traveled >= corridorLength)
            {
                currentSystemId = destinationSystemId;
                IsMoving = false;
                traveled = 0f;
                corridorLength = 0f;
                return true;
            }
            return false;
        }
    }
}
