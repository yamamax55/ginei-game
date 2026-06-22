using System;

namespace Ginei
{
    /// <summary>
    /// lore/<see cref="AdmiralData"/> で名前指定する人物関係の種（PER-PROPS A1）。runtime で相手の表示名を Person.id へ
    /// 解決し、<b>既存の</b> <see cref="PersonRelationGraph"/>（TKO-3 #2480）へ積む（<see cref="RelationshipSeedRules"/>）。
    /// 関係システムは再実装せず、lore→graph の橋渡しだけを担う。
    /// </summary>
    [Serializable]
    public struct RelationshipSeed
    {
        public string targetName;   // 相手の表示名（Person.name で解決）
        public RelationKind kind;    // 既存の RelationKind（上官/部下/同期/師弟/恩義/遺恨/親愛）
        public int intensity;        // 0..100（graph の strength 0..1 へ /100 で変換）
    }
}
