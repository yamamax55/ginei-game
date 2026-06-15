using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 希少資源（戦略資源 #178）の純ロジック（唯一の窓口）。各惑星が<b>鉱床（偏在）</b>を持つときだけ、希少度に応じた率で
    /// 特定の希少資源を産出する（基本資源 #93 とは別レイヤー）。希少資源は<b>用途</b>（先進艦/改造/研究/特殊兵器）の<b>ゲート</b>に使う。
    /// 種類は<b>少数に絞る</b>（タイクン回避）。産出効率は内政の安定度比例（<see cref="GovernanceRules.OutputFactor"/>）を掛ける。test-first。
    /// 交易依存・通商破壊・物流（#94/#95/#177）は #178 後段でここを土台に足す。
    /// </summary>
    public static class StrategicResourceRules
    {
        /// <summary>全希少資源（列挙用）。</summary>
        public static readonly StrategicResourceType[] All =
        {
            StrategicResourceType.レアメタル, StrategicResourceType.反応物質,
            StrategicResourceType.超伝導体, StrategicResourceType.希少結晶,
        };

        /// <summary>希少資源の定義（種類×用途×希少度×基本産出率）の一表（#178・唯一の出所＝二重定義しない）。</summary>
        public static StrategicResourceInfo Info(StrategicResourceType type)
        {
            switch (type)
            {
                case StrategicResourceType.反応物質:
                    return new StrategicResourceInfo(type, StrategicResourceUse.特殊兵器, 0.6f, 0.35f,
                        "反応物質", "高効率燃料・エネルギー兵器の素＝特殊兵器をゲート");
                case StrategicResourceType.超伝導体:
                    return new StrategicResourceInfo(type, StrategicResourceUse.研究, 0.7f, 0.30f,
                        "超伝導体", "電子戦・センサー・研究をゲート");
                case StrategicResourceType.希少結晶:
                    return new StrategicResourceInfo(type, StrategicResourceUse.改造, 0.8f, 0.25f,
                        "希少結晶", "ビーム増幅・艦艇改造をゲート");
                case StrategicResourceType.レアメタル:
                default:
                    return new StrategicResourceInfo(StrategicResourceType.レアメタル, StrategicResourceUse.建艦, 0.4f, 0.50f,
                        "レアメタル", "特殊合金・装甲＝先進艦の建艦をゲート");
            }
        }

        /// <summary>その希少資源の希少度（0..1・高いほど稀）。</summary>
        public static float Rarity(StrategicResourceType type) => Info(type).rarity;

        /// <summary>その希少資源がゲートする用途。</summary>
        public static StrategicResourceUse UseOf(StrategicResourceType type) => Info(type).use;

        /// <summary>
        /// 惑星の希少資源の<b>実効産出率</b>（/戦略秒）。鉱床なし＝0。鉱床あり＝基本率×豊富さ×安定度比例。
        /// 表示・見積り用（備蓄を変えない）。
        /// </summary>
        public static float ProvinceRate(Province planet)
        {
            if (planet == null || !planet.hasStrategicResource) return 0f;
            return Info(planet.strategicResource).baseRate
                 * Mathf.Clamp01(planet.strategicAbundance)
                 * GovernanceRules.OutputFactor(planet);
        }

        /// <summary>惑星（鉱床あり）の希少資源産出を備蓄へ加える（偏在＝鉱床のある惑星だけ出る）。</summary>
        public static void ProduceFromProvince(StrategicResourceStockpile into, Province planet, float dt)
        {
            if (into == null || planet == null || !planet.hasStrategicResource || dt <= 0f) return;
            float r = ProvinceRate(planet);
            if (r > 0f) into.Add(planet.strategicResource, r * dt);
        }

        /// <summary>星系（惑星群）の希少資源産出を備蓄へ合算（#767 集約＝鉱床のある惑星のみ寄与）。</summary>
        public static void ProduceFromSystem(StrategicResourceStockpile into, IReadOnlyList<Province> planets, float dt)
        {
            if (into == null || planets == null || dt <= 0f) return;
            for (int i = 0; i < planets.Count; i++) ProduceFromProvince(into, planets[i], dt);
        }

        /// <summary>用途のゲート判定（#178）：その希少資源を cost ぶん賄えるか（建艦/改造/研究/特殊兵器の前提）。</summary>
        public static bool CanAfford(StrategicResourceStockpile stock, StrategicResourceType type, float cost)
            => stock != null && stock.Get(type) >= cost;
    }
}
