using UnityEngine;

namespace Ginei
{
    /// <summary>作戦ドクトリンの調整係数（#1388）。</summary>
    public readonly struct OperationalDoctrineParams
    {
        /// <summary>運営能力が戦役効率に占める重み（残りが情報能力）。0..1。</summary>
        public readonly float operationWeight;
        /// <summary>運営能力が複雑な兵站を捌く最大効率（采配が満点のときの達成率）。</summary>
        public readonly float maxLogisticsOrchestration;
        /// <summary>情報能力が生の情報を作戦へ活かす最大率（敵情を読んで先手を打つ上限）。</summary>
        public readonly float maxIntelligenceExploitation;
        /// <summary>運営能力が多部隊間の協調へ返す最大ボーナス（梯団統御の上乗せ幅）。</summary>
        public readonly float maxCoordinationBonus;
        /// <summary>計画射程の基準値（運営・情報が満点のときに伸びる先読みの長さ）。</summary>
        public readonly float maxPlanningHorizon;

        public OperationalDoctrineParams(float operationWeight, float maxLogisticsOrchestration,
            float maxIntelligenceExploitation, float maxCoordinationBonus, float maxPlanningHorizon)
        {
            this.operationWeight = Mathf.Clamp01(operationWeight);
            this.maxLogisticsOrchestration = Mathf.Clamp01(maxLogisticsOrchestration);
            this.maxIntelligenceExploitation = Mathf.Clamp01(maxIntelligenceExploitation);
            this.maxCoordinationBonus = Mathf.Max(0f, maxCoordinationBonus);
            this.maxPlanningHorizon = Mathf.Max(0f, maxPlanningHorizon);
        }

        /// <summary>既定＝運営重み0.6・兵站采配上限1.0・情報活用上限1.0・協調ボーナス0.5・計画射程10。</summary>
        public static OperationalDoctrineParams Default
            => new OperationalDoctrineParams(0.6f, 1.0f, 1.0f, 0.5f, 10f);
    }

    /// <summary>
    /// 作戦ドクトリンの純ロジック（#1388・失敗の本質）。これまで未使用だった提督の
    /// operation（運営）・intelligence（情報）能力を、戦役（キャンペーン）レベルの効率へ初めて接続する＝
    /// 戦闘の強さ（attack/defense/mobility）でなく、作戦を立案し兵站を回し情報を活かす能力が長期の戦役を左右し、
    /// 優れた作戦ドクトリンは部隊間の協調スコアを高める（兵站・情報・協調を回す運営の差が長期戦の勝敗を分ける）。
    /// 分担：<see cref="OperationPlanRules"/> は一会戦ぶんの「作戦計画の質×準備時間」、
    /// <see cref="OperationalAptitudeRules"/> は提督の「戦闘類型への適性等級」を扱うのに対し、
    /// ここは未使用の operation/intelligence 能力を戦役（長期）効率へ写すだけ（協調スコア）。
    /// 能力値そのものは <see cref="AdmiralData"/>（operation/intelligence・read-only）から読む想定で書き換えない。
    /// 参謀補完済みの実効能力は <see cref="CommandStaffRules"/>（EffectiveOperation/EffectiveIntelligence）に委譲し、
    /// ここは「正規化済み能力(0..1)→戦役効率」の写像のみ。乱数なし決定論・全入力クランプ。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OperationalDoctrineRules
    {
        /// <summary>
        /// 戦役効率(0..1)＝運営能力 operation(0..1)×operationWeight ＋ 情報能力 intelligence(0..1)×(1−weight)。
        /// 戦闘の強さではなく、作戦・兵站・情報を回す力が長期の戦役を支える（既定は運営寄りの加重平均）。
        /// </summary>
        public static float CampaignEfficiency(float operation, float intelligence, OperationalDoctrineParams p)
        {
            float op = Mathf.Clamp01(operation);
            float intel = Mathf.Clamp01(intelligence);
            return op * p.operationWeight + intel * (1f - p.operationWeight);
        }

        public static float CampaignEfficiency(float operation, float intelligence)
            => CampaignEfficiency(operation, intelligence, OperationalDoctrineParams.Default);

        /// <summary>
        /// 兵站の采配(0..maxLogisticsOrchestration)＝運営能力が複雑な兵站をどれだけ捌けるか（補給・配備・整備）。
        /// 兵站が複雑(supplyComplexity 高)なほど運営能力が物を言い、能力が及ばないと采配は痩せる
        /// ＝実効采配 = 運営能力 ÷ (能力 ＋ 複雑さの不足分)。複雑さ0なら能力なりに満額、複雑さ1で能力が要る。
        /// </summary>
        public static float LogisticsOrchestration(float operation, float supplyComplexity, OperationalDoctrineParams p)
        {
            float op = Mathf.Clamp01(operation);
            float complexity = Mathf.Clamp01(supplyComplexity);
            // 複雑さに対して能力が足りるほど采配が満ちる。complexity=0 は満額、complexity→1 は能力依存。
            float denom = op + (1f - op) * complexity;
            float ratio = denom <= 0f ? 0f : op / Mathf.Max(denom, 0.0001f);
            return Mathf.Clamp01(ratio) * p.maxLogisticsOrchestration;
        }

        public static float LogisticsOrchestration(float operation, float supplyComplexity)
            => LogisticsOrchestration(operation, supplyComplexity, OperationalDoctrineParams.Default);

        /// <summary>
        /// 情報の活用(0..maxIntelligenceExploitation)＝情報能力が生の情報 rawIntel(0..1)をどれだけ作戦に活かすか。
        /// 生情報がいくらあっても、それを読んで先手を打つ情報能力がなければ宝の持ち腐れ＝活用 = 情報能力×生情報。
        /// </summary>
        public static float IntelligenceExploitation(float intelligence, float rawIntel, OperationalDoctrineParams p)
        {
            float intel = Mathf.Clamp01(intelligence);
            float raw = Mathf.Clamp01(rawIntel);
            return intel * raw * p.maxIntelligenceExploitation;
        }

        public static float IntelligenceExploitation(float intelligence, float rawIntel)
            => IntelligenceExploitation(intelligence, rawIntel, OperationalDoctrineParams.Default);

        /// <summary>
        /// 協調スコア(1..1+maxCoordinationBonus)＝運営能力が複数部隊間の連携を高める（梯団の統御）。
        /// 多部隊(formationCount 高)ほど協調の余地が大きく、運営能力がその分の連携を引き出す＝
        /// 1 ＋ 運営能力×部隊数×maxCoordinationBonus。単一部隊(count=0)なら協調の出番はなく1.0。
        /// 優れた作戦ドクトリンは部隊間の協調を高める、というテーマの中核の式。
        /// </summary>
        public static float CoordinationScore(float operation, float formationCount, OperationalDoctrineParams p)
        {
            float op = Mathf.Clamp01(operation);
            float count = Mathf.Clamp01(formationCount);
            return 1f + op * count * p.maxCoordinationBonus;
        }

        public static float CoordinationScore(float operation, float formationCount)
            => CoordinationScore(operation, formationCount, OperationalDoctrineParams.Default);

        /// <summary>
        /// 作戦ドクトリンの完成度(0..1)＝戦役効率(0..1)×方針の一貫性 doctrineCoherence(0..1)。
        /// 能力が高くても方針がぶれていれば（coherence 低）ドクトリンは完成せず、一貫した方針があってこそ能力が活きる。
        /// </summary>
        public static float DoctrineQuality(float campaignEfficiency, float doctrineCoherence)
        {
            return Mathf.Clamp01(campaignEfficiency) * Mathf.Clamp01(doctrineCoherence);
        }

        /// <summary>
        /// 計画の射程＝運営と情報が長期的な作戦計画の先読みをどれだけ伸ばすか（先を読んだ作戦）。
        /// 運営（兵站の段取り）と情報（敵情の先読み）の双方が必要＝両者の幾何平均×maxPlanningHorizon。
        /// 片方が0なら射程も0（兵站だけ・情報だけでは長期計画は立たない）。
        /// </summary>
        public static float PlanningHorizon(float operation, float intelligence, OperationalDoctrineParams p)
        {
            float op = Mathf.Clamp01(operation);
            float intel = Mathf.Clamp01(intelligence);
            // 幾何平均＝どちらか欠けると射程が伸びない（補完でなく両立を要求）。
            float synergy = Mathf.Sqrt(op * intel);
            return synergy * p.maxPlanningHorizon;
        }

        public static float PlanningHorizon(float operation, float intelligence)
            => PlanningHorizon(operation, intelligence, OperationalDoctrineParams.Default);

        /// <summary>
        /// 作戦のテンポ(0..1)＝協調スコア(1+)と情報活用 intelligenceExploitation(0..1)が作戦の速さを上げる（OODA的）。
        /// 部隊が協調し情報を活かすほど、判断→行動の循環が速く回る＝(協調の上乗せ分)×情報活用 を正規化して返す。
        /// 協調(coordinationScore=1=上乗せ無し)や情報活用0ならテンポは立たない。
        /// </summary>
        public static float OperationalTempo(float coordinationScore, float intelligenceExploitation, OperationalDoctrineParams p)
        {
            // 協調スコアの「1を超えた上乗せ分」を 0..1 へ正規化（maxCoordinationBonus が満ちると1）。
            float bonus = Mathf.Max(0f, coordinationScore - 1f);
            float coordNorm = p.maxCoordinationBonus <= 0f ? 0f : Mathf.Clamp01(bonus / p.maxCoordinationBonus);
            float exploit = Mathf.Clamp01(intelligenceExploitation);
            return Mathf.Clamp01(coordNorm * exploit);
        }

        public static float OperationalTempo(float coordinationScore, float intelligenceExploitation)
            => OperationalTempo(coordinationScore, intelligenceExploitation, OperationalDoctrineParams.Default);

        /// <summary>
        /// 優れた幕僚仕事の判定＝戦役効率と協調スコアの双方が閾値 threshold(0..1) を超えるか。
        /// 運営・情報に長け（campaignEfficiency 高）、かつ多部隊を協調させる（coordinationScore の上乗せ分が大きい）
        /// ときだけ true ＝戦闘の強さでなく、作戦・兵站・情報・協調を回す優れた幕僚仕事を体現する。
        /// </summary>
        public static bool IsCompetentStaffWork(float campaignEfficiency, float coordinationScore, float threshold, OperationalDoctrineParams p)
        {
            float th = Mathf.Clamp01(threshold);
            float eff = Mathf.Clamp01(campaignEfficiency);
            // 協調スコアの上乗せ分を 0..1 へ正規化して効率と同じ土俵で測る。
            float bonus = Mathf.Max(0f, coordinationScore - 1f);
            float coordNorm = p.maxCoordinationBonus <= 0f ? 0f : Mathf.Clamp01(bonus / p.maxCoordinationBonus);
            return eff >= th && coordNorm >= th;
        }

        public static bool IsCompetentStaffWork(float campaignEfficiency, float coordinationScore, float threshold)
            => IsCompetentStaffWork(campaignEfficiency, coordinationScore, threshold, OperationalDoctrineParams.Default);
    }
}
