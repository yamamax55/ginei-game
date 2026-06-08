using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 内政（#109 P-1/P-2 最小ループ）の純データ：星系/惑星の統治状態。
    /// 「支配＝即・産出」ではなく <see cref="stability"/>(安定度)×<see cref="integration"/>(統合度) で
    /// 産出・徴募が変わる土台。占領直後は未統合(integration=0)で不安定→時間で統合→安定が回復する。
    /// 数値の解決は <see cref="GovernanceRules"/>(static) が唯一の窓口。
    /// 所有勢力は StarSystem.owner が出所＝ここには持たない（住民の思想 <see cref="nativeIdeology"/> のみ保持）。
    /// 建設マイクロ・通貨経済は持たない（タイクン回避・EPIC #109 方針）。
    /// </summary>
    [System.Serializable]
    public class Province
    {
        /// <summary>紐づく星系ID（StarSystem.id）。</summary>
        public int systemId;

        /// <summary>住民の思想傾向（FactionData.ideology 文字列）。占領しても即は変わらない＝不安定の源。</summary>
        public string nativeIdeology = "";

        /// <summary>人口規模（Pop。徴募・産出のスケール）。</summary>
        public float population = 100f;

        /// <summary>安定度・治安（0..100）。低いと産出減・反乱リスク。既定＝中立。</summary>
        public float stability = GovernanceRules.BaseStability;

        /// <summary>占領統合度（0＝占領直後/未統合 .. 1＝完全統合）。未統合ぶんが安定を押し下げる。</summary>
        public float integration = 1f;

        public Province() { }

        public Province(int systemId, string nativeIdeology, float population = 100f)
        {
            this.systemId = systemId;
            this.nativeIdeology = nativeIdeology ?? "";
            this.population = Mathf.Max(0f, population);
            this.stability = GovernanceRules.BaseStability;
            this.integration = 1f; // 既定は自国領＝統合済み
        }
    }
}
