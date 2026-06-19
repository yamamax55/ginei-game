using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 前哨基地（辺境の小規模拠点・補給拠点）の調整係数。要衝近接・資源・辺境到達から位置価値を量り、
    /// 適正な駐留規模と維持コスト、孤立拠点の脆弱性、補給途絶リスク、前進作戦への支援力を算出する。
    /// </summary>
    public readonly struct OutpostParams
    {
        /// <summary>位置価値に占める要衝近接の重み（0..1）。</summary>
        public readonly float chokepointWeight;
        /// <summary>位置価値に占める資源アクセスの重み（0..1）。</summary>
        public readonly float resourceWeight;
        /// <summary>位置価値に占める辺境到達（戦略的リーチ）の重み（0..1）。</summary>
        public readonly float frontierWeight;
        /// <summary>適正駐留規模の最大スケール（0..1・位置価値×戦力が満点のときの上限）。</summary>
        public readonly float garrisonScale;
        /// <summary>維持コストの基礎（0..1・近傍に最小駐留を置く下地）。</summary>
        public readonly float baseMaintenance;
        /// <summary>孤立による維持コストの割増重み（0..1・遠隔ほど高い）。</summary>
        public readonly float isolationCostWeight;
        /// <summary>孤立拠点の脆弱性の最大スケール（0..1）。</summary>
        public readonly float vulnerabilityScale;
        /// <summary>補給途絶リスクの最大スケール（0..1・補給線長×敵活動が満点のとき）。</summary>
        public readonly float interdictionScale;
        /// <summary>前進作戦への支援力の最大スケール（0..1）。</summary>
        public readonly float supportScale;
        /// <summary>正味価値で維持コストを引く重み（0..1）。</summary>
        public readonly float costPenaltyWeight;
        /// <summary>正味価値で脆弱性を引く重み（0..1）。</summary>
        public readonly float vulnerabilityPenaltyWeight;
        /// <summary>増強すべきと判断する既定の閾値（-1..1・正味価値と位置の合成がこれ以上）。</summary>
        public readonly float reinforceThreshold;

        public OutpostParams(float chokepointWeight, float resourceWeight, float frontierWeight,
            float garrisonScale, float baseMaintenance, float isolationCostWeight,
            float vulnerabilityScale, float interdictionScale, float supportScale,
            float costPenaltyWeight, float vulnerabilityPenaltyWeight, float reinforceThreshold)
        {
            this.chokepointWeight = Mathf.Clamp01(chokepointWeight);
            this.resourceWeight = Mathf.Clamp01(resourceWeight);
            this.frontierWeight = Mathf.Clamp01(frontierWeight);
            this.garrisonScale = Mathf.Clamp01(garrisonScale);
            this.baseMaintenance = Mathf.Clamp01(baseMaintenance);
            this.isolationCostWeight = Mathf.Clamp01(isolationCostWeight);
            this.vulnerabilityScale = Mathf.Clamp01(vulnerabilityScale);
            this.interdictionScale = Mathf.Clamp01(interdictionScale);
            this.supportScale = Mathf.Clamp01(supportScale);
            this.costPenaltyWeight = Mathf.Clamp01(costPenaltyWeight);
            this.vulnerabilityPenaltyWeight = Mathf.Clamp01(vulnerabilityPenaltyWeight);
            this.reinforceThreshold = Mathf.Clamp(reinforceThreshold, -1f, 1f);
        }

        /// <summary>
        /// 既定＝要衝0.5/資源0.3/辺境0.2・駐留スケール0.8・維持基礎0.2・孤立割増0.6・脆弱0.9・途絶0.8・支援0.9・
        /// コスト罰0.6・脆弱罰0.5・増強閾値0.2。
        /// </summary>
        public static OutpostParams Default =>
            new OutpostParams(0.5f, 0.3f, 0.2f, 0.8f, 0.2f, 0.6f, 0.9f, 0.8f, 0.9f, 0.6f, 0.5f, 0.2f);
    }

    /// <summary>
    /// 前哨基地（辺境の小規模拠点・補給拠点）の運営の純ロジック。基地の<b>戦略的位置価値</b>（要衝近接×資源×
    /// 辺境到達）から適正な<b>駐留規模</b>と<b>維持コスト</b>を量り、孤立した拠点の<b>脆弱性</b>と<b>補給途絶リスク</b>、
    /// 前進拠点としての<b>作戦支援力</b>を算出し、<b>正味価値</b>から放棄か増強かを判断する。
    /// <see cref="DepotRules"/>（前進補給基地＝補給基点を前方へ移して到達限界を延伸する倉庫）とは別＝
    /// こちらは辺境に常駐する拠点そのものの運営（位置価値・駐留・孤立脆弱・放棄/増強）。
    /// <see cref="ChokepointValueRules"/>（回廊要衝の価値）は位置価値の入力（要衝近接）として渡せる。
    /// 乱数なし・決定論。倍率・コストは基準値に加算/乗算して使う（実効値パターン・基準非破壊）。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OutpostRules
    {
        /// <summary>
        /// 基地の戦略的位置価値（0..1）＝要衝近接×重み＋資源アクセス×重み＋辺境到達×重みの加重和。
        /// 重みは正規化して合計1に揃える（要衝の近くで資源があり辺境へ手が届くほど価値が高い）。
        /// </summary>
        public static float StrategicPosition(float chokepointProximity, float resourceAccess, float frontierReach, OutpostParams p)
        {
            float choke = Mathf.Clamp01(chokepointProximity);
            float res = Mathf.Clamp01(resourceAccess);
            float reach = Mathf.Clamp01(frontierReach);
            float wSum = p.chokepointWeight + p.resourceWeight + p.frontierWeight;
            if (wSum <= 0f) return 0f; // 全重み0なら価値0（ゼロ割回避）
            float raw = choke * p.chokepointWeight + res * p.resourceWeight + reach * p.frontierWeight;
            return Mathf.Clamp01(raw / wSum);
        }

        public static float StrategicPosition(float chokepointProximity, float resourceAccess, float frontierReach)
            => StrategicPosition(chokepointProximity, resourceAccess, frontierReach, OutpostParams.Default);

        /// <summary>
        /// 適正な駐留規模（0..garrisonScale）＝位置価値×利用可能戦力。価値の高い拠点に戦力があるほど厚く守る。
        /// どちらかが0なら駐留0（価値の無い拠点／割ける兵が無い拠点は駐留しない）。
        /// </summary>
        public static float GarrisonSize(float strategicPosition, float availableForces, OutpostParams p)
        {
            float pos = Mathf.Clamp01(strategicPosition);
            float forces = Mathf.Clamp01(availableForces);
            return Mathf.Clamp01(p.garrisonScale * pos * forces);
        }

        public static float GarrisonSize(float strategicPosition, float availableForces)
            => GarrisonSize(strategicPosition, availableForces, OutpostParams.Default);

        /// <summary>
        /// 拠点の維持コスト（baseMaintenance..1）＝駐留規模に比例し、孤立度（isolation↑＝本国から遠い）ほど割増。
        /// 駐留が大きく遠隔の拠点ほど高い＝辺境の大守備隊は金がかかる。近傍（孤立0）なら基礎×駐留のみ。
        /// </summary>
        public static float MaintenanceCost(float garrisonSize, float isolation, OutpostParams p)
        {
            float gar = Mathf.Clamp01(garrisonSize);
            float iso = Mathf.Clamp01(isolation);
            // 駐留に比例した基礎コスト＋孤立による割増（孤立ほど補給/輸送が嵩む）
            float baseCost = p.baseMaintenance + (1f - p.baseMaintenance) * gar;
            float isolated = baseCost * (1f + p.isolationCostWeight * iso);
            return Mathf.Clamp01(isolated);
        }

        public static float MaintenanceCost(float garrisonSize, float isolation)
            => MaintenanceCost(garrisonSize, isolation, OutpostParams.Default);

        /// <summary>
        /// 孤立した拠点の脆弱性（0..vulnerabilityScale）＝孤立度×(1−駐留規模)×救援距離。
        /// 本国から遠く（isolation↑）守備が薄く（garrison↓）救援が遠い（reliefDistance↑）ほど脆い＝各個撃破の的。
        /// 駐留が満点なら脆弱性0（十分守れば孤立しても落ちない）。孤立0なら脆弱性0（後方の拠点は安全）。
        /// </summary>
        public static float IsolationVulnerability(float isolation, float garrisonSize, float reliefDistance, OutpostParams p)
        {
            float iso = Mathf.Clamp01(isolation);
            float gar = Mathf.Clamp01(garrisonSize);
            float relief = Mathf.Clamp01(reliefDistance);
            float raw = iso * (1f - gar) * relief;
            return Mathf.Clamp01(p.vulnerabilityScale * raw);
        }

        public static float IsolationVulnerability(float isolation, float garrisonSize, float reliefDistance)
            => IsolationVulnerability(isolation, garrisonSize, reliefDistance, OutpostParams.Default);

        /// <summary>
        /// 補給途絶リスク（0..interdictionScale）＝補給線長×敵の活動。補給線が長く（supplyLineLength↑）敵の活動が
        /// 活発（enemyActivity↑）なほど高い＝伸びた補給線が通商破壊で断たれる。どちらかが0ならリスク0。
        /// </summary>
        public static float SupplyInterdictionRisk(float supplyLineLength, float enemyActivity, OutpostParams p)
        {
            float length = Mathf.Clamp01(supplyLineLength);
            float enemy = Mathf.Clamp01(enemyActivity);
            return Mathf.Clamp01(p.interdictionScale * length * enemy);
        }

        public static float SupplyInterdictionRisk(float supplyLineLength, float enemyActivity)
            => SupplyInterdictionRisk(supplyLineLength, enemyActivity, OutpostParams.Default);

        /// <summary>
        /// 前進拠点としての作戦支援力（0..supportScale）＝位置価値×駐留規模。良い位置に十分な戦力を置くほど、
        /// そこを足場に前方作戦を支えられる（出撃基点・補給中継・哨戒）。どちらかが0なら支援0（足場にならない）。
        /// </summary>
        public static float OperationalSupport(float strategicPosition, float garrisonSize, OutpostParams p)
        {
            float pos = Mathf.Clamp01(strategicPosition);
            float gar = Mathf.Clamp01(garrisonSize);
            return Mathf.Clamp01(p.supportScale * pos * gar);
        }

        public static float OperationalSupport(float strategicPosition, float garrisonSize)
            => OperationalSupport(strategicPosition, garrisonSize, OutpostParams.Default);

        /// <summary>
        /// 前哨の正味価値（-1..1）＝作戦支援力 − 維持コスト×罰重み − 脆弱性×罰重み。
        /// 足場として役立つ支援が、維持の負担と落ちる危険を上回れば正＝置く価値がある／下回れば負＝放棄を促す。
        /// </summary>
        public static float OutpostValue(float operationalSupport, float maintenanceCost, float isolationVulnerability, OutpostParams p)
        {
            float support = Mathf.Clamp01(operationalSupport);
            float cost = Mathf.Clamp01(maintenanceCost);
            float vuln = Mathf.Clamp01(isolationVulnerability);
            float net = support - cost * p.costPenaltyWeight - vuln * p.vulnerabilityPenaltyWeight;
            return Mathf.Clamp(net, -1f, 1f);
        }

        public static float OutpostValue(float operationalSupport, float maintenanceCost, float isolationVulnerability)
            => OutpostValue(operationalSupport, maintenanceCost, isolationVulnerability, OutpostParams.Default);

        /// <summary>
        /// 拠点を増強すべきか＝正味価値（outpostValue・-1..1）と位置価値（strategicPosition・0..1）の合成が閾値以上なら true。
        /// 位置価値は 0..1 を -1..1 へ写して合成（要衝なら多少コストがかさんでも増強の判断に傾く）。
        /// 正味価値が負でも位置が極めて良ければ増強が選ばれうる＝放棄か増強かの分岐点。
        /// </summary>
        public static bool ShouldReinforce(float outpostValue, float strategicPosition, float threshold)
        {
            float value = Mathf.Clamp(outpostValue, -1f, 1f);
            float pos = Mathf.Clamp01(strategicPosition);
            float thr = Mathf.Clamp(threshold, -1f, 1f);
            // 位置価値 0..1 を -1..1 へ写し、正味価値と平均して合成スコアを出す
            float posSigned = pos * 2f - 1f;
            float score = (value + posSigned) * 0.5f;
            return score >= thr;
        }

        /// <summary>拠点の増強判定（既定閾値 <see cref="OutpostParams.reinforceThreshold"/>）。</summary>
        public static bool ShouldReinforce(float outpostValue, float strategicPosition)
            => ShouldReinforce(outpostValue, strategicPosition, OutpostParams.Default.reinforceThreshold);
    }
}
