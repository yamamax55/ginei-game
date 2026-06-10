using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の艦隊プール（総艦艇数）を各艦隊の <see cref="FleetUnitData.baseStrength"/> へ配分する純ロジック・唯一の窓口（#148 編成）。
    /// 現役艦隊の baseStrength 合計が**総プールを超えない**よう制約する。供給（プール増）は将来 #884 造船。
    /// 在庫は <see cref="FleetRoster"/> を読む（並行レジストリを作らない）。総プール値は呼び出し側（Game/セーブ）が保持し引数で渡す。test-first。
    /// </summary>
    public static class FleetPoolRules
    {
        /// <summary>現役艦隊に割り当て済みの艦艇数合計（baseStrength は非負として集計）。</summary>
        public static int Allocated(Faction f)
        {
            int sum = 0;
            foreach (var u in FleetRoster.AllFleets(f))
                if (u != null && u.IsActive && u.baseStrength > 0) sum += u.baseStrength;
            return sum;
        }

        /// <summary>プール残（総数 − 割当済み）。下限0（過剰割当でも負は返さない）。</summary>
        public static int Available(Faction f, int totalPool) => Mathf.Max(0, totalPool - Allocated(f));

        /// <summary>この艦隊を <paramref name="newStrength"/>（≥0）にしても、他の現役艦隊との合計が総プールを超えないか。</summary>
        public static bool CanAllocate(FleetUnitData unit, int newStrength, int totalPool)
        {
            if (unit == null || newStrength < 0) return false;
            int others = OthersAllocated(unit);
            return others + newStrength <= totalPool;
        }

        /// <summary>割り当てを設定する（プール超過は false で現状維持）。</summary>
        public static bool SetAllocation(FleetUnitData unit, int newStrength, int totalPool)
        {
            if (!CanAllocate(unit, newStrength, totalPool)) return false;
            unit.baseStrength = newStrength;
            return true;
        }

        /// <summary>艦艇数を増減する（負方向は0でクランプ）。プール超過は false で現状維持。</summary>
        public static bool Adjust(FleetUnitData unit, int delta, int totalPool)
        {
            if (unit == null) return false;
            int target = Mathf.Max(0, unit.baseStrength + delta);
            return SetAllocation(unit, target, totalPool);
        }

        // ===== FleetPool ストアを総プールとして使う版（#884 造船供給と接続） =====

        /// <summary>勢力プール残（<see cref="FleetPool"/> の総数 − 割当済み）。</summary>
        public static int Available(Faction f) => Available(f, FleetPool.Get(f));

        /// <summary>この艦隊を newStrength にしても <see cref="FleetPool"/> の総プールを超えないか。</summary>
        public static bool CanAllocate(FleetUnitData unit, int newStrength)
            => unit != null && CanAllocate(unit, newStrength, FleetPool.Get(unit.faction));

        /// <summary>割り当てを設定する（<see cref="FleetPool"/> 総プール基準）。</summary>
        public static bool SetAllocation(FleetUnitData unit, int newStrength)
            => unit != null && SetAllocation(unit, newStrength, FleetPool.Get(unit.faction));

        /// <summary>艦艇数を増減する（<see cref="FleetPool"/> 総プール基準・負は0クランプ）。</summary>
        public static bool Adjust(FleetUnitData unit, int delta)
            => unit != null && Adjust(unit, delta, FleetPool.Get(unit.faction));

        /// <summary>損耗で勢力の総プールを減らす（<paramref name="lost"/>≥0 を差し引く・0下限）。新しい総数を返す（#884 損耗）。</summary>
        public static int ApplyAttrition(Faction f, int lost) => FleetPool.Add(f, -Mathf.Max(0, lost));

        /// <summary>指定艦隊を除く、同勢力の現役艦隊の割当合計。</summary>
        private static int OthersAllocated(FleetUnitData unit)
        {
            int sum = 0;
            foreach (var u in FleetRoster.AllFleets(unit.faction))
                if (u != null && u != unit && u.IsActive && u.baseStrength > 0) sum += u.baseStrength;
            return sum;
        }
    }
}
