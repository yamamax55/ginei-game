using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 軍要求物資の在庫消費を勢力ごとに回す配線（MILSUP-1/2/3・#2049）。
    /// 既存の <see cref="MilitarySupplyTickRules.TickFleet"/>（補給線#94 の通否で supply を増減）に対し、
    /// こちらは<b>勢力の資源備蓄（物資/弾薬/燃料 = <see cref="ResourceStockpile"/>）から活動状態（待機/移動/交戦）ぶんを実消費</b>し、
    /// 在庫が需要に届かなければ補給レディネスを下げて<b>枯渇損耗</b>させる＝弾薬庫が空けば長期戦は成立しない（兵糧攻め）。
    /// per-faction 集約（砲弾1発単位に降りない＝終盤ラグ回避）。数式は MILSUP 各ルールへ委譲し二重実装しない。
    /// </summary>
    public partial class GalaxyView
    {
        // 補給枯渇でレディネスを更新するステップ（在庫充足で上げ・不足で下げ・約数日でクランプ）。
        private const float MilSupplyRecoverStep = 0.34f; // 在庫充足時の回復
        private const float MilSupplyDepleteStep = 0.20f; // 在庫不足時の枯渇
        private const float MilSupplyAttritionRate = 0.05f; // 干上がった部隊の損耗率（控えめ・bounded）
        private const float MilSupplyLowReadiness = 0.5f;  // この閾値未満で損耗が発生

        /// <summary>資源消費による補給途絶を通知済みの勢力（エッジ検出＝枯渇に陥った瞬間に1回だけ警告）。</summary>
        private readonly HashSet<Faction> milSupplyStarving = new HashSet<Faction>();

        // 勢力ごとの艦隊を集める作業リスト（毎フレ確保しない・集約集計用）。
        private readonly List<StrategicFleet> milSupplyScratch = new List<StrategicFleet>();

        /// <summary>
        /// 軍の在庫消費を1年ぶん回す（#2049 兵站）。勢力ごとに所属艦隊の活動別総需要を備蓄から実消費し、
        /// 在庫が需要に届かない勢力は全艦隊の補給レディネスが下がって損耗する＝補給が尽きた前線は干上がる。
        /// </summary>
        private void RunMilitarySupplyConsumptionTick()
        {
            if (reg == null || reg.fleets == null || DemoFactions == null) return;

            for (int fi = 0; fi < DemoFactions.Length; fi++)
            {
                Faction fac = DemoFactions[fi];
                ResourceStockpile stock = GetStateStockpile(fac);
                if (stock == null) continue; // 備蓄未生成の勢力は対象外（後方互換）

                // この勢力の生存艦隊を集約
                milSupplyScratch.Clear();
                for (int i = 0; i < reg.fleets.Count; i++)
                {
                    StrategicFleet f = reg.fleets[i];
                    if (f != null && f.faction == fac && f.strength > 0) milSupplyScratch.Add(f);
                }
                if (milSupplyScratch.Count == 0) continue;

                // カテゴリ別に総需要を備蓄から消費し、最小律の充足率を全体レディネスとする。
                float overall = 1f;
                for (int t = 0; t < 3; t++)
                {
                    ResourceType type = (ResourceType)t;
                    float demand = MilitaryDemandRules.AggregateDemand(milSupplyScratch, type);
                    if (demand <= 0f) continue;
                    float available = stock.Get(type);
                    float consume = MilitarySupplyFulfillmentRules.Consume(available, demand);
                    stock.Add(type, -consume); // delta 消費（0未満にはならない）
                    float fulfill = MilitarySupplyFulfillmentRules.Fulfillment(available, demand);
                    overall = Mathf.Min(overall, fulfill); // 最も不足するカテゴリが律速
                }

                bool supplied = overall >= 1f;
                int totalLost = 0;
                for (int i = 0; i < milSupplyScratch.Count; i++)
                {
                    StrategicFleet f = milSupplyScratch[i];
                    // レディネスを在庫充足へ寄せる（充足で回復・不足で枯渇・実効値＝基準フィールド supply を更新）。
                    float step = supplied ? MilSupplyRecoverStep : -MilSupplyDepleteStep;
                    f.supply = Mathf.Clamp01(f.supply + step);

                    if (f.supply < MilSupplyLowReadiness)
                    {
                        int lost = Mathf.RoundToInt(
                            MilitarySupplyFulfillmentRules.AttritionFromShortage(f.strength, f.supply, MilSupplyAttritionRate));
                        if (lost > 0)
                        {
                            f.strength = Mathf.Max(0, f.strength - lost);
                            totalLost += lost;
                        }
                    }
                }

                // エッジ検出通知＝枯渇に陥った瞬間だけ警告し、補給回復で解除（毎年は鳴らさない）。
                if (!supplied)
                {
                    if (milSupplyStarving.Add(fac))
                        NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告,
                            $"{fac} 軍需備蓄が払底（補給{Mathf.RoundToInt(overall * 100f)}%）" +
                            (totalLost > 0 ? $"・艦隊が損耗（-{totalLost}）" : "・前線が干上がる"));
                }
                else
                {
                    milSupplyStarving.Remove(fac);
                }
            }
        }

        /// <summary>
        /// 勢力の平均補給レディネス（0..1・観測層 read-only／#2049）。所属艦隊が無ければ満補給（1）扱い。
        /// </summary>
        public float SupplyReadinessOf(Faction faction)
        {
            if (reg == null || reg.fleets == null) return 1f;
            float sum = 0f; int n = 0;
            for (int i = 0; i < reg.fleets.Count; i++)
            {
                StrategicFleet f = reg.fleets[i];
                if (f == null || f.faction != faction) continue;
                sum += Mathf.Clamp01(f.supply);
                n++;
            }
            return n == 0 ? 1f : sum / n;
        }
    }
}
