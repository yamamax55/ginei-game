namespace Ginei
{
    /// <summary>
    /// 配下艦の艦種（#80）。戦艦＝高耐久・高火力・低速・大型／巡航艦＝中庸／駆逐艦＝低耐久・高速・小型。
    /// 倍率・編成比率は Squadron（Game）側で public 調整（実効値パターン）。
    /// Core の ShipyardRules/BuildOrder が参照するため単独ファイル（#496）。
    /// </summary>
    public enum ShipClass { 戦艦, 巡航艦, 駆逐艦 }
}
