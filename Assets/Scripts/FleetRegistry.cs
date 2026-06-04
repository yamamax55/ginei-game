using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 全 IShipTarget（旗艦 FleetStrength ＋配下艦 EscortShip）の在庫を陣営別に保持する静的レジストリ。
    /// 各艦が出現時(Register)・破棄/退却時(Unregister)に自己登録／解除し、
    /// 敵探索を毎回の FindObjectsByType から「陣営別リストの参照」に置き換えて軽量化する。
    ///
    /// 注意：
    /// - 静的状態のためシーンを跨いで残る。会戦開始時に `BattleSetup.Awake` が `Clear()` する。
    /// - 陣営は登録時の値でバケット分けする（陣営は実行中に変化しない前提）。
    /// - 返すリストは内部参照（読み取り専用）。列挙中に Register/Unregister しないこと（探索は読み取りのみ）。
    /// </summary>
    public static class FleetRegistry
    {
        // 全攻撃対象（旗艦＋配下艦）。ShipCombat の敵探索が参照。
        private static readonly List<IShipTarget> imperialTargets = new List<IShipTarget>();
        private static readonly List<IShipTarget> allianceTargets = new List<IShipTarget>();

        // 旗艦のみ。FleetAI の接近目標・BattleManager の勝敗カウントが参照。
        private static readonly List<FleetStrength> imperialFlagships = new List<FleetStrength>();
        private static readonly List<FleetStrength> allianceFlagships = new List<FleetStrength>();

        /// <summary>出現した艦を登録する。</summary>
        public static void Register(IShipTarget t)
        {
            if (t == null) return;

            if (t.Faction == Faction.帝国)
            {
                if (!imperialTargets.Contains(t)) imperialTargets.Add(t);
            }
            else
            {
                if (!allianceTargets.Contains(t)) allianceTargets.Add(t);
            }

            // 旗艦は別リストにも登録
            if (t is FleetStrength fs)
            {
                if (fs.faction == Faction.帝国)
                {
                    if (!imperialFlagships.Contains(fs)) imperialFlagships.Add(fs);
                }
                else
                {
                    if (!allianceFlagships.Contains(fs)) allianceFlagships.Add(fs);
                }
            }
        }

        /// <summary>破棄・退却した艦を登録解除する（両陣営から除去して取りこぼしを防ぐ）。</summary>
        public static void Unregister(IShipTarget t)
        {
            if (t == null) return;
            imperialTargets.Remove(t);
            allianceTargets.Remove(t);
            if (t is FleetStrength fs)
            {
                imperialFlagships.Remove(fs);
                allianceFlagships.Remove(fs);
            }
        }

        /// <summary>自陣営から見た敵の全攻撃対象（旗艦＋配下艦）。</summary>
        public static IReadOnlyList<IShipTarget> GetEnemies(Faction myFaction)
            => (myFaction == Faction.帝国) ? allianceTargets : imperialTargets;

        /// <summary>自陣営から見た敵の旗艦のみ。</summary>
        public static IReadOnlyList<FleetStrength> GetEnemyFlagships(Faction myFaction)
            => (myFaction == Faction.帝国) ? allianceFlagships : imperialFlagships;

        /// <summary>指定陣営の生存旗艦（退却・破棄したものは含まない）。</summary>
        public static IReadOnlyList<FleetStrength> GetFlagships(Faction faction)
            => (faction == Faction.帝国) ? imperialFlagships : allianceFlagships;

        /// <summary>全リストを空にする（会戦開始時に呼ぶ）。</summary>
        public static void Clear()
        {
            imperialTargets.Clear();
            allianceTargets.Clear();
            imperialFlagships.Clear();
            allianceFlagships.Clear();
        }
    }
}
