using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 労働市場の暦境界オーケストレータ（POPLAB-2/3 配線・#2026・純ロジック）。
    /// 惑星（<see cref="Province"/>）の職業配分を1年ぶん動かす＝<b>安定度#109 に応じた労働需要</b>へ <see cref="Workforce"/> を転職フローで収束（総量保存）。
    /// 安定度が下がると（占領直後・戦時）循環失業が増え無職へ寄り、回復すると就業が戻る＝<b>内政#109 と噛み合う労働市場の循環</b>。
    /// 既存の <see cref="OccupationRules.EmploymentRate"/>（=1−無職シェア）がそのまま失業を映す。集約・暦境界Tick・後方互換。test-first。
    /// </summary>
    public static class LaborMarketTickRules
    {
        public const float DefaultFlowRate = 0.25f;       // 年次の転職フロー速度（摩擦＝緩やか）
        public const float CyclicalSensitivity = 0.005f;  // 安定度1ポイント下落あたりの循環失業

        /// <summary>
        /// 安定度に応じた労働需要シェア（目標）。基準＝類型既定（構造的需要）。安定度が基準を下回ると就業者を循環失業ぶん無職へ回す。
        /// 合計は1（総量保存）。
        /// </summary>
        public static Workforce JobDemandShares(Province p)
        {
            var demand = OccupationRules.Default(p.systemType); // 構造的需要（類型）＝合計1
            float cyclical = Mathf.Clamp01(Mathf.Max(0f, GovernanceRules.BaseStability - p.stability) * CyclicalSensitivity);
            if (cyclical <= 0f) return demand;
            float employedScale = 1f - cyclical;
            float added = 0f;
            for (int i = 0; i < Workforce.Count; i++)
            {
                if (i == (int)Occupation.無職) continue;
                float before = demand.shares[i];
                demand.shares[i] = before * employedScale;
                added += before - demand.shares[i];
            }
            demand.shares[(int)Occupation.無職] += added;
            return demand; // 合計は保存される（足した分＝引いた分）
        }

        /// <summary>1年ぶんの職業配分＝安定度連動の需要へ転職フローで収束（無職シェア＝失業が安定度で増減）。</summary>
        public static void TickYear(Province p, float flowRate)
        {
            if (p == null) return;
            if (p.workforce == null) p.workforce = OccupationRules.Default(p.systemType);
            var target = JobDemandShares(p);
            p.workforce = OccupationAllocationRules.Converge(p.workforce, target, flowRate);
        }

        /// <summary>現在の失業率＝無職シェア（観測/集計用・#1957 国マクロへ集約可）。</summary>
        public static float UnemploymentRate(Province p)
        {
            if (p == null) return 0f;
            Workforce w = p.workforce ?? OccupationRules.Default(p.systemType);
            return Mathf.Clamp01(w.Share(Occupation.無職));
        }
    }
}
