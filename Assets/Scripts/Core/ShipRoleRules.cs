using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 役割（運用区分）の互換判定の純ロジック（#883・唯一の窓口）。「戦闘艦と非戦闘艦を混成編成にしない」を
    /// 単一の規則で表す＝戦闘艦同士・非戦闘艦同士は同じ艦隊（梯団 #147）に編成できるが、戦闘×非戦闘の混成は不可。
    /// #80（戦闘艦の艦種：戦艦/巡航/駆逐）とは直交（あれは戦闘艦の中の種別）。判定は <see cref="OrderOfBattle"/> が読む。test-first。
    /// </summary>
    public static class ShipRoleRules
    {
        /// <summary>戦闘艦か（#128。非戦闘＝偵察/入植/輸送）。</summary>
        public static bool IsCombatant(ShipRole role) => role == ShipRole.戦闘艦;

        /// <summary>同じ運用区分か＝同じ艦隊に編成してよいか（戦闘同士 or 非戦闘同士のみ true。混成は false）。</summary>
        public static bool AreCompatible(ShipRole a, ShipRole b) => IsCombatant(a) == IsCombatant(b);

        /// <summary>役割の集合が同質か（戦闘と非戦闘が混在していなければ true。空・単一は true）。</summary>
        public static bool IsHomogeneous(IEnumerable<ShipRole> roles)
        {
            if (roles == null) return true;
            bool hasCombat = false, hasNonCombat = false;
            foreach (ShipRole r in roles)
            {
                if (IsCombatant(r)) hasCombat = true; else hasNonCombat = true;
                if (hasCombat && hasNonCombat) return false;
            }
            return true;
        }

        /// <summary>候補役割を既存集合に加えても同質を保てるか（混成にならないか）。空集合には何でも加えられる。</summary>
        public static bool CompatibleWithGroup(ShipRole candidate, IEnumerable<ShipRole> existing)
        {
            if (existing == null) return true;
            foreach (ShipRole r in existing)
                if (!AreCompatible(candidate, r)) return false;
            return true;
        }
    }
}
