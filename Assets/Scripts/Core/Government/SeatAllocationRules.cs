using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 得票から整数議席を配る純ロジック（最大剰余法＝ヘア式）。配った議席の合計は必ず定数と一致する。
    /// 端数の取り合いは「剰余が大きい→得票が多い→ID が小さい」の順で決める（決定論）。
    /// </summary>
    public static class SeatAllocationRules
    {
        /// <summary>
        /// 最大剰余法で議席を配る。<paramref name="ids"/> と <paramref name="votes"/> は同じ並び。
        /// 返り値も同じ並びの議席数。総得票が0・定数0以下・候補なしは全員0（議席は配られない）。
        /// </summary>
        public static int[] LargestRemainder(IList<int> ids, IList<float> votes, int seats)
        {
            int n = (ids == null || votes == null) ? 0 : System.Math.Min(ids.Count, votes.Count);
            var result = new int[n];
            if (n == 0 || seats <= 0) return result;

            double total = 0d;
            for (int i = 0; i < n; i++) total += System.Math.Max(0f, votes[i]);
            if (total <= 0d) return result;

            var remainder = new double[n];
            int assigned = 0;
            for (int i = 0; i < n; i++)
            {
                double quota = (double)System.Math.Max(0f, votes[i]) * seats / total;
                int floor = (int)System.Math.Floor(quota);
                result[i] = floor;
                remainder[i] = quota - floor;
                assigned += floor;
            }

            int left = seats - assigned;
            if (left <= 0) return result;

            // 端数の順位（剰余大→得票大→ID小）。LINQ を使わず挿入ソート。
            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = i;
            for (int i = 1; i < n; i++)
            {
                int cur = order[i];
                int j = i - 1;
                while (j >= 0 && Before(cur, order[j], ids, votes, remainder))
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = cur;
            }

            // 剰余の多い順に1議席ずつ。剰余は各1未満なので、余りは剰余が正の（＝得票のある）党の数より少ない。
            // 浮動小数の誤差に備えて得票0の党は飛ばし、得票のある党を周回する。
            int guard = n * (left + 1);
            for (int k = 0; left > 0 && guard > 0; k = (k + 1) % n, guard--)
            {
                int idx = order[k];
                if (System.Math.Max(0f, votes[idx]) <= 0f) continue;
                result[idx]++;
                left--;
            }
            return result;
        }

        private static bool Before(int a, int b, IList<int> ids, IList<float> votes, double[] remainder)
        {
            if (remainder[a] != remainder[b]) return remainder[a] > remainder[b];
            float va = System.Math.Max(0f, votes[a]), vb = System.Math.Max(0f, votes[b]);
            if (va != vb) return va > vb;
            return ids[a] < ids[b];
        }
    }
}
