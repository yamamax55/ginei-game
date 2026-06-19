using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 証拠の収束評価（IHER-1 #2397『星を継ぐもの』）。独立した証拠断片
    /// (<see cref="EvidenceFragment"/>) を積み上げ、異なる研究分野(<see cref="ResearchField"/>)を
    /// またいで収束するほど実効信頼性が増す＝バラバラの手がかりが噛み合うと真実味が跳ね上がる。
    /// 基礎信頼性(<see cref="TotalCredibility"/>)に分野多様性(<see cref="FieldDiversity"/>)の収束倍率
    /// (<see cref="ConvergenceMultiplier"/>)を掛けた実効信頼性(<see cref="EffectiveCredibility"/>)が
    /// 閾値を超えると確証(<see cref="IsConclusive"/>)とみなす。null 安全・純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EvidenceRules
    {
        /// <summary>分野が1つ増えるごとの収束加算（2分野で+1段＝1.15）。</summary>
        public const float ConvergencePerField = 0.15f;
        /// <summary>収束倍率の上限（4分野で頭打ち≒1.45→cap 1.6 で安全側）。</summary>
        public const float MaxConvergence = 1.6f;

        /// <summary>基礎信頼性＝各断片の credibility×weight の総和（負の寄与は0にクランプ）。null/空は0。</summary>
        public static float TotalCredibility(EvidenceBody b)
        {
            if (b == null || b.fragments == null) return 0f;
            float total = 0f;
            for (int i = 0; i < b.fragments.Count; i++)
            {
                EvidenceFragment f = b.fragments[i];
                if (f == null) continue;
                total += Mathf.Max(0f, f.credibility * f.weight);
            }
            return total;
        }

        /// <summary>証拠が及ぶ異なる研究分野の数（重複は1つと数える）。null/空は0。</summary>
        public static int FieldDiversity(EvidenceBody b)
        {
            if (b == null || b.fragments == null) return 0;
            HashSet<ResearchField> fields = new HashSet<ResearchField>();
            for (int i = 0; i < b.fragments.Count; i++)
            {
                EvidenceFragment f = b.fragments[i];
                if (f == null) continue;
                fields.Add(f.sourceField);
            }
            return fields.Count;
        }

        /// <summary>
        /// 収束倍率。分野が1つ以下なら1.0（収束なし）、2/3/4分野と増えるほど上昇
        /// （1分野ぶんごとに <see cref="ConvergencePerField"/> 加算・<see cref="MaxConvergence"/> 上限）。
        /// </summary>
        public static float ConvergenceMultiplier(int diversity)
        {
            if (diversity <= 1) return 1f;
            float mult = 1f + ConvergencePerField * (diversity - 1);
            return Mathf.Min(MaxConvergence, mult);
        }

        /// <summary>実効信頼性＝基礎信頼性×収束倍率（分野多様性による）。null/空は0。</summary>
        public static float EffectiveCredibility(EvidenceBody b)
        {
            return TotalCredibility(b) * ConvergenceMultiplier(FieldDiversity(b));
        }

        /// <summary>確証判定。実効信頼性が閾値以上なら true（＝散在する手がかりが収束し真実とみなせる）。</summary>
        public static bool IsConclusive(EvidenceBody b, float threshold)
        {
            return EffectiveCredibility(b) >= threshold;
        }
    }
}
