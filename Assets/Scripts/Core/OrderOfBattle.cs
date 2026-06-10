using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 編制ツリーの台帳（#147・オーダー・オブ・バトル）。軍集団⊃軍団⊃艦隊(#146)の梯団ツリーを束ね、
    /// 「司令部固定・中身流動」（艦隊/下位梯団の attach/detach）・梯団別の司令配属（階級ゲート #14）・
    /// 配下集計を管理する唯一の窓口。艦隊そのものは #146 <see cref="FleetRoster"/> が持ち、ここは番号で参照する
    /// （別レジストリを作らず #146 を木構造へ拡張する方針）。会戦中の艦在庫 <see cref="FleetRegistry"/> とは別物。
    /// 司令配属の階級ゲートは #14 既定ラダー（中将7/大将8/元帥10）で判定する。②直轄投資・③任務戦術は後段。
    /// </summary>
    public static class OrderOfBattle
    {
        // 梯団別の必要階級 tier（#14 既定ラダー：中将7/大将8/元帥10）。マジックナンバー禁止＝const に集約。
        public const int FleetCommanderTier = 7;       // 艦隊司令＝中将
        public const int CorpsCommanderTier = 8;        // 軍団司令＝大将
        public const int ArmyGroupCommanderTier = 10;   // 軍集団司令＝元帥

        private static readonly Dictionary<int, MilitaryFormation> formations = new Dictionary<int, MilitaryFormation>();
        private static int nextId = 1;

        public static void Clear() { formations.Clear(); nextId = 1; }

        public static MilitaryFormation Get(int id) => formations.TryGetValue(id, out var f) ? f : null;

        public static IReadOnlyList<MilitaryFormation> AllFormations(Faction faction)
        {
            var list = new List<MilitaryFormation>();
            foreach (var f in formations.Values) if (f.faction == faction) list.Add(f);
            return list;
        }

        /// <summary>梯団ノードを新規作成する（id は自動採番）。</summary>
        public static MilitaryFormation Create(EchelonType echelon, Faction faction, string name = null)
        {
            var f = new MilitaryFormation { id = nextId++, echelon = echelon, faction = faction, name = name ?? "" };
            formations[f.id] = f;
            return f;
        }

        /// <summary>同勢力・同echelon・同名の梯団があれば返し、無ければ作る（シナリオからの編制構築用）。</summary>
        public static MilitaryFormation GetOrCreate(EchelonType echelon, Faction faction, string name)
        {
            if (!string.IsNullOrEmpty(name))
                foreach (var f in formations.Values)
                    if (f.faction == faction && f.echelon == echelon && f.name == name) return f;
            return Create(echelon, faction, name);
        }

        // ===== 司令部固定・中身流動（①：attach/detach） =====

        /// <summary>
        /// 艦隊(#146 番号)を梯団へ編入する。単一所属＝既に別梯団に居れば移す（中身流動）。
        /// **戦闘艦隊と非戦闘艦隊は混成しない(#883)**＝編入先の既存艦隊と運用区分が違えば拒否（移動もしない）。
        /// </summary>
        public static bool AttachFleet(int formationId, int fleetNumber)
        {
            var f = Get(formationId);
            if (f == null || fleetNumber <= 0) return false;
            if (!CanAttachFleet(formationId, fleetNumber)) return false; // 混成編成は不可
            // 同勢力の他梯団から外して単一所属を保つ
            foreach (var other in formations.Values)
                if (other.faction == f.faction) other.fleetNumbers.Remove(fleetNumber);
            if (!f.fleetNumbers.Contains(fleetNumber)) f.fleetNumbers.Add(fleetNumber);
            return true;
        }

        /// <summary>
        /// その艦隊を梯団へ編入できるか（#883 混成禁止の事前判定）。梯団が既に持つ艦隊と運用区分（戦闘/非戦闘）が
        /// 揃っていれば true。役割は <see cref="FleetRoster"/> から解決（未登録は戦闘艦扱い＝後方互換）。
        /// </summary>
        public static bool CanAttachFleet(int formationId, int fleetNumber)
        {
            var f = Get(formationId);
            if (f == null || fleetNumber <= 0) return false;
            ShipRole candidate = ResolveRole(f.faction, fleetNumber);
            for (int i = 0; i < f.fleetNumbers.Count; i++)
            {
                int num = f.fleetNumbers[i];
                if (num == fleetNumber) continue; // 既に居る自分自身は無視
                if (!ShipRoleRules.AreCompatible(candidate, ResolveRole(f.faction, num))) return false;
            }
            return true;
        }

        /// <summary>艦隊の運用区分を台帳から解決する（未登録は戦闘艦＝後方互換）。</summary>
        private static ShipRole ResolveRole(Faction faction, int fleetNumber)
        {
            FleetUnitData unit = FleetRoster.GetFleet(faction, fleetNumber);
            return unit != null ? unit.shipRole : ShipRole.戦闘艦;
        }

        public static bool DetachFleet(int formationId, int fleetNumber)
        {
            var f = Get(formationId);
            return f != null && f.fleetNumbers.Remove(fleetNumber);
        }

        /// <summary>下位梯団を上位梯団へ編入する（軍団→軍集団 等）。循環は作らない。単一親。</summary>
        public static bool AttachFormation(int parentId, int childId)
        {
            var parent = Get(parentId); var child = Get(childId);
            if (parent == null || child == null || parentId == childId) return false;
            if (parent.faction != child.faction) return false;
            if (WouldCycle(parentId, childId)) return false; // 子孫を親にしない
            // 既存の親から外す（単一親）
            foreach (var f in formations.Values) f.childFormationIds.Remove(childId);
            if (!parent.childFormationIds.Contains(childId)) parent.childFormationIds.Add(childId);
            child.parentId = parentId;
            return true;
        }

        public static bool DetachFormation(int parentId, int childId)
        {
            var parent = Get(parentId); var child = Get(childId);
            if (parent == null || !parent.childFormationIds.Remove(childId)) return false;
            if (child != null && child.parentId == parentId) child.parentId = 0;
            return true;
        }

        // ===== 司令配属（階級ゲート #14） =====

        /// <summary>梯団種別ごとの必要階級 tier（艦隊7/軍団8/軍集団10）。</summary>
        public static int RequiredTier(EchelonType echelon)
        {
            switch (echelon)
            {
                case EchelonType.軍集団: return ArmyGroupCommanderTier;
                case EchelonType.軍団: return CorpsCommanderTier;
                default: return FleetCommanderTier;
            }
        }

        /// <summary>その提督がこの梯団を指揮できる階級か（rankTier ≥ 必要tier）。</summary>
        public static bool CanCommand(AdmiralData admiral, EchelonType echelon)
            => admiral != null && admiral.rankTier >= RequiredTier(echelon);

        /// <summary>梯団へ司令を配属する。階級ゲートを満たさなければ false（現状維持）。</summary>
        public static bool AssignCommander(int formationId, AdmiralData admiral)
        {
            var f = Get(formationId);
            if (f == null || !CanCommand(admiral, f.echelon)) return false;
            f.commander = admiral;
            return true;
        }

        public static void UnassignCommander(int formationId)
        {
            var f = Get(formationId);
            if (f != null) f.commander = null;
        }

        // ===== 集計（梯団ツリーを辿る） =====

        /// <summary>その梯団の配下にある全艦隊(#146 番号)を再帰的に集める（下位梯団も含む）。</summary>
        public static IReadOnlyList<int> AllFleetNumbersUnder(int formationId)
        {
            var result = new List<int>();
            var visited = new HashSet<int>();
            Collect(formationId, result, visited);
            return result;
        }

        /// <summary>その梯団の配下にある艦隊数（再帰）。</summary>
        public static int CountFleetsUnder(int formationId) => AllFleetNumbersUnder(formationId).Count;

        private static void Collect(int formationId, List<int> acc, HashSet<int> visited)
        {
            if (!visited.Add(formationId)) return; // 循環防止
            var f = Get(formationId);
            if (f == null) return;
            foreach (int num in f.fleetNumbers) if (!acc.Contains(num)) acc.Add(num);
            foreach (int childId in f.childFormationIds) Collect(childId, acc, visited);
        }

        /// <summary>childId が parentId の祖先なら true（親子を逆に繋ぐと循環するので禁止）。</summary>
        private static bool WouldCycle(int parentId, int childId)
        {
            int cur = parentId;
            var guard = new HashSet<int>();
            while (cur != 0 && guard.Add(cur))
            {
                if (cur == childId) return true;
                var f = Get(cur);
                cur = f != null ? f.parentId : 0;
            }
            return false;
        }
    }
}
