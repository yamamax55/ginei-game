using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力レベルの文化・宗教・イデオロギーの合成状態（薄いオーケストレータ <see cref="CultureSynthesisTickRules"/> 用の自作 state）。
    /// per-system の <see cref="Culture"/>（#194・惑星単位の民族）とは別＝こちらは勢力全体の文化的結束と市民宗教の薄い合成。
    /// 数値の解決は <see cref="CultureRules"/>/<see cref="CivicFaithRules"/> へ委譲し、ここは器（純データ）に徹する。
    /// 純データ（非 MonoBehaviour・test-first）。<see cref="FactionState"/> の文化版・姉妹データだが本ファイルからは参照しない（独立）。
    /// </summary>
    [System.Serializable]
    public class CultureState
    {
        /// <summary>文化的結束（0＝バラバラ .. 1＝一体）。正統性・包摂で年々高まり、安定度への背景modifierの源。</summary>
        public float cohesion = 0.5f;

        /// <summary>市民宗教への信奉（0..1）＝国家への情緒的紐帯。<see cref="CivicFaithRules"/> が回す。</summary>
        public float faith = 0.5f;

        /// <summary>内面の真摯さ（0..1）＝本心からの信仰。低いと形骸化＝結束に効かない。</summary>
        public float sincerity = 0.5f;

        public CultureState() { }

        public CultureState(float cohesion, float faith, float sincerity)
        {
            this.cohesion = Mathf.Clamp01(cohesion);
            this.faith = Mathf.Clamp01(faith);
            this.sincerity = Mathf.Clamp01(sincerity);
        }
    }
}
