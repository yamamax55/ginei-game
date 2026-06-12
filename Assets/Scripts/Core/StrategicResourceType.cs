namespace Ginei
{
    /// <summary>
    /// 希少資源（戦略資源）の種類（#178・<b>少数に絞る＝タイクン回避</b>）。
    /// 基本資源（<see cref="ResourceType"/>＝物資/弾薬/燃料・#93）とは<b>別レイヤー</b>＝惑星ごとに<b>偏在</b>し、
    /// 先進艦・改造・研究・特殊兵器の<b>ゲート</b>になる（「資源も地理である」）。解決は <see cref="StrategicResourceRules"/>。
    /// </summary>
    public enum StrategicResourceType { レアメタル, 反応物質, 超伝導体, 希少結晶 }

    /// <summary>希少資源の用途（何をゲートするか・#178）。先進艦/改造/研究/特殊兵器のいずれかを要求する。</summary>
    public enum StrategicResourceUse { 建艦, 改造, 研究, 特殊兵器 }

    /// <summary>
    /// 希少資源の定義（種類・用途・希少度・基本産出率）＝<b>StrategicResourceData（#178）の純データ版</b>（read-only 値型）。
    /// 出所は <see cref="StrategicResourceRules.Info"/>（一表＝二重定義しない）。
    /// </summary>
    public readonly struct StrategicResourceInfo
    {
        public readonly StrategicResourceType type;
        public readonly StrategicResourceUse use;     // 用途（ゲート対象）
        public readonly float rarity;                 // 希少度（0..1・高いほど稀＝産出少・鉱床少）
        public readonly float baseRate;               // 鉱床1・安定MAX・abundance1 での産出率（/戦略秒）
        public readonly string displayName;
        public readonly string description;

        public StrategicResourceInfo(StrategicResourceType type, StrategicResourceUse use,
            float rarity, float baseRate, string displayName, string description)
        {
            this.type = type;
            this.use = use;
            this.rarity = rarity;
            this.baseRate = baseRate;
            this.displayName = displayName;
            this.description = description;
        }
    }
}
