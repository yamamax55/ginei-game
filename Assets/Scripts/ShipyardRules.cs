using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 造船・建艦の純ロジック（#884 BUILD-1〜4・唯一の窓口）。船渠に建造オーダーを積み（艦種#80×役割#128の
    /// コスト表）、生産力（BUILD-2＝<see cref="Province"/> の安定度比例産出）に応じて進め、完成艦を艦隊（#146 `FleetRoster`）へ
    /// <b>新造または損耗補充</b>で就役させる（BUILD-4）。タイクン化回避＝「造船能力＝器」「生産力＝速度」を係数で背景的に効かせる。
    /// 技術・人材（BUILD-5）や戦時動員（BUILD-6）は <see cref="Tick"/> に渡す productionFactor へ掛け込む（新モジュール不要）。test-first。
    /// </summary>
    public static class ShipyardRules
    {
        // --- 建造コスト/兵力（BUILD-3・マジックナンバー禁止＝const に集約） ---
        public const float CostBattleship = 100f; // 戦艦＝高コスト
        public const float CostCruiser = 60f;      // 巡航艦＝中庸
        public const float CostDestroyer = 35f;    // 駆逐艦＝安価
        public const float CostTransport = 25f;    // 輸送艦
        public const float CostScout = 20f;        // 偵察艦
        public const float CostColony = 45f;       // 入植艦

        public const int YieldBattleship = 60;
        public const int YieldCruiser = 40;
        public const int YieldDestroyer = 25;
        public const int YieldNonCombat = 20;

        /// <summary>艦種×役割の建造コスト（建艦ポイント）。戦闘艦は艦種で、非戦闘艦は役割で決まる（BUILD-3）。</summary>
        public static float Cost(ShipClass cls, ShipRole role)
        {
            if (role == ShipRole.戦闘艦)
            {
                switch (cls)
                {
                    case ShipClass.戦艦: return CostBattleship;
                    case ShipClass.駆逐艦: return CostDestroyer;
                    default: return CostCruiser;
                }
            }
            switch (role)
            {
                case ShipRole.輸送艦: return CostTransport;
                case ShipRole.偵察艦: return CostScout;
                case ShipRole.入植艦: return CostColony;
                default: return CostCruiser;
            }
        }

        /// <summary>完成で艦隊へ加わる兵力（新造の baseStrength／補充の加算量）。</summary>
        public static int StrengthYield(ShipClass cls, ShipRole role)
        {
            if (role != ShipRole.戦闘艦) return YieldNonCombat;
            switch (cls)
            {
                case ShipClass.戦艦: return YieldBattleship;
                case ShipClass.駆逐艦: return YieldDestroyer;
                default: return YieldCruiser;
            }
        }

        // --- 生産力連動（BUILD-2） ---

        /// <summary>生産力係数＝内政の産出倍率（安定度比例＝支配≠即建艦・#109）。Province 無しは1.0。</summary>
        public static float ProductionFactor(Province p)
            => p == null ? 1f : GovernanceRules.OutputFactor(p);

        // --- キュー操作（BUILD-1/3） ---

        /// <summary>艦種×役割の新規建造オーダーを積む（コスト/兵力はコスト表から）。積んだオーダーを返す。</summary>
        public static BuildOrder Enqueue(Shipyard yard, ShipClass cls, ShipRole role, string fleetName = null)
        {
            if (yard == null) return null;
            var order = new BuildOrder(cls, role, yard.faction, Cost(cls, role), StrengthYield(cls, role), fleetName);
            yard.queue.Add(order);
            return order;
        }

        /// <summary>既存艦隊への損耗補充オーダーを積む（完成でその艦隊の兵力を加算）。</summary>
        public static BuildOrder EnqueueReinforcement(Shipyard yard, int fleetNumber, ShipClass cls, ShipRole role)
        {
            if (yard == null || fleetNumber <= 0) return null;
            var order = new BuildOrder(cls, role, yard.faction, Cost(cls, role), StrengthYield(cls, role))
            { reinforceFleetNumber = fleetNumber };
            yard.queue.Add(order);
            return order;
        }

        /// <summary>並行建造中の件数（先頭から parallelCapacity 件）。</summary>
        public static int ActiveCount(Shipyard yard)
            => yard == null ? 0 : Mathf.Min(yard.parallelCapacity, yard.queue.Count);

        /// <summary>
        /// 建造を dt 進める。先頭から parallelCapacity 件を buildPower×productionFactor×dt で進め、
        /// 完成したオーダーをキューから外して返す（完成は呼び出し側が <see cref="Commission"/> で就役させる）。
        /// </summary>
        public static List<BuildOrder> Tick(Shipyard yard, float dt, float productionFactor)
        {
            var completed = new List<BuildOrder>();
            if (yard == null || dt <= 0f) return completed;

            float factor = Mathf.Max(0f, productionFactor);
            float add = yard.buildPower * factor * dt;

            int active = ActiveCount(yard);
            for (int i = 0; i < active; i++)
            {
                BuildOrder o = yard.queue[i];
                if (o == null) continue;
                o.progress = Mathf.Min(o.cost, o.progress + add);
            }

            for (int i = yard.queue.Count - 1; i >= 0; i--)
            {
                if (yard.queue[i] != null && yard.queue[i].IsComplete)
                {
                    completed.Add(yard.queue[i]);
                    yard.queue.RemoveAt(i);
                }
            }
            completed.Reverse(); // 先頭から完成順
            return completed;
        }

        // --- 就役（BUILD-4・#146 FleetRoster 連携） ---

        /// <summary>
        /// 完成オーダーを艦隊として就役させる（BUILD-4）。<see cref="BuildOrder.reinforceFleetNumber"/>&gt;0 なら既存艦隊の
        /// 兵力を補充、そうでなければ新造艦隊ユニットを <see cref="FleetRoster"/> へ登録（役割 #128 を付与）。未完成は null。
        /// </summary>
        public static FleetUnitData Commission(BuildOrder order)
        {
            if (order == null || !order.IsComplete) return null;

            if (order.reinforceFleetNumber > 0)
            {
                FleetUnitData unit = FleetRoster.GetFleet(order.faction, order.reinforceFleetNumber);
                if (unit == null) return null;
                unit.baseStrength += order.strengthYield; // 損耗補充
                return unit;
            }

            FleetUnitData created = FleetRoster.CreateFleet(order.faction, 0, order.fleetName);
            if (created == null) return null; // 永久欠番等
            created.shipRole = order.shipRole;
            created.baseStrength = order.strengthYield;
            return created;
        }
    }
}
