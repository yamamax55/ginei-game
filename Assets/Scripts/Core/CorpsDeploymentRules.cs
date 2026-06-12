using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>軍団陣形の配置候補（1艦隊）。前線適性の算定に使う。</summary>
    public struct DeploymentCandidate
    {
        public int id;
        public float combatAptitude; // 提督の戦闘適性 0..100（攻撃/防御/統率の実効平均など）
        public float meritDesire;    // 功名心 0..100（高いほど前線を志願）
        public float morale;         // 士気 0..1（高いほど前線に耐える）

        public DeploymentCandidate(int id, float combatAptitude, float meritDesire, float morale)
        {
            this.id = id; this.combatAptitude = combatAptitude; this.meritDesire = meritDesire; this.morale = morale;
        }
    }

    /// <summary>前線適性の加重（戦闘力/功名心/士気）。Default を持つ。</summary>
    public readonly struct DeploymentWeights
    {
        public readonly float combat;
        public readonly float merit;
        public readonly float morale;
        public DeploymentWeights(float combat, float merit, float morale)
        {
            this.combat = combat; this.merit = merit; this.morale = morale;
        }
        /// <summary>既定：戦闘力0.5・功名心0.25・士気0.25。</summary>
        public static DeploymentWeights Default => new DeploymentWeights(0.5f, 0.25f, 0.25f);
    }

    /// <summary>
    /// 軍団陣形内の配置（前列⇔後列）を軍団長が決める純ロジック（test-first）。
    /// 軍団長は隷下提督の<b>戦闘力・功名心・士気</b>から前線適性を見立てて並べる。
    /// <b>功名心の高い提督は前線を志願</b>（史実＝功を焦る武将が先陣を望む）＝前線適性が上がる。
    /// <b>能力を適切に見極められるかは軍団長の能力による</b>＝`PerceivedSuitability` が軍団長の技量で真値と当て推量を混ぜる
    /// （有能なら正しく見抜き強兵を前へ、無能なら見立てを誤る）。
    /// </summary>
    public static class CorpsDeploymentRules
    {
        public const float FrontVolunteerThreshold = 70f;

        /// <summary>前線適性（0..1・高いほど前列向き）＝戦闘力・功名心・士気の加重和。</summary>
        public static float FrontSuitability(float combatAptitude, float meritDesire, float morale, DeploymentWeights w)
        {
            float c = Mathf.Clamp01(combatAptitude / 100f);
            float m = Mathf.Clamp01(meritDesire / 100f);
            float mo = Mathf.Clamp01(morale);
            float wsum = Mathf.Max(1e-4f, w.combat + w.merit + w.morale);
            return Mathf.Clamp01((w.combat * c + w.merit * m + w.morale * mo) / wsum);
        }

        /// <summary>既定加重で前線適性を返す。</summary>
        public static float FrontSuitability(float combatAptitude, float meritDesire, float morale)
            => FrontSuitability(combatAptitude, meritDesire, morale, DeploymentWeights.Default);

        /// <summary>
        /// 軍団長の見立て（0..1）。技量 commanderSkill(0..1) で真値と当て推量(roll 0..1)を混ぜる。
        /// 有能(1)なら真値どおり見抜き、無能(0)なら roll に流される＝弱兵を前に出す誤配置が起きる。
        /// </summary>
        public static float PerceivedSuitability(float trueScore, float commanderSkill, float roll)
            => Mathf.Lerp(Mathf.Clamp01(roll), Mathf.Clamp01(trueScore), Mathf.Clamp01(commanderSkill));

        /// <summary>功名心が閾値以上＝前線を志願する提督（史実：先陣を望む功名の士）。</summary>
        public static bool IsFrontVolunteer(float meritDesire, float threshold = FrontVolunteerThreshold)
            => meritDesire >= threshold;

        /// <summary>
        /// 候補を前→後の順（前線適性の見立てが高い順）に並べた id 配列を返す。roll は決定論注入（id→0..1）。
        /// 軍団長が有能なら強兵・功名の士・高士気を前へ、無能なら roll に流される。
        /// </summary>
        public static int[] OrderFrontToBack(IReadOnlyList<DeploymentCandidate> candidates, float commanderSkill,
            System.Func<int, float> roll, DeploymentWeights weights)
        {
            if (candidates == null || candidates.Count == 0) return new int[0];

            int n = candidates.Count;
            var idx = new int[n];
            var score = new float[n];
            for (int i = 0; i < n; i++)
            {
                idx[i] = i;
                DeploymentCandidate c = candidates[i];
                float trueScore = FrontSuitability(c.combatAptitude, c.meritDesire, c.morale, weights);
                float r = roll != null ? roll(c.id) : 0.5f;
                score[i] = PerceivedSuitability(trueScore, commanderSkill, r);
            }

            // 見立てスコア降順（前列が先）。同点は id 昇順で安定化。
            System.Array.Sort(idx, (a, b) =>
            {
                int cmp = score[b].CompareTo(score[a]);
                return cmp != 0 ? cmp : candidates[a].id.CompareTo(candidates[b].id);
            });

            var order = new int[n];
            for (int i = 0; i < n; i++) order[i] = candidates[idx[i]].id;
            return order;
        }

        /// <summary>既定加重版。</summary>
        public static int[] OrderFrontToBack(IReadOnlyList<DeploymentCandidate> candidates, float commanderSkill, System.Func<int, float> roll)
            => OrderFrontToBack(candidates, commanderSkill, roll, DeploymentWeights.Default);
    }
}
