using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// ミッションコマンド＝任務戦術（Auftragstaktik・史実準拠）の純ロジック。
    /// 上級司令部は「◯◯星系を攻略せよ」と<b>目標だけ</b>を示し、<b>参謀本部</b>が状況判断で
    /// <b>必要兵力を見積もり</b>、その規模の戦力を<b>自動で動員</b>する（どう動かすかは下級が裁量＝任務戦術の核）。
    ///
    /// 史実の要点：
    /// ・攻者三倍の原則（防御された目標への計画的攻勢は概ね 3:1 の優越を求める）を兵力見積もりの基線にする。
    /// ・<b>動員する数は参謀本部の実力で可変</b>＝有能な参謀本部は精緻に（無駄なく必要十分を）見積もり、
    ///   かつ大規模作戦（複数軍団を束ねた軍集団）を統制できる。無能な参謀本部は過大な安全率を盛りつつ
    ///   統制可能規模が小さい＝「必要と称する量を field できず過小動員のまま発動」する失敗様式。
    /// ・諸般の事情（補給・政治的意志・他戦線へ拘束された予備）は <paramref>circumstanceFactor</paramref> で統制上限を上下させる。
    ///
    /// 梯団（艦隊⊂軍団⊂軍集団…）の規模境界・指揮可能規模は <see cref="CommandCapacityRules"/> へ委譲（二重定義しない）。
    /// Core 純ロジック・test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class MissionCommandRules
    {
        // 兵力比（攻者三倍の原則ベース）。
        public const float ForceRatio攻略防御 = 3.0f;   // 防衛惑星/守備隊ありの計画的攻勢
        public const float ForceRatio攻略無防備 = 1.5f; // 無防備星系の占領（軽い優越で足りる）
        public const float ForceRatio防衛 = 1.0f;       // 守るのは均衡で足りる
        public const float ForceRatio哨戒 = 0.5f;       // 示威・偵察＝小規模

        public const float MinMobilization = 2000f;     // 最小動員（敵が居なくても駐留兵力は要る）
        public const float MaxSafetyMargin = 0.5f;       // 無能な参謀本部が盛る安全率の上限（+50%）

        // 統制可能規模（参謀本部の span of command）。無能＝一個艦隊規模、有能＝軍集団規模。
        public const float MinCoordinable = 15000f;      // ≒ 一個艦隊（CommandCapacityRules.Cap大将）
        public const float MaxCoordinable = 90000f;      // ≒ 軍集団（CommandCapacityRules.Ships軍集団Max）

        /// <summary>任務種別×防衛有無に応じた必要兵力比（攻者三倍の原則）。</summary>
        public static float ForceRatio(MissionType type, bool defended)
        {
            switch (type)
            {
                case MissionType.星系攻略: return defended ? ForceRatio攻略防御 : ForceRatio攻略無防備;
                case MissionType.星系防衛: return ForceRatio防衛;
                case MissionType.哨戒:     return ForceRatio哨戒;
                default:                   return ForceRatio防衛;
            }
        }

        /// <summary>
        /// 参謀本部の必要兵力見積もり。基線＝敵戦力×兵力比。無能ほど安全率を盛る（過大見積もり）。
        /// <paramref name="staffCompetence"/> は 0..1（参謀本部の実力＝運営/情報の文才を正規化したもの）。
        /// </summary>
        public static float EstimateRequiredStrength(float enemyStrength, MissionType type, bool defended, float staffCompetence)
        {
            float ratio = ForceRatio(type, defended);
            float baseline = Mathf.Max(0f, enemyStrength) * ratio;
            float comp = Mathf.Clamp01(staffCompetence);
            float margin = Mathf.Lerp(MaxSafetyMargin, 0f, comp); // 有能＝無駄なく／無能＝盛る
            return Mathf.Max(MinMobilization, baseline * (1f + margin));
        }

        /// <summary>
        /// 参謀本部が同時に統制できる戦力上限（span of command）。有能ほど大規模作戦を捌ける。
        /// 諸般の事情（補給・政治・予備の拘束）を <paramref name="circumstanceFactor"/>（既定1）で上下させる。
        /// </summary>
        public static float MaxCoordinableStrength(float staffCompetence, float circumstanceFactor = 1f)
        {
            float comp = Mathf.Clamp01(staffCompetence);
            float span = Mathf.Lerp(MinCoordinable, MaxCoordinable, comp);
            return span * Mathf.Max(0f, circumstanceFactor);
        }

        /// <summary>動員規模に対応する梯団（艦隊⊂軍団⊂軍⊂軍集団⊂宇宙艦隊）。境界は <see cref="CommandCapacityRules"/> 準拠。</summary>
        public static EchelonType RecommendEchelon(float strength)
        {
            if (strength <= CommandCapacityRules.Ships艦隊Max) return EchelonType.艦隊;
            if (strength <= CommandCapacityRules.Ships軍団Max) return EchelonType.軍団;
            if (strength <= CommandCapacityRules.Ships軍Max) return EchelonType.軍;
            if (strength <= CommandCapacityRules.Ships軍集団Max) return EchelonType.軍集団;
            return EchelonType.宇宙艦隊;
        }

        /// <summary>
        /// 任務を計画する：必要兵力を見積もり、統制上限内で「必要十分」な戦力を在席の遊休艦隊から自動動員する。
        /// 動員は戦力大きい順の貪欲選抜（同値は id 昇順）。統制上限が必要兵力に届かないと過小動員（feasible=false）。
        /// </summary>
        public static MissionPlan PlanMission(
            int targetSystemId, MissionType type, Faction faction,
            float enemyStrength, bool defended, float staffCompetence,
            IReadOnlyList<MissionForce> available, float circumstanceFactor = 1f)
        {
            var plan = new MissionPlan
            {
                targetSystemId = targetSystemId,
                type = type,
                faction = faction,
            };

            plan.requiredStrength = EstimateRequiredStrength(enemyStrength, type, defended, staffCompetence);
            float maxCoord = MaxCoordinableStrength(staffCompetence, circumstanceFactor);
            // 「必要規模」と「統制できる上限」の小さい方を目標に動員する（無駄も無理もしない）。
            float target = Mathf.Min(plan.requiredStrength, maxCoord);

            if (available != null && available.Count > 0)
            {
                // 戦力大きい順・同値は id 昇順で決定論ソート。
                var sorted = new List<MissionForce>(available);
                sorted.Sort((a, b) => a.strength != b.strength ? b.strength.CompareTo(a.strength) : a.id.CompareTo(b.id));
                for (int i = 0; i < sorted.Count; i++)
                {
                    if (plan.committedStrength >= target) break; // 必要十分に達したら止める
                    plan.fleetIds.Add(sorted[i].id);
                    plan.committedStrength += sorted[i].strength;
                }
            }

            plan.feasible = plan.committedStrength >= plan.requiredStrength;
            plan.echelon = RecommendEchelon(plan.committedStrength);
            return plan;
        }
    }
}
