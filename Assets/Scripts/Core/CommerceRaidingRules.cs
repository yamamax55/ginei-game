using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 通商破壊の純ロジック（L-3 #95・唯一の窓口）。戦闘と兵站の融合＝艦隊が回廊で敵の輸送船団（非戦闘艦 #128）を
    /// 迎撃し、撃破で敵前線を枯渇させる（<see cref="SupplyRules.TickFront"/> の補給が届かない）。護衛を付ければ守れる。
    /// 迎撃の解決はここに集約（ズームインの小戦闘 or 自動解決 C-8 の数値モデル）。混成禁止 #883＝護衛は別部隊で随伴。test-first。
    /// </summary>
    public static class CommerceRaidingRules
    {
        /// <summary>
        /// 船団が撃破されるか＝襲撃側戦力が護衛戦力＋船団の自衛を上回るか。護衛が十分なら守り切る。
        /// </summary>
        public static bool ConvoyDestroyed(float raiderStrength, float escortStrength, float convoySelfDefense = 0f)
        {
            float defense = Mathf.Max(0f, escortStrength) + Mathf.Max(0f, convoySelfDefense);
            return Mathf.Max(0f, raiderStrength) > defense;
        }

        /// <summary>襲撃を退ける（船団を守り切る）のに必要な最小護衛戦力。</summary>
        public static float EscortNeeded(float raiderStrength, float convoySelfDefense = 0f)
            => Mathf.Max(0f, raiderStrength - Mathf.Max(0f, convoySelfDefense));

        /// <summary>前線へ実際に届く補給量（撃破されれば0＝届かない＝敵が干上がる）。</summary>
        public static float DeliveredSupply(float convoyPayload, bool destroyed)
            => destroyed ? 0f : Mathf.Max(0f, convoyPayload);

        /// <summary>
        /// 迎撃を解決し、前線備蓄へ補給を届ける（撃破なら届かない＝補給切れ）。届いた量を返す。
        /// 補給は全資源一律で簡約（<see cref="ResourceStockpile.AddAll"/>）。
        /// </summary>
        public static float ResolveInterception(ResourceStockpile frontStock, float convoyPayload,
            float raiderStrength, float escortStrength, float convoySelfDefense = 0f)
        {
            bool destroyed = ConvoyDestroyed(raiderStrength, escortStrength, convoySelfDefense);
            float delivered = DeliveredSupply(convoyPayload, destroyed);
            if (frontStock != null && delivered > 0f) frontStock.AddAll(delivered);
            return delivered;
        }
    }
}
