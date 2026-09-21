using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 目安箱を通じて初めて見える「無名の継承」の世界観開示（MEYASU-8 #1304）。
    /// 既存 Petition.hops を読むだけで別の伝播状態は持たず、DisclosureLedger の連鎖へ載せる。
    /// </summary>
    public static class MeyasubakoDisclosures
    {
        public const string Seed = "meyasu_seed_sown";
        public const string Relay = "meyasu_unsung_relay";
        public const string Legacy = "meyasu_anonymous_legacy";

        public static DisclosureEntry SeedSown(Func<IReadOnlyList<Petition>> source)
            => new DisclosureEntry(Seed, "撒かれた種",
                "命令する権限がなくとも、建白という種は撒ける。最初の書き手の名は残らず、文面だけが次の手へ渡った。", "制度誌")
                .When(_ => Any(source, p => p.hops != null && p.hops.Count >= 1));

        public static DisclosureEntry UnsungRelay(Func<IReadOnlyList<Petition>> source)
            => new DisclosureEntry(Relay, "無名の手のリレー",
                "建白は一人の英雄が実現したのではない。名もなき中継者が読み、運び、ときに歪めながら制度へ近づけた。", "列伝")
                .Requires(Seed)
                .When(_ => Any(source, p => p.hops != null && (p.hops.Count >= 2 || p.distorted || p.status == PetitionStatus.再浮上)));

        public static DisclosureEntry AnonymousLegacy(Func<IReadOnlyList<Petition>> source)
            => new DisclosureEntry(Legacy, "名を残さない政策",
                "誰が始めたかは記録されない。残るのは、無名の手を経て執行された政策と、その結果だけである。", "世界観")
                .Requires(Relay)
                .When(_ => Any(source, p => p.drafterId == 0 && p.hops != null && p.hops.Count > 0
                    && (p.status == PetitionStatus.承認 || p.status == PetitionStatus.執行済)));

        private static bool Any(Func<IReadOnlyList<Petition>> source, Predicate<Petition> predicate)
        {
            IReadOnlyList<Petition> items = source != null ? source() : null;
            if (items == null) return false;
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && predicate(items[i])) return true;
            return false;
        }
    }
}
