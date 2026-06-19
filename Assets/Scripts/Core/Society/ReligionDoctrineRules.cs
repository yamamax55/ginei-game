using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 教義→実効係数の写像の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。
    /// 各倍率は「特性0で min、特性1で max」へ線形に効く（GovernanceRules の係数作法に倣う）。
    /// </summary>
    public readonly struct ReligionDoctrineParams
    {
        /// <summary>布教熱の改宗圧力倍率の下限（proselytism=0）。</summary>
        public readonly float conversionMin;
        /// <summary>布教熱の改宗圧力倍率の上限（proselytism=1）。</summary>
        public readonly float conversionMax;
        /// <summary>戦闘性の聖戦圧力倍率の下限（militancy=0）。</summary>
        public readonly float holyWarMin;
        /// <summary>戦闘性の聖戦圧力倍率の上限（militancy=1）。</summary>
        public readonly float holyWarMax;
        /// <summary>結束の社会効果倍率の下限（socialCohesion=0）。</summary>
        public readonly float cohesionMin;
        /// <summary>結束の社会効果倍率の上限（socialCohesion=1）。</summary>
        public readonly float cohesionMax;

        public ReligionDoctrineParams(float conversionMin, float conversionMax,
            float holyWarMin, float holyWarMax, float cohesionMin, float cohesionMax)
        {
            this.conversionMin = conversionMin;
            this.conversionMax = conversionMax;
            this.holyWarMin = holyWarMin;
            this.holyWarMax = holyWarMax;
            this.cohesionMin = cohesionMin;
            this.cohesionMax = cohesionMax;
        }

        /// <summary>既定の調整値。布教熱は改宗を最大1.5倍、戦闘性は聖戦を最大1.5倍、結束は社会効果を最大1.2倍。</summary>
        public static ReligionDoctrineParams Default => new ReligionDoctrineParams(
            conversionMin: 0.5f, conversionMax: 1.5f,
            holyWarMin: 0.5f, holyWarMax: 1.5f,
            cohesionMin: 0.85f, cohesionMax: 1.2f);
    }

    /// <summary>
    /// 教義特性→実効係数の写像（#172-175・カタログ駆動の橋渡し層・純ロジック test-first）。
    /// <see cref="ReligionDef"/> の特性を「既存の窓口へ渡す入力倍率」へ写すだけ＝数式の核（改宗圧力/聖戦圧力/社会効果）は
    /// <see cref="ReligionRules"/> へ委譲し**ここでは二重実装しない**。実効値パターン（基準を上書きしない倍率を返す）。
    /// Game層非依存＝Core 純ロジック。null（フォールバック None）も安全に中立寄りの値を返す。
    /// </summary>
    public static class ReligionDoctrineRules
    {
        /// <summary>布教熱→改宗圧力倍率（proselytism=0 で conversionMin、=1 で conversionMax）。実効値パターン。</summary>
        public static float ConversionFactor(ReligionDef def, ReligionDoctrineParams p)
            => Mathf.Lerp(p.conversionMin, p.conversionMax, def.proselytism);

        /// <summary>戦闘性→聖戦圧力倍率（militancy=0 で holyWarMin、=1 で holyWarMax）。実効値パターン。</summary>
        public static float HolyWarFactor(ReligionDef def, ReligionDoctrineParams p)
            => Mathf.Lerp(p.holyWarMin, p.holyWarMax, def.militancy);

        /// <summary>結束→社会効果倍率（socialCohesion=0 で cohesionMin、=1 で cohesionMax）。実効値パターン。</summary>
        public static float CohesionFactor(ReligionDef def, ReligionDoctrineParams p)
            => Mathf.Lerp(p.cohesionMin, p.cohesionMax, def.socialCohesion);

        /// <summary>
        /// 教義込みの改宗圧力(0..1)：<see cref="ReligionRules.ConversionPressure"/>（核）の結果に布教熱倍率を掛けて
        /// clamp する。布教熱の高い宗教ほど住民を支配信仰へ寄せやすい＝核の数式は再実装しない。
        /// </summary>
        public static float DoctrinalConversionPressure(float localFaith, float rulerFaith, bool affinityMatch,
            ReligionDef rulerDef, ReligionParams rp, ReligionDoctrineParams dp)
        {
            float basePressure = ReligionRules.ConversionPressure(localFaith, rulerFaith, affinityMatch, rp);
            return Mathf.Clamp01(basePressure * ConversionFactor(rulerDef, dp));
        }

        /// <summary>
        /// 教義込みの聖戦圧力(0..1)：<see cref="ReligionRules.HolyWarPressure"/>（核）の結果に**両宗教の戦闘性倍率の幾何平均**を
        /// 掛けて clamp する。双方が好戦的なほど対立が激しい＝核の数式は再実装しない。
        /// </summary>
        public static float DoctrinalHolyWarPressure(float faithA, float faithB, bool holySiteContested,
            ReligionDef defA, ReligionDef defB, ReligionParams rp, ReligionDoctrineParams dp)
        {
            float basePressure = ReligionRules.HolyWarPressure(faithA, faithB, holySiteContested, rp);
            float fa = HolyWarFactor(defA, dp);
            float fb = HolyWarFactor(defB, dp);
            float mutual = Mathf.Sqrt(Mathf.Max(0f, fa * fb)); // 一方が穏健なら抑制（どちらか低いと崩れる幾何平均）
            return Mathf.Clamp01(basePressure * mutual);
        }

        /// <summary>
        /// 教義込みの社会効果係数：<see cref="ReligionRules.SocialEffect"/>（核＝devotion 依存）に結束倍率を掛ける。
        /// 結束の強い宗教ほど安定/士気を底上げ＝核の数式は再実装しない。null（無信仰）は核の socialBase に倍率。
        /// </summary>
        public static float DoctrinalSocialEffect(Religion r, ReligionDef def, ReligionParams rp, ReligionDoctrineParams dp)
        {
            float baseEffect = ReligionRules.SocialEffect(r, rp);
            return baseEffect * CohesionFactor(def, dp);
        }
    }
}
