using System;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 封建制・貴族制の封土（#168）。純データ。
    /// 君主が貴族（封臣）に与える領地＝封臣は<b>忠誠</b>に応じて<b>軍役（levy）</b>を供出するが、
    /// <b>自治権（autonomy）</b>が高いほど中央の統制が緩み、忠誠が下がれば反乱しうる。
    /// 数値ロジックは <see cref="FeudalRules"/> が唯一の窓口（基準値非破壊・実効値パターン）。
    /// </summary>
    [Serializable]
    public class Fief
    {
        [Tooltip("封臣の忠誠（0..1）。高いほど軍役供出が増え反乱が減る")]
        public float vassalLoyalty = 1f;

        [Tooltip("封土が抱える名目兵力（軍役の上限）")]
        public int levySize = 100;

        [Tooltip("封臣の自治権（0..1）。高いほど中央統制が緩み反乱しやすい")]
        public float autonomy = 0.5f;

        public Fief() { }

        public Fief(float vassalLoyalty, int levySize, float autonomy)
        {
            this.vassalLoyalty = vassalLoyalty;
            this.levySize = levySize;
            this.autonomy = autonomy;
        }
    }
}
