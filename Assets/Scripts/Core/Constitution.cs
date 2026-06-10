using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 立憲主義（#170）の純データ：勢力の「憲法的拘束」の度合い。権力者の意志をどれだけ制約するか。
    /// <see cref="powerSeparation"/>(権力分立)/<see cref="rightsProtection"/>(権利保護)/<see cref="ruleOfLaw"/>(法の支配)の
    /// 各 0..1。すべて高いほど専横が効かず＝基準権力は逓減するが、権利保護は正統性ボーナスを生む。
    /// 数値の解決は <see cref="ConstitutionRules"/>(static) が唯一の窓口（基準値は非破壊）。
    /// </summary>
    [System.Serializable]
    public class Constitution
    {
        /// <summary>権力分立（0＝独裁集権 .. 1＝三権分立）。高いほど単独の権力行使を制約する。</summary>
        public float powerSeparation = 0f;

        /// <summary>権利保護（0＝無保障 .. 1＝強い人権保障）。高いほど正統性を底上げする。</summary>
        public float rightsProtection = 0f;

        /// <summary>法の支配（0＝人治 .. 1＝法治）。高いほど恣意的権力行使を制約する。</summary>
        public float ruleOfLaw = 0f;

        public Constitution() { }

        public Constitution(float powerSeparation, float rightsProtection, float ruleOfLaw)
        {
            this.powerSeparation = Mathf.Clamp01(powerSeparation);
            this.rightsProtection = Mathf.Clamp01(rightsProtection);
            this.ruleOfLaw = Mathf.Clamp01(ruleOfLaw);
        }
    }
}
