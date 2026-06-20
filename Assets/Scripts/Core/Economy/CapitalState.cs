using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 資本状態の純データ（ピケティ r>g・#917）。資本収益率 r が成長率 g を上回ると富が資本側へ集中し続ける、を最小モデル化する。
    /// 解決は <see cref="CapitalRules"/> が唯一の窓口（基準値非破壊）。純データ・直列化可。
    /// </summary>
    [System.Serializable]
    public class CapitalState
    {
        [Tooltip("資本収益率 r（年率・0..1 想定）")]
        public float capitalReturn = 0.05f;

        [Tooltip("経済成長率 g（年率・0..1 想定）")]
        public float growthRate = 0.02f;

        [Tooltip("富の集中度（0..1・1=極端な寡占）")]
        public float wealthConcentration = 0.4f;
    }
}
