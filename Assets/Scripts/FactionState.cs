using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勢力の国家状態＝社会・政治シミュ層の合成（本線の統治モデル）。王朝(<see cref="regime"/>・天命/腐敗)、
    /// 統治体(<see cref="polity"/>・合意)、組織(<see cref="organization"/>・結束)、共同体(<see cref="community"/>・希望)を
    /// 1つに束ね、統治スタイル(<see cref="inclusiveness"/>・収奪0↔包摂1)が抑圧→合意→希望へ連鎖する。
    /// 解決は <see cref="FactionStateRules"/>（static）。per-system の `Province`(内政)とは別＝勢力レベルの合成。
    /// 純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public class FactionState
    {
        public Faction faction;
        public Regime regime;
        public Polity polity;
        public Organization organization;
        public Community community;

        /// <summary>統治スタイル 0..1（0=収奪的＝抑圧高・即効だが崩れる／1=包摂的＝抑圧低・遅いが安定・GEO-2 #843）。</summary>
        public float inclusiveness = 0.5f;

        public FactionState() { }

        public FactionState(Faction faction, float inclusiveness = 0.5f)
        {
            this.faction = faction;
            this.inclusiveness = Mathf.Clamp01(inclusiveness);
            regime = new Regime(0, faction);
            polity = new Polity(0, faction, population: 1000000, rulerForce: 10000);
            organization = new Organization(0, faction);
            community = new Community(0);
        }
    }
}
