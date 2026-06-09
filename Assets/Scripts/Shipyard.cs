using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 建造オーダー（#884 BUILD-1/3）。何を（艦種 #80 ×役割 #128）・どれだけのコストで建造するか＋進捗。
    /// 完成すると <see cref="ShipyardRules.Commission"/> で艦隊（#146 `FleetUnitData`）になる（新造 or 損耗補充）。純データ。
    /// </summary>
    public class BuildOrder
    {
        public ShipClass shipClass = ShipClass.巡航艦; // 戦闘艦の艦種（#80）。非戦闘艦では建造規模の目安
        public ShipRole shipRole = ShipRole.戦闘艦;     // 運用区分（#128）
        public Faction faction;
        public string fleetName = "";

        public float cost;        // 必要建艦ポイント
        public float progress;    // 0..cost
        public int strengthYield; // 完成で艦隊へ加わる兵力（新造の baseStrength／補充の加算量）

        /// <summary>0より大きければ既存艦隊への損耗補充（その艦隊番号へ兵力加算）。0＝新造。</summary>
        public int reinforceFleetNumber;

        public BuildOrder() { }

        public BuildOrder(ShipClass shipClass, ShipRole shipRole, Faction faction, float cost, int strengthYield, string fleetName = "")
        {
            this.shipClass = shipClass;
            this.shipRole = shipRole;
            this.faction = faction;
            this.cost = Mathf.Max(0f, cost);
            this.strengthYield = Mathf.Max(0, strengthYield);
            this.fleetName = fleetName ?? "";
        }

        public bool IsComplete => progress >= cost;
        public float Remaining => Mathf.Max(0f, cost - progress);
    }

    /// <summary>
    /// 船渠（造船能力・#884 BUILD-1）。星系に紐づき、同時建造数（船渠）と建艦速度（ポイント/戦略秒）を持つ。
    /// 建造キュー（先頭から <see cref="parallelCapacity"/> 件を並行建造）。解決は <see cref="ShipyardRules"/>。純データ。
    /// </summary>
    public class Shipyard
    {
        public int systemId;
        public Faction faction;

        [Tooltip("同時に建造できる数（船渠数）")]
        public int parallelCapacity = 1;

        [Tooltip("建艦速度（ポイント/戦略秒。生産力係数 BUILD-2 を掛ける）")]
        public float buildPower = 10f;

        public List<BuildOrder> queue = new List<BuildOrder>();

        public Shipyard() { }

        public Shipyard(int systemId, Faction faction, int parallelCapacity = 1, float buildPower = 10f)
        {
            this.systemId = systemId;
            this.faction = faction;
            this.parallelCapacity = Mathf.Max(1, parallelCapacity);
            this.buildPower = Mathf.Max(0f, buildPower);
        }
    }
}
