namespace Ginei
{
    /// <summary>
    /// 土地の用途（地目・#2019 惑星土地の分割）。惑星の土地は用途ごとの区画（ゾーン）に分割され、それぞれを持分で所有・取引できる
    /// ＝第N惑星の「鉱山の土地」「農地」「住宅地」…の権利。少数に絞る（タイクン化回避）。経済類型 <see cref="SystemType"/> ごとに
    /// 各用途の割合が変わる（<see cref="LandZoningRules.ZoneWeight"/>）。
    /// </summary>
    public enum LandUseType { 住宅地, 商業地, 工業地, 農地, 鉱山 }
}
