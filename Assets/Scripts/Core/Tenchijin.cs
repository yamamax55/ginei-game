using UnityEngine;

namespace Ginei
{
    /// <summary>天地人の三要素（孟子「天時不如地利、地利不如人和」）。天＝天の時、地＝地の利、人＝人の和。</summary>
    public enum TenchijinFactor { 天, 地, 人 }

    /// <summary>
    /// 天地人の状態（#軍神・上杉謙信型）。天の時(機・勢い)／地の利(地形・位置)／人の和(結束・忠誠)を 0..1 で持つ。
    /// 三つが<b>揃って</b>はじめて軍神が真価を発揮する＝<see cref="TenchijinRules.Alignment"/> は一つ欠けると崩れる（積）。
    /// 一時バフの三密 <see cref="FocusRules"/>(#872) と同じ「三位一体」思想だが、こちらは能力の限界突破・登場の軸。純データ。
    /// </summary>
    [System.Serializable]
    public struct Tenchijin
    {
        /// <summary>天の時（0..1・好機/勢い）。</summary>
        public float heaven;
        /// <summary>地の利（0..1・地形/位置の優位）。</summary>
        public float earth;
        /// <summary>人の和（0..1・結束/忠誠＝孟子で最重視）。</summary>
        public float person;

        public Tenchijin(float heaven, float earth, float person)
        {
            this.heaven = Mathf.Clamp01(heaven);
            this.earth = Mathf.Clamp01(earth);
            this.person = Mathf.Clamp01(person);
        }

        /// <summary>三要素すべて最大（軍神が真価を発揮する理想状態）。</summary>
        public static Tenchijin Ideal => new Tenchijin(1f, 1f, 1f);

        /// <summary>三要素ゼロ（限界突破なし＝従来動作）。</summary>
        public static Tenchijin None => new Tenchijin(0f, 0f, 0f);
    }
}
