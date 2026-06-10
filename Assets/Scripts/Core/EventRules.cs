using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// イベント評価の純ロジック（#116・唯一の窓口）。発火資格（条件・一回限り・クールダウン）の判定、
    /// 重み付き抽選、選択肢の効果適用を扱う。各機能（内政 P-6 #115・政治 #14・戦略イベント）はこのエンジンに乗る。
    /// 駆動・キューは <see cref="EventEngine"/>。重い分岐ツリーは作らない（1イベントは小さく）。test-first。
    /// </summary>
    public static class EventRules
    {
        /// <summary>発火条件を満たすか（条件 null＝常に真）。</summary>
        public static bool ConditionMet(GameEventDef def, EventContext ctx)
            => def != null && (def.condition == null || def.condition(ctx));

        /// <summary>クールダウンが明けているか（未発火 or 経過時間 ≥ cooldown）。</summary>
        public static bool OffCooldown(GameEventDef def, EventRuntimeState state, float now)
        {
            if (def == null) return false;
            if (state == null || float.IsNegativeInfinity(state.lastFireTime)) return true;
            return (now - state.lastFireTime) >= def.cooldown;
        }

        /// <summary>いま発火できるか（条件＋一回限り＋クールダウン）。</summary>
        public static bool IsEligible(GameEventDef def, EventRuntimeState state, EventContext ctx, float now)
        {
            if (def == null) return false;
            if (!def.repeatable && state != null && state.fireCount > 0) return false; // 一回限りは既発火で不可
            if (!ConditionMet(def, ctx)) return false;
            if (!OffCooldown(def, state, now)) return false;
            return true;
        }

        /// <summary>発火を記録する（回数++・最終時刻）。</summary>
        public static void MarkFired(EventRuntimeState state, float now)
        {
            if (state == null) return;
            state.fireCount++;
            state.lastFireTime = now;
        }

        /// <summary>
        /// 候補から重み付きで1件選ぶ（roll∈[0,1)）。総重み0や空は null。重い候補ほど選ばれやすい。
        /// </summary>
        public static GameEventDef SelectWeighted(IList<GameEventDef> eligible, float roll)
        {
            if (eligible == null || eligible.Count == 0) return null;

            float total = 0f;
            for (int i = 0; i < eligible.Count; i++)
                if (eligible[i] != null) total += Mathf.Max(0f, eligible[i].weight);
            if (total <= 0f) return null;

            float target = Mathf.Clamp01(roll) * total;
            float acc = 0f;
            for (int i = 0; i < eligible.Count; i++)
            {
                if (eligible[i] == null) continue;
                acc += Mathf.Max(0f, eligible[i].weight);
                if (target < acc) return eligible[i];
            }
            return eligible[eligible.Count - 1]; // 端数の保険
        }

        /// <summary>選択肢の効果を適用する（範囲外の index は無視＝通知の確認等）。</summary>
        public static void ApplyChoice(GameEventDef def, int choiceIndex, EventContext ctx)
        {
            if (def == null) return;
            if (choiceIndex >= 0 && choiceIndex < def.choices.Count)
                def.choices[choiceIndex].Apply(ctx);
        }
    }
}
