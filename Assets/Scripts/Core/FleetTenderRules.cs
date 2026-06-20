using UnityEngine;

namespace Ginei
{
    /// <summary>支援艦（工作艦・補給艦・病院船）の調整係数。前線で艦隊に随伴し、野戦修理・洋上補給・医療を担う。</summary>
    public readonly struct FleetTenderParams
    {
        /// <summary>工作艦1隻あたりの基礎修理能力スケール（0..1・複数隻で野戦修理能力を積む単位）。</summary>
        public readonly float repairPerTender;
        /// <summary>補給艦1隻あたりの基礎補給速度スケール（0..1・複数隻で洋上補給速度を積む単位）。</summary>
        public readonly float supplyPerShip;
        /// <summary>補給と修理が継戦力を延伸する最大幅（0..1・補給×修理が満点のとき延ばせる上限）。</summary>
        public readonly float sustainmentGain;
        /// <summary>支援艦の基礎脆弱性スケール（0..1・無装甲・無護衛の支援艦の被狙い度の上限）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>支援艦喪失が継戦力に与える打撃の重み（0..1・継戦を支援に依存するほど喪失が痛い）。</summary>
        public readonly float lossImpactWeight;
        /// <summary>兵站的に支えられているとみなす既定の閾値（0..1・継戦力延伸がこれ以上で支援成立）。</summary>
        public readonly float sustainThreshold;

        public FleetTenderParams(float repairPerTender, float supplyPerShip, float sustainmentGain,
            float vulnerabilityScale, float lossImpactWeight, float sustainThreshold)
        {
            this.repairPerTender = Mathf.Clamp01(repairPerTender);
            this.supplyPerShip = Mathf.Clamp01(supplyPerShip);
            this.sustainmentGain = Mathf.Clamp01(sustainmentGain);
            this.vulnerabilityScale = Mathf.Clamp01(vulnerabilityScale);
            this.lossImpactWeight = Mathf.Clamp01(lossImpactWeight);
            this.sustainThreshold = Mathf.Clamp01(sustainThreshold);
        }

        /// <summary>既定＝修理単位0.25・補給単位0.25・継戦延伸0.6・脆弱0.8・喪失打撃0.7・支援閾値0.4。</summary>
        public static FleetTenderParams Default =>
            new FleetTenderParams(0.25f, 0.25f, 0.6f, 0.8f, 0.7f, 0.4f);
    }

    /// <summary>
    /// 支援艦（工作艦・補給艦・病院船）＝艦隊に随伴して前線で支援する純ロジック（兵站・前線支援）。
    /// <b>野戦修理</b>（工作艦が損傷艦を前線で直す）・<b>洋上補給</b>（補給艦が燃料弾薬を艦隊へ移送）・
    /// <b>医療支援</b>（病院船が死傷者を収容）で艦隊の<b>継戦力を延伸</b>するが、支援艦自体は装甲・護衛が薄く
    /// <b>脆く</b>、<b>喪失すると継戦が一気に細る</b>＝前線に随伴させる価値とリスクのトレードオフ。
    /// <see cref="DepotRules"/>（前進補給基地＝後方の倉庫拠点）とは別＝こちらは艦隊に同行する移動支援艦。
    /// <see cref="SupplyRules"/>（面の補給線）とも別＝こちらは艦隊直付けの支援。乱数なし・決定論。
    /// 倍率・延伸量は基準値に加算/乗算して使う（実効値パターン・基準非破壊）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FleetTenderRules
    {
        /// <summary>
        /// 工作艦による野戦修理能力（0..1）＝工作艦数×修理設備。前線で損傷艦を直す吞み込み。工作艦が多く
        /// （tenderCount↑）設備が整う（repairFacilities↑）ほど高い。どちらかが0なら修理能力0（艦も設備も要る）。
        /// tenderCount は隻数を repairPerTender スケールで正規化（数で線形に積むが上限1）。
        /// </summary>
        public static float FieldRepairCapacity(float tenderCount, float repairFacilities, FleetTenderParams p)
        {
            float count = Mathf.Max(0f, tenderCount);
            float fac = Mathf.Clamp01(repairFacilities);
            float scaled = Mathf.Clamp01(count * p.repairPerTender); // 隻数を能力へ正規化
            return Mathf.Clamp01(scaled * fac);
        }

        public static float FieldRepairCapacity(float tenderCount, float repairFacilities)
            => FieldRepairCapacity(tenderCount, repairFacilities, FleetTenderParams.Default);

        /// <summary>
        /// 修理処理率（0..1）＝修理能力/(修理能力+損傷量)。損傷が修理能力に対して小さいほど高速に捌け、
        /// 損傷が膨大なら処理が追いつかず低下する＝飽和する修理パイプライン。損傷0なら満点（1）、能力0なら0。
        /// </summary>
        public static float RepairThroughput(float fieldRepairCapacity, float damageVolume, FleetTenderParams p)
        {
            float cap = Mathf.Clamp01(fieldRepairCapacity);
            float dmg = Mathf.Max(0f, damageVolume);
            float denom = cap + dmg;
            if (denom <= 0f) return 0f; // 能力も損傷も0＝処理対象なし
            return Mathf.Clamp01(cap / denom);
        }

        public static float RepairThroughput(float fieldRepairCapacity, float damageVolume)
            => RepairThroughput(fieldRepairCapacity, damageVolume, FleetTenderParams.Default);

        /// <summary>
        /// 洋上補給速度（0..1）＝補給艦数×移送設備。艦隊へ燃料弾薬を移す速さ。補給艦が多く（supplyShipCount↑）
        /// 移送設備が整う（transferEquipment↑）ほど速い。どちらかが0なら補給0。隻数は supplyPerShip で正規化。
        /// </summary>
        public static float ReplenishmentRate(float supplyShipCount, float transferEquipment, FleetTenderParams p)
        {
            float count = Mathf.Max(0f, supplyShipCount);
            float equip = Mathf.Clamp01(transferEquipment);
            float scaled = Mathf.Clamp01(count * p.supplyPerShip);
            return Mathf.Clamp01(scaled * equip);
        }

        public static float ReplenishmentRate(float supplyShipCount, float transferEquipment)
            => ReplenishmentRate(supplyShipCount, transferEquipment, FleetTenderParams.Default);

        /// <summary>
        /// 艦隊の継戦力延伸（0..sustainmentGain）＝補給×修理。補給で消耗を補い修理で損傷を回復する両輪が
        /// 揃ってはじめて前線に長く居座れる＝継戦力が延びる。どちらかが0なら延伸0（補給だけ・修理だけでは続かない）。
        /// </summary>
        public static float SustainmentExtension(float replenishmentRate, float fieldRepairCapacity, FleetTenderParams p)
        {
            float rep = Mathf.Clamp01(replenishmentRate);
            float cap = Mathf.Clamp01(fieldRepairCapacity);
            return Mathf.Clamp01(p.sustainmentGain * rep * cap);
        }

        public static float SustainmentExtension(float replenishmentRate, float fieldRepairCapacity)
            => SustainmentExtension(replenishmentRate, fieldRepairCapacity, FleetTenderParams.Default);

        /// <summary>
        /// 支援艦の脆弱性（0..1）＝(1-装甲)×(1-護衛)。支援艦は装甲が薄く（tenderArmor↓）護衛が手薄（escortCoverage↓）
        /// なほど狙われやすい。装甲か護衛のどちらかが満点なら脆弱性は大きく下がる＝守るほど安全。
        /// vulnerabilityScale で全体の被狙い度をスケール。
        /// </summary>
        public static float TenderVulnerability(float tenderArmor, float escortCoverage, FleetTenderParams p)
        {
            float armor = Mathf.Clamp01(tenderArmor);
            float escort = Mathf.Clamp01(escortCoverage);
            float exposed = (1f - armor) * (1f - escort);
            return Mathf.Clamp01(p.vulnerabilityScale * exposed);
        }

        public static float TenderVulnerability(float tenderArmor, float escortCoverage)
            => TenderVulnerability(tenderArmor, escortCoverage, FleetTenderParams.Default);

        /// <summary>
        /// 医療支援の充足（0..1）＝病院船容量/(容量+死傷者)。死傷者が容量に対して少ないほど充足し、死傷者が
        /// 膨大なら収容しきれず低下する＝飽和する医療キャパ。死傷者0なら満点（1）、容量0なら0。
        /// 充足が高いほど人員（軍属・士気）の回復が早まる橋渡し値。
        /// </summary>
        public static float MedicalSupport(float hospitalShipCapacity, float casualties, FleetTenderParams p)
        {
            float cap = Mathf.Clamp01(hospitalShipCapacity);
            float cas = Mathf.Max(0f, casualties);
            float denom = cap + cas;
            if (denom <= 0f) return 0f; // 容量も死傷者も0＝対象なし
            return Mathf.Clamp01(cap / denom);
        }

        public static float MedicalSupport(float hospitalShipCapacity, float casualties)
            => MedicalSupport(hospitalShipCapacity, casualties, FleetTenderParams.Default);

        /// <summary>
        /// 支援艦喪失の打撃（0..1）＝喪失割合×継戦依存。支援艦を多く失う（tenderLost↑）ほど、また継戦を支援に
        /// 依存している（sustainmentExtension↑）ほど痛い＝延伸していた継戦力が一気に剥がれる。喪失0または
        /// 支援に頼っていなければ打撃0。lossImpactWeight で打撃の効きをスケール。
        /// </summary>
        public static float SupportLossImpact(float tenderLost, float sustainmentExtension, FleetTenderParams p)
        {
            float lost = Mathf.Clamp01(tenderLost);
            float sust = Mathf.Clamp01(sustainmentExtension);
            return Mathf.Clamp01(p.lossImpactWeight * lost * sust);
        }

        public static float SupportLossImpact(float tenderLost, float sustainmentExtension)
            => SupportLossImpact(tenderLost, sustainmentExtension, FleetTenderParams.Default);

        /// <summary>
        /// 兵站的に支えられているか＝継戦力延伸（sustainmentExtension）が閾値以上なら true。補給と修理で
        /// 継戦が十分に延びていれば前線に居座れる、足りなければ消耗で退かざるを得ない。
        /// </summary>
        public static bool IsLogisticallySustained(float sustainmentExtension, float threshold)
        {
            float sust = Mathf.Clamp01(sustainmentExtension);
            float thr = Mathf.Clamp01(threshold);
            return sust >= thr;
        }

        /// <summary>兵站充足の判定（既定閾値 <see cref="FleetTenderParams.sustainThreshold"/>）。</summary>
        public static bool IsLogisticallySustained(float sustainmentExtension)
            => IsLogisticallySustained(sustainmentExtension, FleetTenderParams.Default.sustainThreshold);
    }
}
