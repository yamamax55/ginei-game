using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 政略結婚・血縁外交の一組（PDX-2 #647・パラドゲー式の婚姻同盟）。
    /// 二つの家(<see cref="houseA"/>/<see cref="houseB"/>)を婚姻で結び、相手の継承権への請求権
    /// 強度(<see cref="claimStrength"/>)を持つ。婚姻は同盟結束を底上げし、請求権は世代を経て減衰する。
    /// 解決は <see cref="MarriageRules"/>（static）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    [System.Serializable]
    public class MarriageAlliance
    {
        public string houseA;            // 婚姻を結ぶ家A
        public string houseB;            // 婚姻を結ぶ家B
        public float claimStrength = 0f; // 相手家の継承への請求権強度 0..1

        public MarriageAlliance() { }

        public MarriageAlliance(string houseA, string houseB, float claimStrength = 0f)
        {
            this.houseA = houseA;
            this.houseB = houseB;
            this.claimStrength = Mathf.Clamp01(claimStrength);
        }
    }
}
