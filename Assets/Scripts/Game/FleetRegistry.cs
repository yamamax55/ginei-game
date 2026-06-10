using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 全 IShipTarget（旗艦 FleetStrength ＋配下艦 EscortShip）の在庫を保持する静的レジストリ。
    /// 各艦が出現時(Register)・破棄/退却時(Unregister)に自己登録／解除し、
    /// 敵探索を毎回の FindObjectsByType から「在庫リストの参照＋敵対判定」に置き換えて軽量化する。
    ///
    /// 多勢力対応：陣営別のバケットは持たず、全艦を1リストで保持する。敵味方の区別は
    /// 探索側が `FactionRelations.IsHostile` で判定する（3勢力以上でもコード変更不要）。
    ///
    /// 注意：
    /// - 静的状態のためシーンを跨いで残る。会戦開始時に `BattleSetup.Awake` が `Clear()` する。
    /// - 返すリストは内部参照（読み取り専用）。列挙中に Register/Unregister しないこと（探索は読み取りのみ）。
    /// </summary>
    public static class FleetRegistry
    {
        // 全攻撃対象（旗艦＋配下艦）。ShipCombat の敵探索が参照。
        private static readonly List<IShipTarget> allTargets = new List<IShipTarget>();

        // 旗艦のみ。FleetAI の接近目標・BattleManager の勝敗カウントが参照。
        private static readonly List<FleetStrength> allFlagships = new List<FleetStrength>();

        /// <summary>全攻撃対象（旗艦＋配下艦）。敵味方は呼び出し側が IsHostile で判定する。</summary>
        public static IReadOnlyList<IShipTarget> AllTargets => allTargets;

        /// <summary>全旗艦（生存中のみ。退却・破棄したものは Unregister 済み）。</summary>
        public static IReadOnlyList<FleetStrength> AllFlagships => allFlagships;

        /// <summary>出現した艦を登録する。</summary>
        public static void Register(IShipTarget t)
        {
            if (t == null) return;
            if (!allTargets.Contains(t)) allTargets.Add(t);
            if (t is FleetStrength fs && !allFlagships.Contains(fs)) allFlagships.Add(fs);
        }

        /// <summary>破棄・退却した艦を登録解除する。</summary>
        public static void Unregister(IShipTarget t)
        {
            if (t == null) return;
            allTargets.Remove(t);
            if (t is FleetStrength fs) allFlagships.Remove(fs);
        }

        /// <summary>全リストを空にする（会戦開始時に呼ぶ）。</summary>
        public static void Clear()
        {
            allTargets.Clear();
            allFlagships.Clear();
        }
    }
}
