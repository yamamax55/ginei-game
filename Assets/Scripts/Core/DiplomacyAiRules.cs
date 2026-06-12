using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 外交AIの判断（DIPLO-2・#2119・純ロジック）。関係値と国力から宣戦/講和/同盟を決める。
    /// 数値の素は DIP-1（関係）/DIP-3（講和受諾度）へ委譲し、ここは閾値の判断のみ（plain float で test 可能）。test-first。
    /// </summary>
    public static class DiplomacyAiRules
    {
        /// <summary>AI 判断の調整値。</summary>
        public struct DiploAiParams
        {
            public float warOpinionThreshold;     // これ以下の関係で開戦を検討（負値）
            public float warPowerRatio;           // 自軍/敵軍 がこれ以上なら開戦（優位）
            public float allyOpinionThreshold;    // これ以上の関係で同盟を提案
            public float peaceAcceptanceThreshold; // これ以上の講和受諾度で講和

            public DiploAiParams(float warOpinionThreshold, float warPowerRatio, float allyOpinionThreshold, float peaceAcceptanceThreshold)
            {
                this.warOpinionThreshold = warOpinionThreshold;
                this.warPowerRatio = warPowerRatio;
                this.allyOpinionThreshold = allyOpinionThreshold;
                this.peaceAcceptanceThreshold = peaceAcceptanceThreshold;
            }

            /// <summary>既定＝開戦関係−60/国力比1.1/同盟関係50/講和受諾0.6。</summary>
            public static DiploAiParams Default => new DiploAiParams(-60f, 1.1f, 50f, 0.6f);
        }

        /// <summary>開戦すべきか＝関係が低く（≤閾値）かつ国力優位（自/敵≥比）。</summary>
        public static bool ShouldDeclareWar(float opinion, float ownStrength, float theirStrength, DiploAiParams p)
            => opinion <= p.warOpinionThreshold && ownStrength >= Mathf.Max(0f, theirStrength) * p.warPowerRatio;

        /// <summary>同盟を提案すべきか＝関係が高い（≥閾値）。</summary>
        public static bool ShouldProposeAlliance(float opinion, DiploAiParams p)
            => opinion >= p.allyOpinionThreshold;

        /// <summary>講和すべきか＝講和受諾度（DIP-3）が閾値以上。</summary>
        public static bool ShouldMakePeace(float peaceAcceptance, DiploAiParams p)
            => peaceAcceptance >= p.peaceAcceptanceThreshold;

        /// <summary>開戦事由の選択（険悪なほど苛烈＝征服／懲罰／従属）。</summary>
        public static CasusBelli ChooseCasusBelli(float opinion)
        {
            if (opinion <= -80f) return CasusBelli.征服;
            if (opinion <= -50f) return CasusBelli.懲罰;
            return CasusBelli.従属;
        }
    }
}
