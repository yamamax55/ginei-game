using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 開示台帳（FND-4 #495・物語の背骨の駆動本体）。開示項目を登録し、コンテキストで評価して<b>連鎖開示</b>を
    /// 不動点まで進める（秘史Aの開示が秘史Bの前提を満たし、Bが解ける…）。状態 <see cref="DisclosureState"/> は直列化可能で
    /// セーブ（FND-2）に乗る。`GameEvent` エンジン（#116）の効果から `DisclosureRules.TryReveal` を呼んで連動もできる。
    /// 提示UI（開示演出）は読む側に委ねる＝headless でテストできる。test-first。
    /// </summary>
    public class DisclosureLedger
    {
        private readonly List<DisclosureEntry> entries = new List<DisclosureEntry>();

        /// <summary>開示状態（開示済みの id 集合・直列化対象）。</summary>
        public DisclosureState State { get; private set; } = new DisclosureState();

        public DisclosureLedger() { }
        public DisclosureLedger(DisclosureState state) { if (state != null) State = state; }

        public void Register(DisclosureEntry entry) { if (entry != null) entries.Add(entry); }

        public DisclosureEntry Get(string id)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i] != null && entries[i].id == id) return entries[i];
            return null;
        }

        public bool IsRevealed(string id) => State.IsRevealed(id);
        public IReadOnlyList<DisclosureEntry> Entries => entries;

        /// <summary>
        /// 評価して開示可能なものを<b>連鎖的に（不動点まで）</b>開示する。新規開示された項目を順に返す。
        /// 1つの開示が別の前提を満たす連鎖を1回の評価で解ききる。条件（コンテキスト依存）は呼ぶたびに再判定される。
        /// </summary>
        public List<DisclosureEntry> Evaluate(EventContext ctx)
        {
            var newlyRevealed = new List<DisclosureEntry>();
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int i = 0; i < entries.Count; i++)
                {
                    if (DisclosureRules.TryReveal(entries[i], State, ctx))
                    {
                        newlyRevealed.Add(entries[i]);
                        changed = true; // 連鎖：再走査で前提が新たに満ちた項目を拾う
                    }
                }
            }
            return newlyRevealed;
        }

        /// <summary>開示の進捗（0..1）。</summary>
        public float Progress() => DisclosureRules.Progress(entries, State);
    }
}
