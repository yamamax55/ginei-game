using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// ネームド資産の所有台帳（NASSET-2・#2063・static・唯一の窓口）。
    /// <see cref="FleetRoster"/>/`GovernmentRegistry` と同型＝資産の登録/照会/所有者別集計を一手に担う。
    /// <b>有界リスト</b>（ネームド粒度＝固有資産のみ・質量シミュではない＝終盤ラグを生まない）。Core 純ロジック・test-first。
    /// </summary>
    public static class NamedAssetRegistry
    {
        static readonly List<NamedAsset> assets = new List<NamedAsset>();
        static int nextId = 1;

        /// <summary>全資産（読み取り用）。</summary>
        public static IReadOnlyList<NamedAsset> All => assets;

        /// <summary>採番（既存最大+1 を保証）。</summary>
        public static int NextId()
        {
            int id = nextId++;
            return id;
        }

        /// <summary>登録（id 未設定なら採番）。</summary>
        public static NamedAsset Register(NamedAsset a)
        {
            if (a == null) return null;
            if (a.id <= 0) a.id = NextId();
            else if (a.id >= nextId) nextId = a.id + 1;
            assets.Add(a);
            return a;
        }

        /// <summary>id で取得（無ければ null）。</summary>
        public static NamedAsset Get(int id)
        {
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].id == id) return assets[i];
            return null;
        }

        /// <summary>除去（成功で true）。</summary>
        public static bool Remove(int id)
        {
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].id == id) { assets.RemoveAt(i); return true; }
            return false;
        }

        /// <summary>全消去（採番もリセット）。</summary>
        public static void Clear()
        {
            assets.Clear();
            nextId = 1;
        }

        /// <summary>人物が所有する資産を列挙。</summary>
        public static List<NamedAsset> OwnedByPerson(int personId)
        {
            var result = new List<NamedAsset>();
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsPersonOwned && assets[i].ownerPersonId == personId) result.Add(assets[i]);
            return result;
        }

        /// <summary>国家が所有する資産を列挙。</summary>
        public static List<NamedAsset> OwnedByFaction(Faction faction)
        {
            var result = new List<NamedAsset>();
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsFactionOwned && assets[i].ownerFaction == faction) result.Add(assets[i]);
            return result;
        }

        /// <summary>人物の所有資産の時価合計。</summary>
        public static float TotalValueOfPerson(int personId)
        {
            float sum = 0f;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsPersonOwned && assets[i].ownerPersonId == personId) sum += assets[i].value;
            return sum;
        }

        /// <summary>国家の所有資産の時価合計。</summary>
        public static float TotalValueOfFaction(Faction faction)
        {
            float sum = 0f;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsFactionOwned && assets[i].ownerFaction == faction) sum += assets[i].value;
            return sum;
        }

        /// <summary>人物の所有資産数。</summary>
        public static int CountOwnedByPerson(int personId)
        {
            int n = 0;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsPersonOwned && assets[i].ownerPersonId == personId) n++;
            return n;
        }

        /// <summary>国家の所有資産数。</summary>
        public static int CountOwnedByFaction(Faction faction)
        {
            int n = 0;
            for (int i = 0; i < assets.Count; i++)
                if (assets[i].IsFactionOwned && assets[i].ownerFaction == faction) n++;
            return n;
        }
    }
}
