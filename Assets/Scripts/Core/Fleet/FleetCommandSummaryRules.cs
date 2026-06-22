using System.Collections.Generic;

namespace Ginei
{
    /// <summary>艦隊管理サマリの1艦隊ぶんの入力（兵力・現役か・司令在席か・司令の指揮可能規模）。</summary>
    public readonly struct FleetCommandEntry
    {
        public readonly int strength;          // 艦隊の兵力（艦艇数）
        public readonly bool active;           // 現役か（解隊/欠番は false）
        public readonly bool hasCommander;     // 司令が在席か
        public readonly int commanderCapacity; // 司令の指揮可能規模（CommandCapacityRules.MaxStrengthForTier・空席は0）

        public FleetCommandEntry(int strength, bool active, bool hasCommander, int commanderCapacity)
        {
            this.strength = strength;
            this.active = active;
            this.hasCommander = hasCommander;
            this.commanderCapacity = commanderCapacity;
        }
    }

    /// <summary>艦隊管理サマリ（プレイヤーが「自分の艦隊」を一望するための集計値）。</summary>
    public readonly struct FleetCommandSummary
    {
        public readonly int totalFleets;     // 台帳の総艦隊数
        public readonly int activeFleets;    // うち現役
        public readonly int totalStrength;   // 現役艦隊の合計兵力
        public readonly int vacantCommands;  // 現役なのに司令が空席
        public readonly int overCapacity;    // 現役・司令在席だが兵力が指揮可能規模を超過（要昇進/再配置）
        public readonly float averageStrength; // 現役艦隊あたり平均兵力

        public FleetCommandSummary(int totalFleets, int activeFleets, int totalStrength,
                                   int vacantCommands, int overCapacity, float averageStrength)
        {
            this.totalFleets = totalFleets;
            this.activeFleets = activeFleets;
            this.totalStrength = totalStrength;
            this.vacantCommands = vacantCommands;
            this.overCapacity = overCapacity;
            this.averageStrength = averageStrength;
        }
    }

    /// <summary>
    /// プレイヤー艦隊管理の集計ロジック（純・決定論）。艦隊台帳（<see cref="FleetRoster"/>）から組み立てた
    /// <see cref="FleetCommandEntry"/> 群を集約し、過大指揮（司令の格に対し兵力過多）・空席指揮・総兵力などを返す
    /// 唯一の窓口。判定の数値（指揮可能規模）は <see cref="CommandCapacityRules"/> 由来の値を受け取るだけ＝二重実装しない。
    /// <see cref="PlayerFleetManagementOverlay"/> が描画に使う。test-first。
    /// </summary>
    public static class FleetCommandSummaryRules
    {
        /// <summary>兵力が司令の指揮可能規模を超えているか（規模0＝空席や未設定は超過扱いにしない）。</summary>
        public static bool IsOverCapacity(int strength, int commanderCapacity)
            => commanderCapacity > 0 && strength > commanderCapacity;

        /// <summary>艦隊群を集計してサマリを返す（null/空は全0）。</summary>
        public static FleetCommandSummary Summarize(IList<FleetCommandEntry> entries)
        {
            if (entries == null || entries.Count == 0)
                return new FleetCommandSummary(0, 0, 0, 0, 0, 0f);

            int total = 0, active = 0, strength = 0, vacant = 0, over = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                FleetCommandEntry e = entries[i];
                total++;
                if (!e.active) continue;
                active++;
                strength += e.strength;
                if (!e.hasCommander) vacant++;
                else if (IsOverCapacity(e.strength, e.commanderCapacity)) over++;
            }
            float avg = active > 0 ? (float)strength / active : 0f;
            return new FleetCommandSummary(total, active, strength, vacant, over, avg);
        }
    }
}
