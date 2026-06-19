using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 軍自動編成（<see cref="ArmyOrganizationRules"/>）が産む<b>梯団（軍団等）の編成命令</b>（純データ）。
    /// 複数の <see cref="FleetOrder"/>（その index 群）を束ね、上位指揮官（大将級以上＝<see cref="CommandCapacityRules.Tier軍団"/>）を戴く。
    /// 配下艦隊側は <see cref="FleetOrder.corpsIndex"/> でこの梯団を指す（双方向）。指揮官未充足の塊は梯団化されない。
    /// </summary>
    public sealed class CorpsOrder
    {
        /// <summary>梯団司令の人物 id（-1＝空席＝梯団は組まれない想定）。</summary>
        public int commanderId = -1;
        /// <summary>梯団の段（既定＝軍団）。</summary>
        public EchelonType echelon = EchelonType.軍団;
        /// <summary>配下の <see cref="ArmyPlan.fleets"/> への index 群。</summary>
        public readonly List<int> fleetIndices = new();
    }
}
