using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 諜報網の純データ（諜報 ESP・対象勢力/星系に紐づく自勢力の浸透状態）。
    /// 潜入度 <see cref="infiltration"/>(0..1) が高いほど情報取得・破壊工作が効き、対象の防諜
    /// <see cref="counterIntel"/>(0..1) が高いほど任務は阻まれ露見しやすい。数値の解決は
    /// <see cref="EspionageRules"/>(static) が唯一の窓口（基準フィールドは非破壊）。
    /// </summary>
    [System.Serializable]
    public class SpyNetwork
    {
        /// <summary>潜入度（0＝未浸透 .. 1＝深く浸透）。情報取得・工作の源。</summary>
        public float infiltration = 0f;

        /// <summary>防諜（0＝無防備 .. 1＝厳重）。任務阻止・露見リスクの源（対象側の能力を写したもの）。</summary>
        public float counterIntel = 0f;

        public SpyNetwork() { }

        public SpyNetwork(float infiltration, float counterIntel = 0f)
        {
            this.infiltration = Mathf.Clamp01(infiltration);
            this.counterIntel = Mathf.Clamp01(counterIntel);
        }
    }
}
