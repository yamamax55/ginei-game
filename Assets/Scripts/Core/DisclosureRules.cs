using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 開示の純ロジック（FND-4 #495・秘史開示 #450・唯一の窓口）。前提（先行開示）と条件（コンテキスト）が揃ったとき
    /// 真実を開示し、開示時の効果を適用する。世界観EPIC（秘史/天井CAP/ID vs進化論/ニーチェ/啓蒙/エンディング…）は
    /// すべて <see cref="DisclosureEntry"/> として乗る＝“構想が実装可能資産になる”背骨。連鎖開示は <see cref="DisclosureLedger"/>。test-first。
    /// </summary>
    public static class DisclosureRules
    {
        /// <summary>前提（先行開示）がすべて満たされているか。</summary>
        public static bool PrerequisitesMet(DisclosureEntry entry, DisclosureState state)
        {
            if (entry == null || state == null) return false;
            for (int i = 0; i < entry.prerequisites.Count; i++)
                if (!state.IsRevealed(entry.prerequisites[i])) return false;
            return true;
        }

        /// <summary>開示条件を満たすか（条件 null＝常に真）。</summary>
        public static bool ConditionMet(DisclosureEntry entry, EventContext ctx)
            => entry != null && (entry.condition == null || entry.condition(ctx));

        /// <summary>いま開示できるか（未開示＋前提充足＋条件成立）。</summary>
        public static bool CanReveal(DisclosureEntry entry, DisclosureState state, EventContext ctx)
        {
            if (entry == null || state == null) return false;
            if (state.IsRevealed(entry.id)) return false;
            return PrerequisitesMet(entry, state) && ConditionMet(entry, ctx);
        }

        /// <summary>開示できるなら開示する（状態へ記録＋効果適用）。開示したら true。</summary>
        public static bool TryReveal(DisclosureEntry entry, DisclosureState state, EventContext ctx)
        {
            if (!CanReveal(entry, state, ctx)) return false;
            state.Reveal(entry.id);
            entry.onReveal?.Invoke(ctx);
            return true;
        }

        /// <summary>開示の進捗（0..1＝開示済み項目数/全項目数）。エンディング条件・達成率の素。</summary>
        public static float Progress(IList<DisclosureEntry> all, DisclosureState state)
        {
            if (all == null || all.Count == 0 || state == null) return 0f;
            int revealed = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && state.IsRevealed(all[i].id)) revealed++;
            return (float)revealed / all.Count;
        }
    }
}
