namespace Ginei
{
    /// <summary>
    /// 陣形の種類定義。すべて「旗艦＝中心(原点)、配下艦は左右対称、旗艦の向き(Transform.up=前方)に追従」。
    /// 配置の実装は Squadron（Game）。Core の AdmiralData/ScenarioData/FleetUnitData が参照するため単独ファイル（#496）。
    /// </summary>
    public enum Formation
    {
        紡錘陣, // Spindle（デフォルト）：前方にやや尖った縦長のレンズ形
        鶴翼陣, // Crescent：前方に開いた幅広の弧
        円陣,   // Circle：同心円リング
        横陣,   // Line abreast：左右一直線（1〜2段）
        方陣    // Square：格子状
    }
}
