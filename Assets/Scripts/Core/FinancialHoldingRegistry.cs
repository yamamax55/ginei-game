using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 金融資産の保有台帳（NFIN-2・#2070・static・唯一の窓口）。
    /// <see cref="NamedAssetRegistry"/>#2063 と同型＝保有持分の登録/照会/所有者別・銘柄別集計を一手に担う。
    /// <b>有界リスト</b>（ネームド粒度・原資産は既存市場系に委譲）。Core 純ロジック・test-first。
    /// </summary>
    public static class FinancialHoldingRegistry
    {
        static readonly List<FinancialHolding> holdings = new List<FinancialHolding>();
        static int nextId = 1;

        public static IReadOnlyList<FinancialHolding> All => holdings;

        public static int NextId() => nextId++;

        public static FinancialHolding Register(FinancialHolding h)
        {
            if (h == null) return null;
            if (h.id <= 0) h.id = NextId();
            else if (h.id >= nextId) nextId = h.id + 1;
            holdings.Add(h);
            return h;
        }

        public static FinancialHolding Get(int id)
        {
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].id == id) return holdings[i];
            return null;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].id == id) { holdings.RemoveAt(i); return true; }
            return false;
        }

        public static void Clear()
        {
            holdings.Clear();
            nextId = 1;
        }

        /// <summary>人物が保有する持分を列挙。</summary>
        public static List<FinancialHolding> OwnedByPerson(int personId)
        {
            var result = new List<FinancialHolding>();
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsPersonOwned && holdings[i].ownerPersonId == personId) result.Add(holdings[i]);
            return result;
        }

        /// <summary>国家が保有する持分を列挙。</summary>
        public static List<FinancialHolding> OwnedByFaction(Faction faction)
        {
            var result = new List<FinancialHolding>();
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsFactionOwned && holdings[i].ownerFaction == faction) result.Add(holdings[i]);
            return result;
        }

        /// <summary>商品種別で列挙。</summary>
        public static List<FinancialHolding> HoldingsOfInstrument(FinancialInstrument instrument)
        {
            var result = new List<FinancialHolding>();
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].instrument == instrument) result.Add(holdings[i]);
            return result;
        }

        /// <summary>原資産（銘柄）で列挙（紙くず化の一括同期に使う）。</summary>
        public static List<FinancialHolding> HoldingsOfUnderlying(int underlyingId)
        {
            var result = new List<FinancialHolding>();
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].underlyingId == underlyingId) result.Add(holdings[i]);
            return result;
        }

        public static float TotalMarketValueOfPerson(int personId)
        {
            float sum = 0f;
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsPersonOwned && holdings[i].ownerPersonId == personId)
                    sum += FinancialAssetRules.MarketValue(holdings[i]);
            return sum;
        }

        public static float TotalMarketValueOfFaction(Faction faction)
        {
            float sum = 0f;
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsFactionOwned && holdings[i].ownerFaction == faction)
                    sum += FinancialAssetRules.MarketValue(holdings[i]);
            return sum;
        }

        public static int CountOwnedByPerson(int personId)
        {
            int n = 0;
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsPersonOwned && holdings[i].ownerPersonId == personId) n++;
            return n;
        }

        public static int CountOwnedByFaction(Faction faction)
        {
            int n = 0;
            for (int i = 0; i < holdings.Count; i++)
                if (holdings[i].IsFactionOwned && holdings[i].ownerFaction == faction) n++;
            return n;
        }
    }
}
