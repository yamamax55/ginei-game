using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 不動産権利証の台帳（NFIN-4・#2070・static・唯一の窓口）。
    /// 惑星別の持分（deed）を登録/照会し、1惑星の持分合計（健全なら≤1）と細分化度（枚数）を集計する。
    /// <see cref="NamedAssetRegistry"/>#2063 と同型・有界。Core 純ロジック・test-first。
    /// </summary>
    public static class PropertyDeedRegistry
    {
        static readonly List<PropertyDeed> deeds = new List<PropertyDeed>();
        static int nextId = 1;

        public static IReadOnlyList<PropertyDeed> All => deeds;

        public static int NextId() => nextId++;

        public static PropertyDeed Register(PropertyDeed d)
        {
            if (d == null) return null;
            if (d.id <= 0) d.id = NextId();
            else if (d.id >= nextId) nextId = d.id + 1;
            deeds.Add(d);
            return d;
        }

        public static PropertyDeed Get(int id)
        {
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].id == id) return deeds[i];
            return null;
        }

        public static bool Remove(int id)
        {
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].id == id) { deeds.RemoveAt(i); return true; }
            return false;
        }

        public static void Clear()
        {
            deeds.Clear();
            nextId = 1;
        }

        public static List<PropertyDeed> OwnedByPerson(int personId)
        {
            var result = new List<PropertyDeed>();
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].IsPersonOwned && deeds[i].ownerPersonId == personId) result.Add(deeds[i]);
            return result;
        }

        public static List<PropertyDeed> OwnedByFaction(Faction faction)
        {
            var result = new List<PropertyDeed>();
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].IsFactionOwned && deeds[i].ownerFaction == faction) result.Add(deeds[i]);
            return result;
        }

        /// <summary>指定惑星の権利証を列挙。</summary>
        public static List<PropertyDeed> DeedsOnSystem(int systemId)
        {
            var result = new List<PropertyDeed>();
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].systemId == systemId) result.Add(deeds[i]);
            return result;
        }

        /// <summary>指定惑星の持分合計（1以下が健全）。</summary>
        public static float TotalShareOnSystem(int systemId)
        {
            float sum = 0f;
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].systemId == systemId) sum += deeds[i].share;
            return sum;
        }

        /// <summary>指定惑星の権利証枚数（細分化度＝多いほど細分化）。</summary>
        public static int CountDeedsOnSystem(int systemId)
        {
            int n = 0;
            for (int i = 0; i < deeds.Count; i++)
                if (deeds[i].systemId == systemId) n++;
            return n;
        }
    }
}
