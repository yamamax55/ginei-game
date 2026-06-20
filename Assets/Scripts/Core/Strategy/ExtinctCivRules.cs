using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 先行文明の遺産解決（#2399 IHER-3・純ロジック test-first）。唯一の窓口。
    /// 記録(<see cref="ExtinctCivilization"/>)から、後継勢力の研究産出倍率(<see cref="TechHeritage"/>)・
    /// 遺跡の地勢(<see cref="GeoFootprint"/>＝現マップに残る星系)・滅亡原因による気質補正(<see cref="ExtinctionCauseEffect"/>)を導く。
    /// すべて倍率を返すだけで基準値は破壊しない（実効値パターン）。null/空の記録は中立（1.0／空列挙）＝後方互換。
    /// 調整値は public const に集約（マジックナンバー禁止）。
    /// </summary>
    public static class ExtinctCivRules
    {
        /// <summary>中立倍率（記録なし・原因不明）。</summary>
        public const float NeutralFactor = 1f;
        /// <summary>技術段階1あたりの研究産出ボーナス（高文明ほど後継への遺産が大きい）。</summary>
        public const float TechHeritagePerTier = 0.05f;
        /// <summary>遺産が最も活きる研究分野へのわずかな上乗せ（情報＝失われた知の解読）。</summary>
        public const float TechHeritageFieldBias = 0.02f;
        /// <summary>戦争で滅んだ文明の遺産＝軍事偏重（やや好戦的）。</summary>
        public const float WarCauseFactor = 1.1f;
        /// <summary>環境崩壊で滅んだ文明の遺産＝慎重（やや抑制的）。</summary>
        public const float EnvironmentCauseFactor = 0.95f;

        /// <summary>
        /// 後継勢力の研究産出倍率。peakTechTier が高いほど大きい（単調増加）。
        /// 遺産が最も活きる分野（情報）にはわずかな上乗せ。null は中立 1.0。
        /// </summary>
        public static float TechHeritage(ExtinctCivilization ec, ResearchField field)
        {
            if (ec == null) return NeutralFactor;
            int tier = Mathf.Max(0, ec.peakTechTier);
            float factor = NeutralFactor + tier * TechHeritagePerTier;
            if (field == ResearchField.情報) factor += tier * TechHeritageFieldBias;
            return factor;
        }

        /// <summary>
        /// 遺跡の地勢＝記録の版図のうち現銀河マップに残る星系を返す（null はスキップ）。
        /// ec/map が null、または該当星系がなければ空。
        /// </summary>
        public static IEnumerable<StarSystem> GeoFootprint(ExtinctCivilization ec, GalaxyMap map)
        {
            if (ec == null || map == null || ec.territorySystemIds == null) yield break;
            for (int i = 0; i < ec.territorySystemIds.Count; i++)
            {
                StarSystem s = map.GetSystem(ec.territorySystemIds[i]);
                if (s != null) yield return s;
            }
        }

        /// <summary>
        /// 滅亡原因による後継勢力の気質補正倍率（1.0 近傍）。
        /// 戦争＝軍事偏重／環境＝慎重／不明＝中立。ec が null は中立 1.0。
        /// faction は将来の勢力別補正用（現状未使用＝中立）。
        /// </summary>
        public static float ExtinctionCauseEffect(ExtinctCivilization ec, FactionData faction)
        {
            if (ec == null) return NeutralFactor;
            switch (ec.extinctionCause)
            {
                case ExtinctionCause.戦争: return WarCauseFactor;
                case ExtinctionCause.環境: return EnvironmentCauseFactor;
                default:                   return NeutralFactor;
            }
        }
    }
}
