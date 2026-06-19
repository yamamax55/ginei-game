using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 補給の優先配分（限られた補給を主攻/前線/危機にどう割り振るか）の調整値。
    /// 任務重要度の効き・切迫度の効き・主攻に厚く配分すべき既定しきい値・崩壊リスクの非線形指数。
    /// </summary>
    public readonly struct SupplyPriorityParams
    {
        /// <summary>任務重要度→優先度の効き（重要度に対する重み・大きいほど主攻に集中）。</summary>
        public readonly float importanceWeight;
        /// <summary>切迫度→優先度の効き（urgency に対する重み・大きいほど危機の戦線へ寄せる）。</summary>
        public readonly float urgencyWeight;
        /// <summary>主攻が「厚く配分できた」と見なす充足度の既定しきい値（0..1）。</summary>
        public readonly float mainEffortThreshold;
        /// <summary>薄く削った戦線が崩れるリスクの非線形指数（大きいほど最低線割れで急増）。</summary>
        public readonly float starvationExponent;

        public SupplyPriorityParams(float importanceWeight, float urgencyWeight, float mainEffortThreshold, float starvationExponent)
        {
            this.importanceWeight = Mathf.Max(0f, importanceWeight);
            this.urgencyWeight = Mathf.Max(0f, urgencyWeight);
            this.mainEffortThreshold = Mathf.Clamp01(mainEffortThreshold);
            this.starvationExponent = Mathf.Max(0.01f, starvationExponent);
        }

        /// <summary>既定：重要度の効き1.0・切迫度の効き1.0・主攻しきい値0.8・崩壊指数2.0（最低線割れで急増）。</summary>
        public static SupplyPriorityParams Default
            => new SupplyPriorityParams(DefaultImportanceWeight, DefaultUrgencyWeight, DefaultMainEffortThreshold, DefaultStarvationExponent);

        public const float DefaultImportanceWeight = 1.0f;
        public const float DefaultUrgencyWeight = 1.0f;
        public const float DefaultMainEffortThreshold = 0.8f;
        public const float DefaultStarvationExponent = 2.0f;
    }

    /// <summary>
    /// 補給の優先配分（限られた補給を主攻/前線/危機にどう割り振るか）の純ロジック。
    /// 補給能力が需要を満たせないとき、主攻に厚く・二次正面に薄く配る判断を担う。
    /// 配分を誤れば主攻が止まり、削りすぎた戦線が崩れる（最低線割れ）。
    ///
    /// <b>分担</b>：
    /// - <see cref="SupplyRules"/>／<see cref="LogisticsBurdenRules"/>（補給の到達・消費・補給線の負担・既存なら）とは別＝
    ///   こちらは<b>限られた補給をどの戦線へ割り振るかの配分判断</b>に徹する（到達・消費は扱わない）。
    /// - <see cref="StrategicReserveRules"/>（戦域の予備兵力＝遊兵の運用）とは別＝予備兵力でなく<b>補給物資</b>の配分。
    /// 盤面非依存の plain 引数のみ（艦隊・シーンに触れない）。test-first・実効値パターン（基準値非破壊）。
    /// </summary>
    public static class SupplyPriorityRules
    {
        /// <summary>既定パラメータで補給不足の総量を返す。</summary>
        public static float TotalShortfall(float totalDemand, float totalSupply)
            => TotalShortfall(totalDemand, totalSupply, SupplyPriorityParams.Default);

        /// <summary>
        /// 補給不足の総量（需要−供給・非負）。供給が需要を満たせば0、足りなければ不足ぶん。
        /// この不足ぶんを「どこから削るか」が以降の配分判断。
        /// </summary>
        public static float TotalShortfall(float totalDemand, float totalSupply, SupplyPriorityParams p)
        {
            float demand = Mathf.Max(0f, totalDemand);
            float supply = Mathf.Max(0f, totalSupply);
            return Mathf.Max(0f, demand - supply);
        }

        /// <summary>既定パラメータで部隊の補給優先度を返す。</summary>
        public static float PriorityWeight(float missionImportance, float urgency)
            => PriorityWeight(missionImportance, urgency, SupplyPriorityParams.Default);

        /// <summary>
        /// 部隊の補給優先度。任務重要度(0..1想定)と切迫度(0..1想定)を各重みで足し合わせる。
        /// 主攻（重要度高）や危機の戦線（切迫度高）ほど高い＝配分が厚く回る。重要度も切迫度も0なら優先度0。
        /// </summary>
        public static float PriorityWeight(float missionImportance, float urgency, SupplyPriorityParams p)
        {
            float importance = Mathf.Clamp01(missionImportance);
            float urg = Mathf.Clamp01(urgency);
            return Mathf.Max(0f, importance * p.importanceWeight + urg * p.urgencyWeight);
        }

        /// <summary>
        /// 優先度比例の補給配分。各戦線の優先度(priorityWeights)に比例して availableSupply を割り振る。
        /// 主攻（優先度高）へ厚く回る。優先度が全て0／配列が null・空なら全0配列（行き先なし）。
        /// 配列は null/空安全・負値は0クランプ。総量保存。
        /// </summary>
        public static float[] Allocation(float[] priorityWeights, float availableSupply)
        {
            if (priorityWeights == null || priorityWeights.Length == 0)
                return new float[0];

            int n = priorityWeights.Length;
            float[] result = new float[n];
            float total = Mathf.Max(0f, availableSupply);

            float sum = 0f;
            for (int i = 0; i < n; i++)
            {
                float w = Mathf.Max(0f, priorityWeights[i]);
                result[i] = w;
                sum += w;
            }

            if (sum <= 0f)
            {
                for (int i = 0; i < n; i++) result[i] = 0f;
                return result;
            }

            for (int i = 0; i < n; i++)
                result[i] = total * (result[i] / sum);
            return result;
        }

        /// <summary>
        /// 配分後の各戦線の充足度(0..1)。配分(allocatedSupply)が需要(sectorDemand)を満たせば1.0、
        /// 半分なら0.5。需要0なら満たすものが無い＝1.0（充足）。負値は0クランプ。
        /// </summary>
        public static float SectorReadiness(float allocatedSupply, float sectorDemand)
        {
            float allocated = Mathf.Max(0f, allocatedSupply);
            float demand = Mathf.Max(0f, sectorDemand);
            if (demand <= 0f) return 1f;
            return Mathf.Clamp01(allocated / demand);
        }

        /// <summary>
        /// 主攻の充足(0..1)。主攻への配分(mainEffortAllocation)が主攻需要(mainEffortDemand)をどれだけ満たすか。
        /// 主攻に厚く配分できたか＝主攻が止まらず動けるかの指標（<see cref="SectorReadiness"/> の主攻版・意味づけ）。
        /// </summary>
        public static float MainEffortPriority(float mainEffortAllocation, float mainEffortDemand)
            => SectorReadiness(mainEffortAllocation, mainEffortDemand);

        /// <summary>既定パラメータで薄く削った戦線の崩壊リスクを返す。</summary>
        public static float StarvedSectorRisk(float allocatedSupply, float sectorMinimum)
            => StarvedSectorRisk(allocatedSupply, sectorMinimum, SupplyPriorityParams.Default);

        /// <summary>
        /// 薄く削った戦線が崩れるリスク(0..1)。配分(allocatedSupply)が戦線維持の最低線(sectorMinimum)を
        /// どれだけ割り込んだかで決まる。最低線を満たせばリスク0、配分ゼロでリスク最大1.0。
        /// starvationExponent で非線形＝最低線を大きく割り込むほど急増（削りすぎは崩壊）。
        /// 最低線が0なら割れようがない＝リスク0。
        /// </summary>
        public static float StarvedSectorRisk(float allocatedSupply, float sectorMinimum, SupplyPriorityParams p)
        {
            float allocated = Mathf.Max(0f, allocatedSupply);
            float minimum = Mathf.Max(0f, sectorMinimum);
            if (minimum <= 0f) return 0f;
            // 最低線に対する不足割合(0..1)。満たせば0、ゼロ配分で1。
            float shortfallFraction = Mathf.Clamp01((minimum - allocated) / minimum);
            return Mathf.Clamp01(Mathf.Pow(shortfallFraction, p.starvationExponent));
        }

        /// <summary>
        /// 危機戦線を最優先で救うトリアージ充足(0..1)。危機の需要(criticalDemand)を総供給(totalSupply)から
        /// 真っ先に満たす＝危機需要が総供給以内なら1.0（全救援）、超えれば供給ぶんしか救えない。
        /// 危機需要が0なら救うべき危機が無い＝1.0。負値は0クランプ。
        /// </summary>
        public static float LogisticsTriage(float criticalDemand, float totalSupply)
        {
            float critical = Mathf.Max(0f, criticalDemand);
            float supply = Mathf.Max(0f, totalSupply);
            if (critical <= 0f) return 1f;
            return Mathf.Clamp01(supply / critical);
        }

        /// <summary>既定しきい値で補給が足りているか判定する。</summary>
        public static bool IsSupplyAdequate(float sectorReadiness)
            => IsSupplyAdequate(sectorReadiness, SupplyPriorityParams.Default.mainEffortThreshold);

        /// <summary>
        /// 補給が足りているか(bool)。戦線の充足度(sectorReadiness)が threshold 以上なら充足（主攻が止まらない）。
        /// しきい値を下回れば配分不足＝その戦線は補給に飢える。
        /// </summary>
        public static bool IsSupplyAdequate(float sectorReadiness, float threshold)
        {
            float readiness = Mathf.Clamp01(sectorReadiness);
            float th = Mathf.Clamp01(threshold);
            return readiness >= th;
        }
    }
}
