using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の艦隊プール＝**保有総艦艇数**のストア（#148 編成 ／ #884 造船の供給先）。造船の完成
    /// （<see cref="ShipyardRules.CommissionToPool"/>）で増え、編成画面（<see cref="FleetPoolRules"/>）がこれを総プールとして読む。
    /// 個々の艦隊 <see cref="FleetRoster"/>・会戦中在庫 <see cref="FleetRegistry"/> とは別物＝「勢力が配分できる総艦艇」の単一の出所。
    /// 値は非負。勢力ごとに独立。test-first。
    /// </summary>
    public static class FleetPool
    {
        private static readonly Dictionary<Faction, int> total = new Dictionary<Faction, int>();

        /// <summary>全勢力のプールを空にする（会戦セットアップ・テスト初期化）。</summary>
        public static void Clear() => total.Clear();

        /// <summary>勢力の総艦艇プール（未設定は0）。</summary>
        public static int Get(Faction f) => total.TryGetValue(f, out var v) ? v : 0;

        /// <summary>総プールを設定する（負は0クランプ）。</summary>
        public static void Set(Faction f, int amount) => total[f] = Mathf.Max(0, amount);

        /// <summary>総プールを増減する（造船で増・損耗で減）。結果は0下限。新しい総数を返す。</summary>
        public static int Add(Faction f, int delta)
        {
            int v = Mathf.Max(0, Get(f) + delta);
            total[f] = v;
            return v;
        }
    }
}
