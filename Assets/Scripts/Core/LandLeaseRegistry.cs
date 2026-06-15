using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 土地賃貸借の台帳（#2019 惑星土地の貸出基盤・static・唯一の窓口）。締結中のリース（<see cref="LandLease"/>）を登録/照会する。
    /// <see cref="PropertyDeedRegistry"/> と同型・有界。Core 純ロジック・test-first。
    /// </summary>
    public static class LandLeaseRegistry
    {
        static readonly List<LandLease> leases = new List<LandLease>();
        static int nextId = 1;

        public static IReadOnlyList<LandLease> All => leases;
        public static int NextId() => nextId++;

        public static LandLease Register(LandLease l)
        {
            if (l == null) return null;
            if (l.id <= 0) l.id = NextId();
            else if (l.id >= nextId) nextId = l.id + 1;
            leases.Add(l);
            return l;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < leases.Count; i++)
                if (leases[i].id == id) { leases.RemoveAt(i); return true; }
            return false;
        }

        public static void Clear() { leases.Clear(); nextId = 1; }

        public static List<LandLease> OnSystem(int systemId)
        {
            var r = new List<LandLease>();
            for (int i = 0; i < leases.Count; i++)
                if (leases[i].systemId == systemId) r.Add(leases[i]);
            return r;
        }

        /// <summary>指定所有者が貸主（地代の受け手）のリース。</summary>
        public static List<LandLease> ByLessor(OwnerRef lessor)
        {
            var r = new List<LandLease>();
            for (int i = 0; i < leases.Count; i++)
                if (Same(leases[i].Lessor, lessor)) r.Add(leases[i]);
            return r;
        }

        /// <summary>指定所有者が借主（地代の払い手）のリース。</summary>
        public static List<LandLease> ByLessee(OwnerRef lessee)
        {
            var r = new List<LandLease>();
            for (int i = 0; i < leases.Count; i++)
                if (Same(leases[i].Lessee, lessee)) r.Add(leases[i]);
            return r;
        }

        static bool Same(OwnerRef a, OwnerRef b) => a.Key == b.Key;
    }
}
