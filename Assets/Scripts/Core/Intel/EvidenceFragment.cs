using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 証拠断片（IHER-1 #2397『星を継ぐもの』）。ある主題(topic)について、特定の研究分野
    /// (<see cref="ResearchField"/>) から独立に得られた一片の手がかり。信頼度 credibility(0..1 目安) と
    /// 重み weight(その断片の証拠としての比重) を持つ。断片は単体では弱いが、異なる分野で収束すると
    /// 実効信頼性が跳ね上がる（<see cref="EvidenceRules"/>）。純データ（非 MonoBehaviour・直列化可）。
    /// </summary>
    [System.Serializable]
    public class EvidenceFragment
    {
        /// <summary>主題（何についての証拠か）。</summary>
        public string topic;
        /// <summary>この断片の出所となった研究分野（<see cref="ResearchField"/> を再利用）。</summary>
        public ResearchField sourceField;
        /// <summary>断片単体の信頼度（0..1 目安・負はルール側で0扱い）。</summary>
        public float credibility;
        /// <summary>証拠としての比重（重要度。1.0 を基準に増減）。</summary>
        public float weight;

        public EvidenceFragment(string topic, ResearchField sourceField, float credibility, float weight)
        {
            this.topic = topic;
            this.sourceField = sourceField;
            this.credibility = credibility;
            this.weight = weight;
        }
    }
}
