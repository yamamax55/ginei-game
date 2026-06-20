using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給充足→戦闘力・機動・継戦への接続（MILSUP-4・#2049・#106/ORBAT-4 連携・純ロジック）。
    /// 弾薬充足→戦闘力（ダメージ#106）／燃料充足→機動（速度）／物資充足→継戦・士気（<see cref="FleetSustainment"/> ORBAT-4 と整合）。
    /// 実効値パターン（基準非破壊）＝充足1.0で従来挙動。会戦/戦略の双方で読む単一窓口。test-first。
    /// </summary>
    public static class MilitaryReadinessRules
    {
        /// <summary>戦闘力係数＝0.1+0.9×弾薬充足（弾切れで火力ほぼ0・満載で1.0）。#106 ダメージへ乗る。</summary>
        public static float FirepowerFactor(float ammoFulfillment)
            => 0.1f + 0.9f * Mathf.Clamp01(ammoFulfillment);

        /// <summary>機動係数＝0.1+0.9×燃料充足（燃料切れで機動ほぼ0）。速度へ乗る。</summary>
        public static float MobilityFactor(float fuelFulfillment)
            => 0.1f + 0.9f * Mathf.Clamp01(fuelFulfillment);

        /// <summary>継戦・士気係数＝0.5+0.5×物資充足（糧食切れで敗走しやすい・極端でない）。継戦#ORBAT-4/士気へ。</summary>
        public static float SustainmentFactor(float provisionFulfillment)
            => 0.5f + 0.5f * Mathf.Clamp01(provisionFulfillment);

        /// <summary>総合補給レディネス＝3カテゴリ充足の最小（最も欠けた物資が全体を律速＝リービッヒの最小律）。</summary>
        public static float OverallReadiness(float ammo, float fuel, float provision)
            => Mathf.Min(Mathf.Clamp01(ammo), Mathf.Min(Mathf.Clamp01(fuel), Mathf.Clamp01(provision)));
    }
}
