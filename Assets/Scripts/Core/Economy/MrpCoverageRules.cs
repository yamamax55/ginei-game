using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// MRP の充足評価（SCM-3・#2105・純ロジック）。所要に対する供給の充足率・不足量。
    /// 最小律のサービスレベル（最も逼迫した原材料がチェーンを律速）は <see cref="ScmTickRules"/> が原材料に対して計算。test-first。
    /// </summary>
    public static class MrpCoverageRules
    {
        /// <summary>充足率＝所要0以下は1／clamp01(供給/所要)。</summary>
        public static float Coverage(float available, float requirement)
            => requirement <= 0f ? 1f : Mathf.Clamp01(Mathf.Max(0f, available) / requirement);

        /// <summary>不足量＝max(0, 所要−供給)。</summary>
        public static float Shortfall(float available, float requirement)
            => Mathf.Max(0f, requirement - Mathf.Max(0f, available));
    }
}
