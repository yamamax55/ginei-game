using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 戦略マップの銀河グラフ（C-1 #34）。星系(ノード)＋回廊(エッジ)。
    /// 「回廊で結ばれていなければ移動不可（航行不能宙域）」を判定する唯一の窓口。純データ。
    /// </summary>
    public class GalaxyMap
    {
        public List<StarSystem> systems = new List<StarSystem>();
        public List<Corridor> corridors = new List<Corridor>();

        public void AddSystem(StarSystem s) { if (s != null) systems.Add(s); }
        public void AddCorridor(Corridor c) { if (c != null) corridors.Add(c); }

        /// <summary>ID で星系を取得（無ければ null）。</summary>
        public StarSystem GetSystem(int id)
        {
            for (int i = 0; i < systems.Count; i++)
                if (systems[i] != null && systems[i].id == id) return systems[i];
            return null;
        }

        /// <summary>aId と bId を直接結ぶ回廊（向き不問・同一星系は無効）。無ければ null。</summary>
        public Corridor GetCorridor(int aId, int bId)
        {
            if (aId == bId) return null;
            for (int i = 0; i < corridors.Count; i++)
            {
                Corridor c = corridors[i];
                if (c != null && c.Connects(aId) && c.Connects(bId)) return c;
            }
            return null;
        }

        /// <summary>2星系が回廊で直接つながっているか（＝戦略マップ上で移動可能）。</summary>
        public bool AreConnected(int aId, int bId) => GetCorridor(aId, bId) != null;

        /// <summary>指定星系に回廊でつながる隣接星系IDの一覧。</summary>
        public List<int> Neighbors(int systemId)
        {
            List<int> result = new List<int>();
            for (int i = 0; i < corridors.Count; i++)
            {
                Corridor c = corridors[i];
                if (c == null || !c.Connects(systemId)) continue;
                int other = c.Other(systemId);
                if (other >= 0 && !result.Contains(other)) result.Add(other);
            }
            return result;
        }
    }
}
