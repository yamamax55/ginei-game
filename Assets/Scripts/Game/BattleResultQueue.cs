using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// ウィンドウ化会戦（WIN-3 #2570）の結果を戦略へ**直列に**書き戻すためのキュー。各会戦は別シーンで同時進行し
    /// 独立に決着するため、決着した会戦は自分の受け渡しスナップショット（結果込み）をここへ積む。
    /// <see cref="GalaxyView"/> が1フレーム1件ずつ <see cref="BattleHandoff.Restore"/> して既存の反映処理
    /// （<c>StrategyRules.ApplyHandoffResult</c> / 攻城反映）に流す＝global BattleHandoff を奪い合わない。
    /// </summary>
    public static class BattleResultQueue
    {
        private static readonly Queue<BattleHandoff.State> pending = new Queue<BattleHandoff.State>();

        /// <summary>未反映の結果件数。</summary>
        public static int Count => pending.Count;

        /// <summary>決着した会戦の結果（受け渡しスナップショット）を積む。</summary>
        public static void Push(BattleHandoff.State state)
        {
            if (state != null) pending.Enqueue(state);
        }

        /// <summary>先頭の結果を取り出す（無ければ false）。</summary>
        public static bool TryPop(out BattleHandoff.State state)
        {
            if (pending.Count > 0) { state = pending.Dequeue(); return true; }
            state = null;
            return false;
        }

        /// <summary>全消去（戦略の初期化・タイトルへ戻る時など）。</summary>
        public static void Clear() => pending.Clear();
    }
}
