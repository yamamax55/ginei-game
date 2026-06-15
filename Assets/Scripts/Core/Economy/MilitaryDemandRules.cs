using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 部隊の総需要算出（MILSUP-2・#2049・純ロジック）。
    /// 艦隊兵力（<see cref="StrategicFleet.strength"/>/<see cref="FleetStrength"/>）×活動状態×原単位（MILSUP-1）＝部隊のカテゴリ別需要。
    /// 梯団#147 配下の艦隊需要を合算（集約）。砲弾1発単位に降りない。test-first。
    /// </summary>
    public static class MilitaryDemandRules
    {
        /// <summary>戦略艦隊の活動状態＝交戦中→交戦／移動中→移動／それ以外→待機。</summary>
        public static MilitaryActivity ActivityOf(StrategicFleet f)
        {
            if (f == null) return MilitaryActivity.待機;
            if (f.engaged) return MilitaryActivity.交戦;
            if (f.IsMoving) return MilitaryActivity.移動;
            return MilitaryActivity.待機;
        }

        /// <summary>1艦隊のカテゴリ別需要（活動状態を自動判定）。</summary>
        public static float FleetDemand(StrategicFleet f, ResourceType type)
            => f == null ? 0f : MilitarySupplyRules.Upkeep(f.strength, type, ActivityOf(f));

        /// <summary>艦隊群（梯団#147）のカテゴリ別総需要を合算（集約）。</summary>
        public static float AggregateDemand(IReadOnlyList<StrategicFleet> fleets, ResourceType type)
        {
            if (fleets == null) return 0f;
            float sum = 0f;
            for (int i = 0; i < fleets.Count; i++) sum += FleetDemand(fleets[i], type);
            return sum;
        }
    }
}
