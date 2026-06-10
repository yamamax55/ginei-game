using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦略マップ上の艦隊群と銀河の状態をまとめて管理する（C-1 #34）。
    /// 銀河時間の一括進行（全艦隊 Tick）と、星系ごとの在席照会を提供する。純ロジック。
    /// （将来 MonoBehaviour 側はこれを保持して毎フレーム Tick する想定。）
    /// </summary>
    public class StrategicFleetRegistry
    {
        public GalaxyMap map;
        public List<StrategicFleet> fleets = new List<StrategicFleet>();

        public StrategicFleetRegistry() { }
        public StrategicFleetRegistry(GalaxyMap map) { this.map = map; }

        public void Add(StrategicFleet f) { if (f != null && !fleets.Contains(f)) fleets.Add(f); }
        public void Remove(StrategicFleet f) { fleets.Remove(f); }

        public StrategicFleet GetFleet(int id)
        {
            for (int i = 0; i < fleets.Count; i++)
                if (fleets[i] != null && fleets[i].id == id) return fleets[i];
            return null;
        }

        /// <summary>
        /// 全艦隊の銀河時間を deltaTime 進める（経路追従＝多ホップ自動継続）。
        /// このフレームに最終目的地へ到着した艦隊の数を返す。
        /// </summary>
        public int Tick(float deltaTime)
        {
            int arrivedFinal = 0;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet f = fleets[i];
                if (f == null) continue;
                bool hopArrived = f.Tick(map, deltaTime);
                if (hopArrived && !f.IsMoving && !f.HasRoute) arrivedFinal++; // 最終到着
            }
            return arrivedFinal;
        }

        /// <summary>指定星系に停泊中（移動していない）の艦隊を列挙する。</summary>
        public List<StrategicFleet> FleetsAt(int systemId)
        {
            var result = new List<StrategicFleet>();
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet f = fleets[i];
                if (f != null && !f.IsOnCorridor && f.currentSystemId == systemId) result.Add(f);
            }
            return result;
        }
    }
}
