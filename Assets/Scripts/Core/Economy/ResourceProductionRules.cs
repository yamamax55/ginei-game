using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資源生産の純ロジック（L-1 #93・唯一の窓口）。支配星系が<b>類型</b>（工業/農業/鉱業/居住）に応じて
    /// 物資/弾薬/燃料を時間で産出する（建設マイクロ無し＝類型で決まる）。産出効率は内政の安定度比例
    /// （<see cref="GovernanceRules.OutputFactor"/>＝支配≠即産出）を係数で掛ける。タイクン化回避＝多段生産ツリーは持たない。test-first。
    /// </summary>
    public static class ResourceProductionRules
    {
        // 類型ごとの産出率（/戦略秒・マジックナンバー禁止＝const に集約）。
        public const float IndustryAmmo = 4f;       // 工業＝弾薬主
        public const float IndustrySupplies = 2f;   //   ＋物資
        public const float AgricultureSupplies = 5f; // 農業＝食料（物資）
        public const float MiningFuel = 4f;          // 鉱業＝燃料
        public const float HabitationSupplies = 1f;  // 居住＝少量物資

        /// <summary>類型×資源の産出率（/戦略秒）。該当しない組み合わせは0。</summary>
        public static float Rate(SystemType type, ResourceType res)
        {
            switch (type)
            {
                case SystemType.工業:
                    if (res == ResourceType.弾薬) return IndustryAmmo;
                    if (res == ResourceType.物資) return IndustrySupplies;
                    return 0f;
                case SystemType.農業:
                    return res == ResourceType.物資 ? AgricultureSupplies : 0f;
                case SystemType.鉱業:
                    return res == ResourceType.燃料 ? MiningFuel : 0f;
                case SystemType.居住:
                default:
                    return res == ResourceType.物資 ? HabitationSupplies : 0f;
            }
        }

        /// <summary>
        /// 星系の類型に応じた産出を備蓄へ加える（factor＝産出効率＝安定度比例 <see cref="GovernanceRules.OutputFactor"/> 等）。
        /// </summary>
        public static void Produce(ResourceStockpile into, SystemType type, float factor, float dt)
        {
            if (into == null || dt <= 0f) return;
            float f = Mathf.Max(0f, factor);
            into.Add(ResourceType.物資, Rate(type, ResourceType.物資) * f * dt);
            into.Add(ResourceType.弾薬, Rate(type, ResourceType.弾薬) * f * dt);
            into.Add(ResourceType.燃料, Rate(type, ResourceType.燃料) * f * dt);
        }

        // ===== 惑星単位の産出（#767 ハイブリッド＝惑星が産出の単一の真実・星系はその集約）=====

        /// <summary>
        /// <b>惑星</b>（<see cref="Province"/>）単位の産出を備蓄へ加える。類型は <see cref="Province.systemType"/>、
        /// 効率は <see cref="GovernanceRules.OutputFactor"/>（安定度比例＝支配≠即産出）。各惑星が自分の地味で資源を出す。
        /// </summary>
        public static void ProduceFromProvince(ResourceStockpile into, Province planet, float dt)
        {
            if (into == null || planet == null || dt <= 0f) return;
            Produce(into, planet.systemType, GovernanceRules.OutputFactor(planet), dt);
        }

        /// <summary>星系（惑星群）の産出を備蓄へ加える＝各惑星が自分の類型×統治で産出し合算（#767 集約・星系＝惑星の集約ビュー）。</summary>
        public static void ProduceFromSystem(ResourceStockpile into, IReadOnlyList<Province> planets, float dt)
        {
            if (into == null || planets == null || dt <= 0f) return;
            for (int i = 0; i < planets.Count; i++) ProduceFromProvince(into, planets[i], dt);
        }

        /// <summary>
        /// その惑星の資源の<b>実効産出率</b>（/戦略秒＝類型率×安定度比例の産出倍率）。表示・見積り用（備蓄を変えない）。
        /// </summary>
        public static float ProvinceRate(Province planet, ResourceType res)
            => planet == null ? 0f : Rate(planet.systemType, res) * GovernanceRules.OutputFactor(planet);
    }
}
