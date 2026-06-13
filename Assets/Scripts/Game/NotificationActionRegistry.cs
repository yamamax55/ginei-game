using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 通知（<see cref="NotificationCenter"/>）の seq に「ダブルクリックで実行するアクション」を紐づける窓口（Game層）。
    /// Core の <see cref="Notification"/> は純データ（payload を持たない＝test-first）なので、操作はここで seq→Action として持つ。
    /// 例：接敵通知に「その回廊の会戦へ潜行」を登録し、<see cref="NotificationFeed"/> の行ダブルクリックで実行する。
    /// シーンを跨いだ stale 参照（破棄済み MonoBehaviour 捕捉）を避けるため、登録元（GalaxyView 等）が破棄時に <see cref="Clear"/> する。
    /// </summary>
    public static class NotificationActionRegistry
    {
        // 無制限増加を避ける（古い seq から間引く）。接敵通知は少数なので十分。
        private const int MaxEntries = 64;
        private static readonly Dictionary<long, System.Action> actions = new Dictionary<long, System.Action>();

        /// <summary>通知 seq にアクションを登録（同 seq は上書き）。</summary>
        public static void Register(long seq, System.Action action)
        {
            if (action == null) return;
            actions[seq] = action;
            if (actions.Count > MaxEntries) PruneOldest();
        }

        /// <summary>その seq にアクションが紐づいているか（行をクリック可能にするか判定）。</summary>
        public static bool Has(long seq) => actions.ContainsKey(seq);

        /// <summary>seq のアクションがあれば実行して true。無ければ false。</summary>
        public static bool TryInvoke(long seq)
        {
            if (!actions.TryGetValue(seq, out System.Action a) || a == null) return false;
            a();
            return true;
        }

        /// <summary>全消去（シーン離脱・初期化）。stale 参照を残さない。</summary>
        public static void Clear() => actions.Clear();

        private static void PruneOldest()
        {
            long min = long.MaxValue;
            foreach (long k in actions.Keys) if (k < min) min = k;
            if (min != long.MaxValue) actions.Remove(min);
        }
    }
}
