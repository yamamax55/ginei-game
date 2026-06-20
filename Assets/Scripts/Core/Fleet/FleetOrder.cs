namespace Ginei
{
    /// <summary>
    /// 軍自動編成（<see cref="ArmyOrganizationRules"/>）が産む<b>1艦隊ぶんの編成命令</b>（純データ）。
    /// 兵力・役割（<see cref="ShipRole"/>）・指揮班（司令/副司令/参謀の人物 id）・梯団段（<see cref="EchelonType"/>）・
    /// 所属梯団（<see cref="ArmyPlan.corps"/> への index）を持つ。id は -1＝空席（適任なし）が既定。
    /// 数値の出所は <see cref="CommandCapacityRules"/>／<see cref="FleetAutoOrganizeRules"/>（二重実装しない）。
    /// </summary>
    public sealed class FleetOrder
    {
        /// <summary>この艦隊の兵力（隻）。</summary>
        public int strength;
        /// <summary>艦隊の役割（既定＝戦闘艦＝従来動作）。</summary>
        public ShipRole role = ShipRole.戦闘艦;
        /// <summary>司令の人物 id（-1＝空席）。</summary>
        public int commanderId = -1;
        /// <summary>副司令の人物 id（-1＝空席）。</summary>
        public int viceId = -1;
        /// <summary>参謀の人物 id（-1＝空席）。</summary>
        public int chiefId = -1;
        /// <summary>この兵力に自然な梯団段（<see cref="CommandCapacityRules.EchelonForTier"/> 由来）。</summary>
        public EchelonType echelon;
        /// <summary>所属する梯団（<see cref="ArmyPlan.corps"/> の index・-1＝独立艦隊）。</summary>
        public int corpsIndex = -1;
    }
}
