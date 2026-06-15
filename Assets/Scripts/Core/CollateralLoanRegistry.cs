using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 資産担保融資の台帳（#186/#2070・static・唯一の窓口）。締結中の担保ローン（<see cref="CollateralLoan"/>）を登録/照会する。
    /// <see cref="LandLeaseRegistry"/> と同型・有界。Core 純ロジック・test-first。
    /// </summary>
    public static class CollateralLoanRegistry
    {
        static readonly List<CollateralLoan> loans = new List<CollateralLoan>();
        static int nextId = 1;

        public static IReadOnlyList<CollateralLoan> All => loans;
        public static int NextId() => nextId++;

        public static CollateralLoan Register(CollateralLoan l)
        {
            if (l == null) return null;
            if (l.id <= 0) l.id = NextId();
            else if (l.id >= nextId) nextId = l.id + 1;
            loans.Add(l);
            return l;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < loans.Count; i++)
                if (loans[i].id == id) { loans.RemoveAt(i); return true; }
            return false;
        }

        public static void Clear() { loans.Clear(); nextId = 1; }

        /// <summary>指定借り手のローン。</summary>
        public static List<CollateralLoan> ByBorrower(OwnerRef borrower)
        {
            var r = new List<CollateralLoan>();
            for (int i = 0; i < loans.Count; i++)
                if (loans[i].Borrower.Key == borrower.Key) r.Add(loans[i]);
            return r;
        }

        /// <summary>指定貸し手のローン（与信残高の出所）。</summary>
        public static List<CollateralLoan> ByLender(OwnerRef lender)
        {
            var r = new List<CollateralLoan>();
            for (int i = 0; i < loans.Count; i++)
                if (loans[i].Lender.Key == lender.Key) r.Add(loans[i]);
            return r;
        }
    }
}
