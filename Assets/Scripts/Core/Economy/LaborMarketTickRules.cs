using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働市場の暦境界オーケストレータ（POPLAB-2/3/6 + SKILL-5 配線・#2026/#2034・純ロジック）。
    /// 惑星（<see cref="Province"/>）の職業配分を1年ぶん動かす＝<b>安定度#109・戦時動員#96・技能による再配置速度</b>を織り込んだ労働需要へ
    /// <see cref="Workforce"/> を転職フローで収束（総量保存）。安定度↓で循環失業↑、戦時は生産労働→軍属（総力戦#96）、技能が高い大衆ほど速く再配置（リスキリング#2034）。
    /// 既存 <see cref="OccupationRules.EmploymentRate"/>/<see cref="OccupationRules.RecruitablePool"/> がそのまま動態を映す。集約・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class LaborMarketTickRules
    {
        public const float DefaultFlowRate = 0.25f;       // 年次の基準転職フロー速度（摩擦＝緩やか）
        public const float CyclicalSensitivity = 0.005f;  // 安定度1ポイント下落あたりの循環失業
        public const float WarMobilizationRate = 0.25f;   // 戦時（前線星系）の動員率＝生産労働をこの割合で軍属へ

        /// <summary>安定度・戦時動員を織り込んだ労働需要シェア（目標・合計1＝総量保存）。</summary>
        public static Workforce JobDemandShares(Province p, float mobilizationRate)
        {
            var demand = OccupationRules.Default(p.systemType); // 構造的需要（類型）＝合計1
            // 戦時動員（総力戦#96）：生産労働（農民/工員/鉱員）の mob 割合を軍属へ
            float mob = Mathf.Clamp01(mobilizationRate);
            if (mob > 0f)
            {
                float drawn = 0f;
                int[] producers = { (int)Occupation.農民, (int)Occupation.工員, (int)Occupation.鉱員 };
                for (int k = 0; k < producers.Length; k++)
                {
                    float take = demand.shares[producers[k]] * mob;
                    demand.shares[producers[k]] -= take;
                    drawn += take;
                }
                demand.shares[(int)Occupation.軍属] += drawn;
            }
            // 安定度連動の循環失業（占領直後・戦時の不安定で就業者を無職へ）
            float cyclical = Mathf.Clamp01(Mathf.Max(0f, GovernanceRules.BaseStability - p.stability) * CyclicalSensitivity);
            if (cyclical > 0f)
            {
                float added = 0f;
                for (int i = 0; i < Workforce.Count; i++)
                {
                    if (i == (int)Occupation.無職) continue;
                    float before = demand.shares[i];
                    demand.shares[i] = before * (1f - cyclical);
                    added += before - demand.shares[i];
                }
                demand.shares[(int)Occupation.無職] += added;
            }
            return demand; // 合計は保存される
        }

        /// <summary>安定度連動のみ（動員なし）の労働需要。</summary>
        public static Workforce JobDemandShares(Province p) => JobDemandShares(p, 0f);

        /// <summary>技能（教育）による再配置速度（リスキリング#2034・SKILL-5）＝技能が高い大衆ほど速く転職できる（0.5〜1.5倍）。</summary>
        public static float ReskillingFlowRate(float baseFlow, float overallSkill)
            => Mathf.Max(0f, baseFlow) * Mathf.Lerp(0.5f, 1.5f, Mathf.Clamp01(overallSkill));

        /// <summary>1年ぶんの職業配分＝安定度・動員を織り込んだ需要へ転職フローで収束。</summary>
        public static void TickYear(Province p, float mobilizationRate, float flowRate)
        {
            if (p == null) return;
            if (p.workforce == null) p.workforce = OccupationRules.Default(p.systemType);
            var target = JobDemandShares(p, mobilizationRate);
            p.workforce = OccupationAllocationRules.Converge(p.workforce, target, flowRate);
        }

        /// <summary>動員なしの1年Tick（後方互換）。</summary>
        public static void TickYear(Province p, float flowRate) => TickYear(p, 0f, flowRate);

        /// <summary>現在の失業率＝無職シェア（観測/集計用・#1957 国マクロへ集約可）。</summary>
        public static float UnemploymentRate(Province p)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? OccupationRules.Default(p.systemType);
            return Mathf.Clamp01(w.Share(Occupation.無職));
        }
    }
}
