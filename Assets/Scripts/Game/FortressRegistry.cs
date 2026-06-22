using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// シーン内の戦術要塞（<see cref="FortressUnit"/>）の在庫を保持する静的レジストリ（#76）。
    /// 要塞は <see cref="FleetRegistry"/> にも IShipTarget として登録される（攻撃対象・索敵用）が、
    /// 旗艦(FleetStrength)ではないため AllFlagships には載らない。勝敗判定（#78 要塞攻略/要塞防衛）や
    /// AI の攻城目標選定が「勢力ごとの生存要塞」を引けるよう、要塞専用の在庫をここで別管理する。
    ///
    /// 注意：静的状態のためシーンを跨いで残る。会戦開始時に <see cref="FortressUnit"/> 側の生成前に
    /// 自分のシーン分を <see cref="ClearScene"/> でクリアする（FleetRegistry と同じ規律）。
    /// </summary>
    public static class FortressRegistry
    {
        private static readonly List<FortressUnit> active = new List<FortressUnit>();

        /// <summary>生存中の全要塞（破壊・陥落済みは Unregister 済み）。</summary>
        public static IReadOnlyList<FortressUnit> All => active;

        public static void Register(FortressUnit f)
        {
            if (f == null) return;
            if (!active.Contains(f)) active.Add(f);
        }

        public static void Unregister(FortressUnit f)
        {
            if (f == null) return;
            active.Remove(f);
        }

        public static void Clear() => active.Clear();

        /// <summary>指定勢力の生存要塞が1つでもあるか。</summary>
        public static bool AnyAliveOfFaction(Faction faction)
        {
            for (int i = 0; i < active.Count; i++)
            {
                FortressUnit f = active[i];
                if (f != null && f.IsAlive && f.Faction == faction) return true;
            }
            return false;
        }

        /// <summary>指定シーンに属する要塞だけを在庫から外す（その会戦の開始時クリア。他会戦は残す）。</summary>
        public static void ClearScene(Scene scene)
        {
            active.RemoveAll(f => f == null || f.gameObject.scene == scene);
        }
    }
}
