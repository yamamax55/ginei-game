using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 寡頭支配と実権（#164）の一権力者。形式上の地位（<see cref="formalRank"/>＝階級 tier #14）と
    /// 非公式の影響力（<see cref="informalInfluence"/>＝派閥・恩顧・私兵）・政治的策謀（<see cref="intrigue"/>）を
    /// 分けて持つ＝「肩書は高いが実権の無い傀儡」「肩書は低いが実権を握る黒幕（影の支配者）」を表す核。
    /// 実権の合成・傀儡/黒幕の判定は <see cref="PowerRules"/>（static）が唯一の窓口。基準値非破壊。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class PowerActor
    {
        /// <summary>形式上の地位＝階級 tier（#14。大きいほど上位の肩書）。</summary>
        public int formalRank;

        /// <summary>非公式の影響力 0..1（派閥・恩顧・私兵＝肩書に表れない実力）。</summary>
        public float informalInfluence;

        /// <summary>政治的策謀の浸透 0..1（#819 と同型。根回し・調略で実権を増幅する）。</summary>
        public float intrigue;

        public PowerActor() { }

        public PowerActor(int formalRank, float informalInfluence = 0f, float intrigue = 0f)
        {
            this.formalRank = Mathf.Max(0, formalRank);
            this.informalInfluence = Mathf.Clamp01(informalInfluence);
            this.intrigue = Mathf.Clamp01(intrigue);
        }
    }
}
