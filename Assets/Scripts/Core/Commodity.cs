using System.Collections.Generic;

namespace Ginei
{
    /// <summary>品目の区分（BOM-1・#2098）。原材料／中間財／消費財（食品・衣類・住宅…）／資本財／軍需。</summary>
    public enum CommodityCategory { 原材料, 中間財, 消費財, 資本財, 軍需 }

    /// <summary>
    /// 品目（コモディティ・BOM-1・#2098・純データ）。汎用BOMの構成単位。
    /// 消費財カテゴリの例＝食品/衣類/住宅。少数に絞る（集約）。test-first。
    /// </summary>
    public class Commodity
    {
        public int id;
        public string name;
        public CommodityCategory category;

        public Commodity() { }

        public Commodity(int id, string name, CommodityCategory category)
        {
            this.id = id;
            this.name = name;
            this.category = category;
        }
    }

    /// <summary>
    /// 品目カタログ（BOM-1・#2098・static・唯一の窓口）。品目を登録・採番し、id/名前/カテゴリで引く。
    /// `FleetRoster`/`CommodityCatalog` と同型の static レジストリ。Core 純ロジック・test-first。
    /// </summary>
    public static class CommodityCatalog
    {
        static readonly List<Commodity> items = new List<Commodity>();
        static int nextId = 1;

        public static IReadOnlyList<Commodity> All => items;

        public static int NextId() => nextId++;

        /// <summary>品目を登録（同名があればそれを返す＝冪等）。</summary>
        public static Commodity Register(string name, CommodityCategory category)
        {
            var existing = ByName(name);
            if (existing != null) return existing;
            var c = new Commodity(NextId(), name, category);
            items.Add(c);
            return c;
        }

        public static Commodity Get(int id)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].id == id) return items[i];
            return null;
        }

        public static Commodity ByName(string name)
        {
            for (int i = 0; i < items.Count; i++)
                if (items[i].name == name) return items[i];
            return null;
        }

        public static List<Commodity> ByCategory(CommodityCategory category)
        {
            var result = new List<Commodity>();
            for (int i = 0; i < items.Count; i++)
                if (items[i].category == category) result.Add(items[i]);
            return result;
        }

        public static void Clear()
        {
            items.Clear();
            nextId = 1;
        }
    }
}
