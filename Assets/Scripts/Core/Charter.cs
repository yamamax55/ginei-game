using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// マグナカルタ＝王権を縛る成文契約（#624 王権制約）の純データ。
    /// 「権力は無制限ではなく、契約で制約される」を表す：課税同意・適正手続き・抵抗権の3条項と、
    /// それがどれだけ慣習法（不文の常識）として定着しているか（<see cref="strength"/> 0..1）。
    /// 数値の解決は <see cref="MagnaCartaRules"/>(static) が唯一の窓口。建設マイクロは持たない（純データ）。
    /// </summary>
    [System.Serializable]
    public class Charter
    {
        /// <summary>課税同意条項（議会/諸侯の同意なく課税できない）。</summary>
        public bool taxConsent;

        /// <summary>適正手続き条項（法によらず自由・財産を奪われない＝デュープロセス）。</summary>
        public bool dueProcess;

        /// <summary>抵抗権条項（王が契約を破れば臣下は抵抗してよい）。</summary>
        public bool resistanceRight;

        /// <summary>慣習法化度（0＝紙の上だけ .. 1＝不文の常識として定着）。条項の実効を左右する。</summary>
        public float strength = 0f;

        public Charter() { }

        public Charter(bool taxConsent, bool dueProcess, bool resistanceRight, float strength = 0f)
        {
            this.taxConsent = taxConsent;
            this.dueProcess = dueProcess;
            this.resistanceRight = resistanceRight;
            this.strength = Mathf.Clamp01(strength);
        }
    }
}
