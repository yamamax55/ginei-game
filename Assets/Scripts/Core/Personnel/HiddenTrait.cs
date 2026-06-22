using System;

namespace Ginei
{
    /// <summary>
    /// 隠し特性（PER-PROPS C10）。出生の秘密・敵への内通・持病など、観測者の情報能力が高いほど露見する。
    /// concealment＝秘匿度(0..100・高いほど隠れる)。露見判定は <see cref="HiddenTraitRules"/>。
    /// </summary>
    [Serializable]
    public struct HiddenTrait
    {
        public string label;     // 例：「出生の秘密」「敵への内通」「持病」
        public int concealment;  // 0..100（高いほど露見しにくい）

        public HiddenTrait(string label, int concealment)
        {
            this.label = label;
            this.concealment = concealment;
        }
    }
}
