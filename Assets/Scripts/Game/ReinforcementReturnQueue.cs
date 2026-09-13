using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 会戦が終わったのに<b>まだ着いていなかった援軍</b>を、戦略マップへ差し戻すための受け渡し（#38 C-5）。
    ///
    /// 会戦シーン（<see cref="BattleManager"/>）が戦場を閉じたときに積み、戦略シーンへ戻った
    /// <see cref="GalaxyView"/> が取り出して艦隊を盤面へ戻す。積みっぱなしにすると派遣した艦隊が
    /// 宙に浮く（消えたように見える）ので、**戦略へ戻るすべての経路で必ず空にする**。
    ///
    /// シーンを跨ぐので static。<see cref="BattleResultQueue"/>（会戦結果の受け渡し）と同じ役回りで、
    /// 複数同時会戦（WIN-3）でも各会戦が自分の閉鎖ぶんを積むだけなので取り違えない。
    /// </summary>
    public static class ReinforcementReturnQueue
    {
        // 会戦は多くても数件・援軍も少数なので有界化は不要だが、取り忘れを溜め込まないよう上限を設ける。
        private const int MaxEntries = 64;
        private static readonly List<WarpReinforcement> pending = new List<WarpReinforcement>();

        /// <summary>差し戻す援軍を積む（会戦側）。</summary>
        public static void Push(WarpReinforcement order)
        {
            if (!order.IsValid) return;
            pending.Add(order);
            if (pending.Count > MaxEntries) pending.RemoveAt(0); // 古いものから捨てる（無制限増加を避ける）
        }

        // 到着して会戦に参戦した援軍の艦隊ID。**差し戻しとは別**に持つ。
        // 到着した艦隊は台帳から消えるので、これを控えておかないと戦略側で
        // 「増援航行中」の印が外れず、盤面に残ったまま永久に操作できなくなる（実機QAで判明）。
        private static readonly List<int> arrivedFleetIds = new List<int>();

        /// <summary>戦場に到着して参戦した援軍を控える（会戦側）。</summary>
        public static void PushArrived(int fleetId)
        {
            if (fleetId == 0 || arrivedFleetIds.Contains(fleetId)) return;
            arrivedFleetIds.Add(fleetId);
            if (arrivedFleetIds.Count > MaxEntries) arrivedFleetIds.RemoveAt(0);
        }

        /// <summary>到着済みの艦隊IDを<b>消さずに</b>覗く（戦略側で参戦者の一覧を作るのに使う）。</summary>
        public static int PeekArrivedIds(List<int> outList)
        {
            if (outList == null) return 0;
            outList.AddRange(arrivedFleetIds);
            return arrivedFleetIds.Count;
        }

        /// <summary>到着済みの艦隊IDを全部取り出して空にする（戦略側）。0件なら false。</summary>
        public static bool TakeArrivedIds(List<int> outList)
        {
            if (outList == null || arrivedFleetIds.Count == 0) return false;
            outList.AddRange(arrivedFleetIds);
            arrivedFleetIds.Clear();
            return true;
        }

        /// <summary>溜まっている差し戻しを全部取り出して空にする（戦略側）。0件なら false。</summary>
        public static bool TakeAll(List<WarpReinforcement> outList)
        {
            if (outList == null || pending.Count == 0) return false;
            outList.AddRange(pending);
            pending.Clear();
            return true;
        }

        /// <summary>溜まっている件数（デバッグ・観測用）。</summary>
        public static int Count => pending.Count;

        /// <summary>全消去（シーン初期化・新規戦役）。</summary>
        public static void Clear() { pending.Clear(); arrivedFleetIds.Clear(); }
    }
}
