using UnityEngine;

namespace Ginei
{
    /// <summary>非暴力運動の調整係数（#831/#832/#836）。</summary>
    public readonly struct NonviolenceParams
    {
        /// <summary>弾圧→支持転換の係数（道徳の柔術の強さ）。</summary>
        public readonly float jiujitsuCoef;
        /// <summary>支持がこれ以上で勝利（統治者が譲歩）。</summary>
        public readonly float triumphThreshold;

        public NonviolenceParams(float jiujitsuCoef, float triumphThreshold)
        {
            this.jiujitsuCoef = jiujitsuCoef;
            this.triumphThreshold = triumphThreshold;
        }

        public static NonviolenceParams Default => new NonviolenceParams(0.5f, 0.6f);
    }

    /// <summary>
    /// 非暴力＝道徳の柔術の純ロジック（公民権 #831/#832・ガンジー #836）。平和的な運動への弾圧は、
    /// 可視化（メディア）されると<b>沈黙の多数を支持へ転換する</b>＝敵の力（暴力）を敵に向ける柔術
    /// （バーミングハム/セルマ＝"見られて勝つ"）。支持が高いほど統治者の協力(<see cref="ConsentRules"/>)を
    /// 引き上げ（非協力）、閾値突破で戦わずに勝つ。情報戦（#819 家康の手紙）の反転＝被害が武器。test-first。
    /// </summary>
    public static class NonviolenceRules
    {
        /// <summary>
        /// 弾圧（道徳の柔術）：brutality（残虐さ）×mediaReach（可視化）×commitment（献身する少数）に比例して
        /// 支持が上がる＝敵の暴力が運動を強くする。可視化されない弾圧（mediaReach=0）は転換しない。上昇量を返す。
        /// </summary>
        public static float Repress(Movement m, float brutality, float mediaReach, NonviolenceParams p)
        {
            if (m == null) return 0f;
            float shift = Mathf.Clamp01(brutality) * Mathf.Clamp01(mediaReach) * Mathf.Clamp01(m.commitment) * p.jiujitsuCoef;
            m.support = Mathf.Clamp01(m.support + shift);
            return shift;
        }

        public static float Repress(Movement m, float brutality, float mediaReach)
            => Repress(m, brutality, mediaReach, NonviolenceParams.Default);

        /// <summary>運動が勝利したか（支持が閾値突破＝統治者が譲歩せざるを得ない）。</summary>
        public static bool IsTriumphant(Movement m, NonviolenceParams p) => m != null && m.support >= p.triumphThreshold;
        public static bool IsTriumphant(Movement m) => IsTriumphant(m, NonviolenceParams.Default);

        /// <summary>
        /// 運動の支持を統治体への非協力として波及させる（ガンジー：支持が高いほど協力を引き上げる）。
        /// polity.cooperation を support×amount だけ引き下げる（戦わずに統治不能へ近づける）。
        /// </summary>
        public static void PressurePolity(Movement m, Polity polity, float amount)
        {
            if (m == null || polity == null) return;
            ConsentRules.Withdraw(polity, Mathf.Clamp01(m.support) * Mathf.Abs(amount));
        }
    }
}
