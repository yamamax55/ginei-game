using System.Collections.Generic;

namespace Ginei
{
    /// <summary>梯団の種別（#147）。序列は 艦隊 &lt; 軍団 &lt; 軍集団。艦隊は #146 FleetUnitData が葉。</summary>
    public enum EchelonType { 艦隊, 軍団, 軍集団 }

    /// <summary>
    /// 上位梯団（軍団・軍集団）のノード（#147）。艦隊(#146 FleetUnitData)を束ねる木構造の中間/上位ノード。
    /// 「司令部固定・中身流動」＝ノード（司令部）は残したまま配下の艦隊・下位梯団を attach/detach で付け替える。
    /// 木構造・配属・集計の管理は <see cref="OrderOfBattle"/>(static) が唯一の窓口。
    /// ★艦隊そのものは #146 <see cref="FleetUnitData"/>／<see cref="FleetRoster"/> が持つ（ここでは番号で参照）。
    /// 会戦中の艦在庫 <see cref="FleetRegistry"/>（ランタイム）とも別物。
    /// </summary>
    [System.Serializable]
    public class MilitaryFormation
    {
        public int id;
        public string name = "";
        public EchelonType echelon = EchelonType.軍団;
        public Faction faction = Faction.帝国;
        [System.NonSerialized] public AdmiralData commander; // 配属司令（null=空席）
        public int parentId;                                  // 0=最上位（親なし）
        public readonly List<int> childFormationIds = new List<int>(); // 下位梯団のid
        public readonly List<int> fleetNumbers = new List<int>();      // 直下の艦隊(#146 番号)

        public bool HasCommander => commander != null;
        public string DisplayName => !string.IsNullOrEmpty(name) ? name : $"{echelon}#{id}";
    }
}
