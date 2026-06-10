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
    }
}
