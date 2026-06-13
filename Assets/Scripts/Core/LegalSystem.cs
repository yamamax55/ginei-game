using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 法体系の状態（LAW-1・#2126・純データ）。法の支配の4要素（各0..1）。
    /// 立憲主義#170（制約権力）・秘密警察#166（抑圧）とは別軸＝法そのものの統治品質。
    /// </summary>
    public class LegalSystem
    {
        public float judicialIndependence; // 司法の独立（行政から独立した裁き）
        public float equalityBeforeLaw;    // 法の前の平等（権力者も庶民も同じ法）
        public float powerConstraint;      // 権力の制約（統治者も法に従う＝法の支配の核）
        public float legalPredictability;  // 法の予測可能性（恣意でなく安定した法）

        public LegalSystem() { }

        public LegalSystem(float judicialIndependence, float equalityBeforeLaw, float powerConstraint, float legalPredictability)
        {
            this.judicialIndependence = Mathf.Clamp01(judicialIndependence);
            this.equalityBeforeLaw = Mathf.Clamp01(equalityBeforeLaw);
            this.powerConstraint = Mathf.Clamp01(powerConstraint);
            this.legalPredictability = Mathf.Clamp01(legalPredictability);
        }
    }

    /// <summary>
    /// 法の支配の純ロジック（LAW-1・#2126）。4要素の合成指数と、法治（rule by law）との区別を扱う。
    /// 法治＝法による統治（権力の道具）、法の支配＝権力も法に従う。<b>権力制約が低いと法治どまり</b>。test-first。
    /// </summary>
    public static class RuleOfLawRules
    {
        public const float RuleByLawThreshold = 0.4f; // 権力制約がこれ未満なら法治どまり

        /// <summary>法の支配指数＝4要素の平均（0..1）。</summary>
        public static float RuleOfLawIndex(LegalSystem s)
        {
            if (s == null) return 0f;
            return (Mathf.Clamp01(s.judicialIndependence) + Mathf.Clamp01(s.equalityBeforeLaw)
                  + Mathf.Clamp01(s.powerConstraint) + Mathf.Clamp01(s.legalPredictability)) / 4f;
        }

        /// <summary>法治どまりか＝権力制約が閾値未満（法はあるが権力を縛らない＝権力の道具）。</summary>
        public static bool IsRuleByLawOnly(LegalSystem s, float threshold = RuleByLawThreshold)
            => s != null && s.powerConstraint < threshold;
    }
}
