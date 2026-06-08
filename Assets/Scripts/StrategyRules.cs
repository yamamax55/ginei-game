using System.Collections.Generic;

namespace Ginei
{
    /// <summary>同一回廊で敵対艦隊が遭遇した組（会戦トリガー・C-3への布石）。</summary>
    public readonly struct FleetEncounter
    {
        public readonly StrategicFleet a;
        public readonly StrategicFleet b;
        public FleetEncounter(StrategicFleet a, StrategicFleet b) { this.a = a; this.b = b; }
    }

    /// <summary>
    /// 戦略マップの判定ルール（C-1 #34 仕上げ）。会戦トリガー（回廊での敵対遭遇）と
    /// 星系の占領（所有フリップ）の唯一の窓口。敵対判定は必ず FactionRelations 経由。純ロジック。
    /// </summary>
    public static class StrategyRules
    {
        /// <summary>
        /// レジストリ内で「同じ回廊上にいる敵対艦隊の組」をすべて返す（＝回廊会戦の発火条件）。
        /// 両者とも移動中・同一回廊・FactionRelations で敵対、を満たす組のみ。
        /// </summary>
        public static List<FleetEncounter> FindEncounters(StrategicFleetRegistry reg)
        {
            var result = new List<FleetEncounter>();
            if (reg == null) return result;

            var fleets = reg.fleets;
            for (int i = 0; i < fleets.Count; i++)
            {
                StrategicFleet a = fleets[i];
                if (a == null || !a.IsMoving) continue;
                for (int j = i + 1; j < fleets.Count; j++)
                {
                    StrategicFleet b = fleets[j];
                    if (b == null || !b.IsMoving) continue;
                    if (!a.IsOnSameCorridor(b)) continue;
                    if (!FactionRelations.IsHostile(null, a.faction, null, b.faction)) continue;
                    result.Add(new FleetEncounter(a, b));
                }
            }
            return result;
        }

        /// <summary>レジストリ内に回廊会戦の発火条件があるか。</summary>
        public static bool AnyEncounter(StrategicFleetRegistry reg) => FindEncounters(reg).Count > 0;

        /// <summary>
        /// 指定星系の占領を解決する。停泊中の艦隊が1勢力のみで、その勢力が現所有者と敵対していれば
        /// 所有権をその勢力へ移す。複数勢力が居る（＝戦闘案件）／無人／同一勢力なら何もしない。
        /// フリップしたら true。
        /// </summary>
        public static bool ResolveOccupation(GalaxyMap map, StrategicFleetRegistry reg, int systemId)
        {
            if (map == null || reg == null) return false;
            StarSystem sys = map.GetSystem(systemId);
            if (sys == null) return false;

            List<StrategicFleet> present = reg.FleetsAt(systemId);
            if (present.Count == 0) return false;

            // 在席勢力が1つに揃っているか（揃っていなければ占領未確定）
            Faction occupier = present[0].faction;
            for (int i = 1; i < present.Count; i++)
                if (present[i].faction != occupier) return false;

            // 現所有者と敵対していれば占領（同一勢力なら据え置き）
            if (FactionRelations.IsHostile(null, sys.owner, null, occupier))
            {
                sys.owner = occupier;
                return true;
            }
            return false;
        }

        /// <summary>全星系の占領を解決し、所有が変わった星系数を返す。</summary>
        public static int ResolveAllOccupations(GalaxyMap map, StrategicFleetRegistry reg)
        {
            if (map == null || reg == null) return 0;
            int flips = 0;
            for (int i = 0; i < map.systems.Count; i++)
            {
                StarSystem s = map.systems[i];
                if (s != null && ResolveOccupation(map, reg, s.id)) flips++;
            }
            return flips;
        }
    }
}
