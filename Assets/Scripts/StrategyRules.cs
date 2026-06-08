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

    /// <summary>回廊戦闘の結果（C-3）。攻撃側勝利かと、勝者の残存兵力。</summary>
    public readonly struct CorridorBattleResult
    {
        public readonly bool attackerWon;
        public readonly int survivorStrength;
        public CorridorBattleResult(bool attackerWon, int survivorStrength)
        { this.attackerWon = attackerWon; this.survivorStrength = survivorStrength; }
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
        /// 回廊が前線（FTL不可）か。両端の星系を所有する勢力が敵対していれば true。
        /// 自勢力↔敵対勢力をつなぐ回廊はワープで通り抜けられず、回廊内で会戦になる（C-3 で起動予定）。
        /// 敵対判定は FactionRelations 経由（所有者の enum 比較＝後方互換）。
        /// </summary>
        public static bool IsFtlBlocked(GalaxyMap map, Corridor c)
        {
            if (map == null || c == null) return false;
            StarSystem a = map.GetSystem(c.aId);
            StarSystem b = map.GetSystem(c.bId);
            if (a == null || b == null) return false;
            return FactionRelations.IsHostile(null, a.owner, null, b.owner);
        }

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

        // ===== 回廊戦闘（C-3 #36）=====

        /// <summary>
        /// 兵力差で回廊戦闘を解決する（抽象版）。攻撃側は優勢（攻撃兵力＞防衛兵力）で勝利し、
        /// 勝者の残存兵力＝勝者総兵力−敗者総兵力。同数は防衛側が守り切る（攻撃側敗北）。
        /// ※将来ここを実際の戦術エンジン（BattleSetup）起動の結果に差し替える。
        /// </summary>
        public static CorridorBattleResult ResolveCorridorBattle(int attackerStrength, int defenderStrength)
        {
            bool aWon = attackerStrength > defenderStrength;
            int survivor = aWon ? attackerStrength - defenderStrength : defenderStrength - attackerStrength;
            return new CorridorBattleResult(aWon, survivor);
        }

        /// <summary>
        /// 前線回廊への侵攻＝回廊戦闘を起動・解決して戦略状態へ書き戻す（C-3 抽象版）。
        /// fromSystemId（攻撃側星系）と toSystemId（敵星系）が前線回廊でつながっていること、
        /// fromSystem に「toSystem 所有者と敵対する停泊艦隊」が居ることが前提。
        /// 攻撃側勝利：防衛艦を除去、toSystem を攻撃側勢力が占領、生存攻撃艦が toSystem へ前進。
        /// 防衛側勝利：攻撃艦を除去。兵力は ResolveCorridorBattle で按分し、残存0は相打ち除去。
        /// 起動条件を満たさなければ false。
        /// </summary>
        public static bool EngageFrontline(GalaxyMap map, StrategicFleetRegistry reg,
            int fromSystemId, int toSystemId, out CorridorBattleResult result)
        {
            result = new CorridorBattleResult(false, 0);
            if (map == null || reg == null) return false;

            Corridor c = map.GetCorridor(fromSystemId, toSystemId);
            if (c == null || !IsFtlBlocked(map, c)) return false; // 前線でなければ侵攻ではない
            StarSystem to = map.GetSystem(toSystemId);
            if (to == null) return false;

            var attackers = new List<StrategicFleet>();
            foreach (var f in reg.FleetsAt(fromSystemId))
                if (FactionRelations.IsHostile(null, f.faction, null, to.owner)) attackers.Add(f);
            if (attackers.Count == 0) return false; // 攻撃できる艦隊がいない

            Faction attackerFaction = attackers[0].faction;
            var defenders = reg.FleetsAt(toSystemId);

            int aStr = SumStrength(attackers);
            int dStr = SumStrength(defenders);
            result = ResolveCorridorBattle(aStr, dStr);

            List<StrategicFleet> winners = result.attackerWon ? attackers : defenders;
            List<StrategicFleet> losers = result.attackerWon ? defenders : attackers;
            int winnerTotal = result.attackerWon ? aStr : dStr;

            foreach (var l in losers) reg.Remove(l);
            ScaleStrength(winners, result.survivorStrength, winnerTotal);
            foreach (var wn in new List<StrategicFleet>(winners))
                if (wn.strength <= 0) reg.Remove(wn);

            if (result.attackerWon)
            {
                to.owner = attackerFaction;
                foreach (var a in attackers)
                    if (reg.GetFleet(a.id) != null) { a.currentSystemId = toSystemId; a.destinationSystemId = toSystemId; }
            }
            return true;
        }

        private static int SumStrength(List<StrategicFleet> fleets)
        {
            int s = 0;
            foreach (var f in fleets) if (f != null) s += f.strength;
            return s;
        }

        private static void ScaleStrength(List<StrategicFleet> fleets, int survivor, int total)
        {
            if (total <= 0) { foreach (var f in fleets) if (f != null) f.strength = 0; return; }
            foreach (var f in fleets)
                if (f != null) f.strength = (int)System.Math.Round((double)f.strength * survivor / total);
        }
    }
}
