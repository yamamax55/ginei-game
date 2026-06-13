using UnityEngine;

namespace Ginei
{
    /// <summary>ネームド資産のカテゴリ（NASSET-1・#2063）。固有名を持つ個別資産の種類（少数に絞る）。</summary>
    public enum NamedAssetCategory { 邸宅, 領地, 旗艦, 美術品, 企業, 財宝, 宮殿, 称号 }

    /// <summary>ネームド資産の所有者種別（NASSET-1・#2063）。人物（<see cref="Person"/>）か国家（<see cref="Faction"/>）か。</summary>
    public enum AssetOwnerKind { 人物, 国家 }

    /// <summary>
    /// ネームド資産（固有名を持つ個別資産・NASSET-1・#2063・純データ・後方互換）。
    /// 人物（<see cref="Person"/>）または国家（<see cref="Faction"/>）が所有する識別可能な資産＝邸宅/領地/旗艦/美術品/企業/財宝/宮殿/称号。
    /// 集約的な液状財産（人物=<see cref="Person.wealth"/>#2056／国家=treasury#163）とは別＝<b>個別の固有資産</b>。
    /// 相続#152・没収#154・贈与で所有者が変わり、収益/維持費（<see cref="NamedAssetRules"/>）・威信（支持#113）を生む。test-first。
    /// </summary>
    public class NamedAsset
    {
        public int id;
        public string name;                  // 固有名（例：「獅子泉宮」「ブリュンヒルト」）
        public NamedAssetCategory category;

        // --- 所有者（ポリモーフィズム＝人物 or 国家） ---
        public AssetOwnerKind ownerKind = AssetOwnerKind.人物;
        public int ownerPersonId;            // ownerKind==人物 のとき有効
        public Faction ownerFaction;         // ownerKind==国家 のとき有効

        // --- 価値・収益（評価は NamedAssetRules） ---
        public float value;                  // 時価（個別資産の評価額）
        public float yieldRate = 0f;         // 年間収益率（領地/企業は収益を生む）
        public float upkeepRate = 0f;        // 維持費率（邸宅/宮殿/旗艦は維持費がかかる）
        public float appreciationRate = 0f;  // 年間値上がり率（美術品/財宝は値上がる・暴落#185 で負も）
        public float prestige = 0f;          // 威信（固有資産の格＝支持#113/正統性へ）

        public bool transferable = true;     // 相続・譲渡・没収できるか（称号など不可のものは false）

        public NamedAsset() { }

        public NamedAsset(int id, string name, NamedAssetCategory category)
        {
            this.id = id;
            this.name = name;
            this.category = category;
        }

        /// <summary>人物所有か。</summary>
        public bool IsPersonOwned => ownerKind == AssetOwnerKind.人物;

        /// <summary>国家所有か。</summary>
        public bool IsFactionOwned => ownerKind == AssetOwnerKind.国家;

        /// <summary>所有者キー（表示/集計用＝人物は "P:id"、国家は "F:faction"）。</summary>
        public string OwnerKey => IsPersonOwned ? $"P:{ownerPersonId}" : $"F:{ownerFaction}";
    }
}
