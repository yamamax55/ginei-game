using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// イベントエンジンの駆動本体（#116・横断基盤）。登録済みイベントを条件評価し、重み付きで1件発火して<b>キュー</b>へ積み
    /// （同時多発は順次提示）、選択肢で解決＝効果適用してキューから外す。提示UIは持たず（<see cref="Current"/> を読む側＝
    /// `EventManager`/モーダルが描画）、ロジックだけを担う＝headless でテストできる。`EventManager` がこれを Tick で駆動する。
    /// 同一イベントの多重キューはしない。test-first。
    /// </summary>
    public class EventEngine
    {
        private class Entry
        {
            public GameEventDef def;
            public EventRuntimeState state = new EventRuntimeState();
        }

        private readonly List<Entry> entries = new List<Entry>();
        private readonly Queue<GameEventDef> pending = new Queue<GameEventDef>();
        private readonly HashSet<GameEventDef> inQueue = new HashSet<GameEventDef>();

        /// <summary>イベントを登録する。</summary>
        public void Register(GameEventDef def)
        {
            if (def == null) return;
            entries.Add(new Entry { def = def });
        }

        /// <summary>提示待ちのイベント数。</summary>
        public int PendingCount => pending.Count;

        /// <summary>いま提示すべきイベント（先頭。無ければ null）。UI はこれを描画する。</summary>
        public GameEventDef Current => pending.Count > 0 ? pending.Peek() : null;

        /// <summary>指定 id の発火回数（テスト/セーブ用。未登録は0）。</summary>
        public int FireCount(string id)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].def != null && entries[i].def.id == id) return entries[i].state.fireCount;
            return 0;
        }

        /// <summary>
        /// 条件評価して<b>1件</b>発火しキューへ積む（roll で重み付き抽選）。発火したらその定義を返す（無ければ null）。
        /// 既にキュー中のイベントは候補から除く。`EventManager` が間引き間隔で呼ぶ想定。
        /// </summary>
        public GameEventDef Tick(EventContext ctx, float now, float roll)
        {
            var eligible = new List<GameEventDef>();
            var eligibleEntries = new List<Entry>();
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (inQueue.Contains(e.def)) continue; // 多重提示しない
                if (EventRules.IsEligible(e.def, e.state, ctx, now))
                {
                    eligible.Add(e.def);
                    eligibleEntries.Add(e);
                }
            }
            if (eligible.Count == 0) return null;

            GameEventDef chosen = EventRules.SelectWeighted(eligible, roll);
            if (chosen == null) return null;

            // 選ばれた定義の state を発火記録
            for (int i = 0; i < eligibleEntries.Count; i++)
            {
                if (eligibleEntries[i].def == chosen)
                {
                    EventRules.MarkFired(eligibleEntries[i].state, now);
                    break;
                }
            }
            pending.Enqueue(chosen);
            inQueue.Add(chosen);
            return chosen;
        }

        /// <summary>
        /// 現在のイベントを選択肢 <paramref name="choiceIndex"/> で解決する（効果適用＋キューから外す）。
        /// 通知（選択肢なし/確認のみ）は index 範囲外で呼んでよい。
        /// </summary>
        public void Resolve(int choiceIndex, EventContext ctx)
        {
            if (pending.Count == 0) return;
            GameEventDef def = pending.Dequeue();
            inQueue.Remove(def);
            EventRules.ApplyChoice(def, choiceIndex, ctx);
        }

        /// <summary>キューを空にする（シーン遷移・リセット用。発火履歴は保持）。</summary>
        public void ClearPending()
        {
            pending.Clear();
            inQueue.Clear();
        }
    }
}
