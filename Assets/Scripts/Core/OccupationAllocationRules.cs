using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 職業配分の動的化＝転職フロー（POPLAB-2・#2026・#110・純ロジック）。
    /// 静的な <see cref="Workforce"/> を需要・賃金・教育で動かす＝現シェアを目標シェアへ一定割合ずつ移す（<b>総量保存</b>・摩擦で緩やか）。
    /// 目標値収束（<see cref="GovernanceRules"/> 流儀）。暦境界（月次想定）でTick。少数6種のまま（粒度を下げない＝タイクン回避）。test-first。
    /// </summary>
    public static class OccupationAllocationRules
    {
        /// <summary>労働需要（職業別求人量）→目標シェア（正規化）。需要の多い職へ流れる。</summary>
        public static Workforce TargetShareFromDemand(IReadOnlyList<float> jobDemandByOccupation)
        {
            var w = new Workforce();
            if (jobDemandByOccupation == null) return w;
            for (int i = 0; i < Workforce.Count && i < jobDemandByOccupation.Count; i++)
                w.shares[i] = Mathf.Max(0f, jobDemandByOccupation[i]);
            w.Normalize();
            return w;
        }

        /// <summary>
        /// 現シェアを目標シェアへ flowRate ぶん移す＝new[i]=cur[i]+(target[i]-cur[i])×flowRate。
        /// cur/target がともに合計1なら <b>合計は保存される</b>（Σ(target-cur)=0）。flowRate∈[0,1]＝摩擦で緩やか。
        /// </summary>
        public static Workforce Converge(Workforce current, Workforce target, float flowRate)
        {
            var w = new Workforce();
            if (current == null || target == null) return w;
            float f = Mathf.Clamp01(flowRate);
            for (int i = 0; i < Workforce.Count; i++)
                w.shares[i] = Mathf.Max(0f, current.shares[i] + (target.shares[i] - current.shares[i]) * f);
            return w;
        }

        /// <summary>1ステップ＝需要から目標を作り、現シェアをそこへ収束（暦境界で呼ぶ想定）。</summary>
        public static Workforce Step(Workforce current, IReadOnlyList<float> jobDemandByOccupation, float flowRate)
            => Converge(current, TargetShareFromDemand(jobDemandByOccupation), flowRate);
    }
}
