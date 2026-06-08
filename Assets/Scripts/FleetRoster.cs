using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 艦隊編制の台帳（#146・オーダー・オブ・バトル）。勢力ごとに番号→<see cref="FleetUnitData"/> を解決し、
    /// 空き番号の払い出し・提督配属・解隊・永久欠番を管理する唯一の窓口。
    /// 番号体系は勢力ごとに独立（帝国の第1艦隊と同盟の第1艦隊は別物）。
    /// 配属の階級ゲートは #14（<see cref="AdmiralData.rankTier"/>）で判定する（tier 解決は RankSystem が出所）。
    /// ★会戦中の艦在庫 <see cref="FleetRegistry"/>（ランタイム）とは別物。流用・拡張しない。
    /// </summary>
    public static class FleetRoster
    {
        private static readonly Dictionary<Faction, Dictionary<int, FleetUnitData>> byFaction
            = new Dictionary<Faction, Dictionary<int, FleetUnitData>>();
        private static readonly Dictionary<Faction, HashSet<int>> retiredNumbers
            = new Dictionary<Faction, HashSet<int>>();

        /// <summary>台帳を空にする（会戦セットアップやテストの初期化用）。</summary>
        public static void Clear() { byFaction.Clear(); retiredNumbers.Clear(); }

        private static Dictionary<int, FleetUnitData> Units(Faction f)
        {
            if (!byFaction.TryGetValue(f, out var d)) { d = new Dictionary<int, FleetUnitData>(); byFaction[f] = d; }
            return d;
        }
        private static HashSet<int> Retired(Faction f)
        {
            if (!retiredNumbers.TryGetValue(f, out var s)) { s = new HashSet<int>(); retiredNumbers[f] = s; }
            return s;
        }

        public static FleetUnitData GetFleet(Faction f, int number)
            => Units(f).TryGetValue(number, out var u) ? u : null;

        public static IReadOnlyList<FleetUnitData> AllFleets(Faction f) => new List<FleetUnitData>(Units(f).Values);

        /// <summary>永久欠番か（以後その番号は払い出されない）。</summary>
        public static bool IsRetired(Faction f, int number) => Retired(f).Contains(number);

        /// <summary>払い出し可能な最小番号。使用中（現役）の番号と永久欠番を飛ばす。解隊済みは再利用可。</summary>
        public static int NextAvailableNumber(Faction f)
        {
            var units = Units(f); var ret = Retired(f);
            int n = 1;
            while (true)
            {
                bool occupied = units.TryGetValue(n, out var u) && u != null && u.status != FleetStatus.解隊;
                if (!occupied && !ret.Contains(n)) return n;
                n++;
            }
        }

        /// <summary>既存ユニットを台帳へ登録（番号で上書き）。永久欠番ユニットは欠番集合へも反映。</summary>
        public static FleetUnitData Register(FleetUnitData unit)
        {
            if (unit == null) return null;
            Units(unit.faction)[unit.fleetNumber] = unit;
            if (unit.status == FleetStatus.永久欠番) Retired(unit.faction).Add(unit.fleetNumber);
            return unit;
        }

        /// <summary>
        /// 新規艦隊ユニットを払い出す（number≤0 なら NextAvailableNumber）。永久欠番の番号は拒否（null）。
        /// 既に現役の同番号があればそれを返す（重複生成しない）。
        /// </summary>
        public static FleetUnitData CreateFleet(Faction f, int number = 0, string name = null)
        {
            if (number <= 0) number = NextAvailableNumber(f);
            if (Retired(f).Contains(number)) return null; // 永久欠番は払い出さない
            var existing = GetFleet(f, number);
            if (existing != null && existing.status == FleetStatus.現役) return existing;

            var u = ScriptableObject.CreateInstance<FleetUnitData>();
            u.fleetNumber = number; u.faction = f; u.fleetName = name ?? "";
            u.status = FleetStatus.現役;
            Units(f)[number] = u;
            return u;
        }

        // ===== 提督配属（階級ゲート #14） =====

        /// <summary>その提督を配属できるか（requiredTier 以上の階級が必要。0＝ゲート無し）。</summary>
        public static bool CanAssign(AdmiralData admiral, int requiredTier)
            => admiral != null && admiral.rankTier >= requiredTier;

        /// <summary>空席ユニットへ提督を配属する。階級ゲートを満たさなければ false（配属しない）。</summary>
        public static bool AssignAdmiral(FleetUnitData unit, AdmiralData admiral, int requiredTier = 0)
        {
            if (unit == null || !CanAssign(admiral, requiredTier)) return false;
            unit.assignedAdmiral = admiral;
            return true;
        }

        /// <summary>配属を解く（戦死・更迭で空席化）。</summary>
        public static void Unassign(FleetUnitData unit) { if (unit != null) unit.assignedAdmiral = null; }

        /// <summary>指揮官を入れ替える（上書き＝再配属。階級ゲートを満たさなければ false で現状維持）。</summary>
        public static bool ReassignAdmiral(FleetUnitData unit, AdmiralData admiral, int requiredTier = 0)
            => AssignAdmiral(unit, admiral, requiredTier);

        // ===== 解隊・永久欠番 =====

        /// <summary>解隊する（番号は再利用可＝status=解隊・指揮官は空席化）。</summary>
        public static bool Disband(Faction f, int number)
        {
            var u = GetFleet(f, number);
            if (u == null) return false;
            u.status = FleetStatus.解隊; u.assignedAdmiral = null;
            return true;
        }

        /// <summary>永久欠番にする（以後その番号を払い出さない＝status=永久欠番・指揮官は空席化）。</summary>
        public static bool RetireNumber(Faction f, int number)
        {
            var u = GetFleet(f, number);
            if (u != null) { u.status = FleetStatus.永久欠番; u.assignedAdmiral = null; }
            Retired(f).Add(number);
            return true;
        }
    }
}
