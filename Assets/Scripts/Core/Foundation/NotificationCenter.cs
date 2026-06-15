using System.Collections.Generic;

namespace Ginei
{
    /// <summary>通知の分類（#964 NOTIF-1）。フィードの色分け・フィルタに使う。</summary>
    public enum NotificationCategory { システム, 戦闘, 建艦, 占領, 政治, 人事, 内政, 外交 }

    /// <summary>通知の重要度（#964）。フィードの色・将来のポーズ/音に使う。</summary>
    public enum NotificationSeverity { 情報, 注意, 警告 }

    /// <summary>1件の通知（#964 NOTIF-1・純データ）。表示時刻は UI 側が付与＝Core は timeless（テスト容易）。</summary>
    public readonly struct Notification
    {
        public readonly long seq;
        public readonly NotificationCategory category;
        public readonly NotificationSeverity severity;
        public readonly string message;

        public Notification(long seq, NotificationCategory category, NotificationSeverity severity, string message)
        {
            this.seq = seq;
            this.category = category;
            this.severity = severity;
            this.message = message ?? "";
        }
    }

    /// <summary>
    /// 通知の単一窓口（#964 NOTIF-1）。散在する通知（battleMsg/ShowMessage 等）をここへ集約し、左下フィード（NOTIF-2）が読む。
    /// 一意の <c>seq</c> を採番してリングバッファ（<see cref="Capacity"/>）に保持。UI は <see cref="Since"/> で前回以降の新着だけ取得する。
    /// 純ロジック・シーン非依存・test-first。会戦中在庫等とは無関係＝表示用の在庫。
    /// </summary>
    public static class NotificationCenter
    {
        /// <summary>保持上限（古いものから捨てる）。履歴パネル（NOTIF-5）もこの範囲。</summary>
        public const int Capacity = 100;

        private static readonly List<Notification> items = new List<Notification>();
        private static long nextSeq = 1;

        /// <summary>最後に採番した seq（未通知なら0）。フィードが生成時にここまでを既読扱いにしてフラッドを防ぐ。</summary>
        public static long LastSeq => nextSeq - 1;

        /// <summary>全保持通知（古い順・読み取り専用）。</summary>
        public static IReadOnlyList<Notification> All => items;

        /// <summary>通知を積む。採番した seq を返す。</summary>
        public static long Push(NotificationCategory category, NotificationSeverity severity, string message)
        {
            long seq = nextSeq++;
            items.Add(new Notification(seq, category, severity, message));
            if (items.Count > Capacity) items.RemoveRange(0, items.Count - Capacity);
            return seq;
        }

        /// <summary>重要度 既定（情報）で積む簡易版。</summary>
        public static long Push(NotificationCategory category, string message)
            => Push(category, NotificationSeverity.情報, message);

        /// <summary><paramref name="afterSeq"/> より新しい通知を古い順で返す（フィードの差分取得用）。</summary>
        public static List<Notification> Since(long afterSeq)
        {
            var r = new List<Notification>();
            for (int i = 0; i < items.Count; i++)
                if (items[i].seq > afterSeq) r.Add(items[i]);
            return r;
        }

        /// <summary>直近 <paramref name="count"/> 件を新しい順で返す（履歴パネル用）。</summary>
        public static List<Notification> Recent(int count)
        {
            var r = new List<Notification>();
            if (count <= 0) return r;
            for (int i = items.Count - 1; i >= 0 && r.Count < count; i--) r.Add(items[i]);
            return r;
        }

        /// <summary>全消去＋採番リセット（シーン初期化・テスト）。</summary>
        public static void Clear() { items.Clear(); nextSeq = 1; }
    }
}
