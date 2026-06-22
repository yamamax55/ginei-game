using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>艦隊の運用行動（割り当て予算内で選ぶ「やること」）。</summary>
    public enum FleetTask { 補給, 訓練, 整備, 哨戒, 休養 }

    /// <summary>艦隊運用の予算/費用パラメータ（兵力あたり）。</summary>
    public readonly struct FleetOpsParams
    {
        public readonly float budgetPerStrength; // 兵力あたりの運用予算（艦隊規模に比例）
        public readonly float costSupply;        // 補給（兵力あたり）
        public readonly float costTrain;         // 訓練
        public readonly float costMaintain;      // 整備
        public readonly float costPatrol;        // 哨戒
        public readonly float costRest;          // 休養

        public FleetOpsParams(float budgetPerStrength, float costSupply, float costTrain,
                              float costMaintain, float costPatrol, float costRest)
        {
            this.budgetPerStrength = budgetPerStrength;
            this.costSupply = costSupply;
            this.costTrain = costTrain;
            this.costMaintain = costMaintain;
            this.costPatrol = costPatrol;
            this.costRest = costRest;
        }

        /// <summary>既定：予算=兵力×1.0。費用は 補給0.3／訓練0.4／整備0.25／哨戒0.35／休養0.1（合計1.4＝全部は選べない）。</summary>
        public static FleetOpsParams Default => new FleetOpsParams(1.0f, 0.3f, 0.4f, 0.25f, 0.35f, 0.1f);
    }

    /// <summary>
    /// 艦隊運用の予算ロジック（純・決定論）。プレイヤーが艦隊に割り当てられた予算の範囲で「やること」（補給/訓練/整備/
    /// 哨戒/休養）を選ぶための唯一の窓口＝予算算出・各行動の費用・合計（重複は1回）・予算内判定・残額を返す。
    /// 効果（補給度/練度等）の反映は別途で、本ルールは<b>選択と制約</b>だけを担う。<see cref="PlayerFleetManagementOverlay"/>
    /// の発令UIが使う。test-first。
    /// </summary>
    public static class FleetOperationsRules
    {
        /// <summary>艦隊の運用予算＝兵力×係数（非負）。</summary>
        public static int Budget(int strength, FleetOpsParams p)
            => Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, strength) * p.budgetPerStrength));

        /// <summary>1行動の費用＝兵力×行動係数（非負）。</summary>
        public static int TaskCost(FleetTask task, int strength, FleetOpsParams p)
        {
            float w;
            switch (task)
            {
                case FleetTask.補給: w = p.costSupply; break;
                case FleetTask.訓練: w = p.costTrain; break;
                case FleetTask.整備: w = p.costMaintain; break;
                case FleetTask.哨戒: w = p.costPatrol; break;
                default: w = p.costRest; break;
            }
            return Mathf.Max(0, Mathf.RoundToInt(Mathf.Max(0, strength) * w));
        }

        /// <summary>選択行動の合計費用（同じ行動は1回だけ数える）。</summary>
        public static int TotalCost(IList<FleetTask> tasks, int strength, FleetOpsParams p)
        {
            if (tasks == null || tasks.Count == 0) return 0;
            bool s = false, t = false, m = false, pa = false, r = false;
            int sum = 0;
            for (int i = 0; i < tasks.Count; i++)
            {
                switch (tasks[i])
                {
                    case FleetTask.補給: if (!s) { s = true; sum += TaskCost(FleetTask.補給, strength, p); } break;
                    case FleetTask.訓練: if (!t) { t = true; sum += TaskCost(FleetTask.訓練, strength, p); } break;
                    case FleetTask.整備: if (!m) { m = true; sum += TaskCost(FleetTask.整備, strength, p); } break;
                    case FleetTask.哨戒: if (!pa) { pa = true; sum += TaskCost(FleetTask.哨戒, strength, p); } break;
                    default: if (!r) { r = true; sum += TaskCost(FleetTask.休養, strength, p); } break;
                }
            }
            return sum;
        }

        /// <summary>選択が予算内に収まるか。</summary>
        public static bool CanAfford(IList<FleetTask> tasks, int strength, FleetOpsParams p)
            => TotalCost(tasks, strength, p) <= Budget(strength, p);

        /// <summary>残予算＝予算−合計費用（超過は負）。</summary>
        public static int Remaining(IList<FleetTask> tasks, int strength, FleetOpsParams p)
            => Budget(strength, p) - TotalCost(tasks, strength, p);
    }
}
