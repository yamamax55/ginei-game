using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 信念の更新と誤謬コスト（IHER-4 #2400『星を継ぐもの』）。勢力は仮説(<see cref="BeliefState"/>)に傾倒し、
    /// 確証的な反証(<see cref="EvidenceBody"/>＝<see cref="EvidenceRules.IsConclusive"/>)が来て初めて反証成立とみなす。
    /// 仮説を捨てて更新する代償は傾倒度に比例し(<see cref="UpdateCost"/>)、制度化された組織は更新を拒んで硬直する
    /// (<see cref="ShouldLock"/>)＝既存パラダイムに固執するほど真実への乗り換えが高くつく。
    /// null 安全・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BeliefRules
    {
        /// <summary>傾倒度1.0ぶんの更新コスト（仮説乗り換えの代償係数）。</summary>
        public const float CostPerCommitment = 100f;
        /// <summary>これ以上の制度化度で組織は更新を拒みロックする（パラダイムの硬直）。</summary>
        public const float LockThreshold = 0.7f;

        /// <summary>
        /// 反証成立判定。呼び出し側は仮説に反する証拠を渡す前提＝その反証が確証的
        /// (<see cref="EvidenceRules.IsConclusive"/>)なときだけ true。null は false。
        /// </summary>
        public static bool IsRefuted(BeliefState belief, EvidenceBody contraryEvidence, float threshold)
        {
            if (belief == null || contraryEvidence == null) return false;
            return EvidenceRules.IsConclusive(contraryEvidence, threshold);
        }

        /// <summary>更新コスト＝傾倒度に比例（<see cref="CostPerCommitment"/> 倍）。null は0。</summary>
        public static float UpdateCost(BeliefState belief)
        {
            if (belief == null) return 0f;
            return belief.commitment * CostPerCommitment;
        }

        /// <summary>制度化度が <see cref="LockThreshold"/> 以上なら更新を拒みロックすべき＝true（硬直）。</summary>
        public static bool ShouldLock(BeliefState belief, float institutionalization)
        {
            return institutionalization >= LockThreshold;
        }

        /// <summary>仮説を新仮説へ更新＝傾倒度を0へリセットしロックを解く（基準非破壊・null安全）。</summary>
        public static void Update(BeliefState belief, string newHypothesis)
        {
            if (belief == null) return;
            belief.hypothesis = newHypothesis;
            belief.commitment = 0f;
            belief.locked = false;
        }

        /// <summary>仮説への傾倒を強化＝傾倒度を加算し0..1へクランプ（null安全）。</summary>
        public static void Reinforce(BeliefState belief, float amount)
        {
            if (belief == null) return;
            belief.commitment = Mathf.Clamp01(belief.commitment + amount);
        }
    }
}
