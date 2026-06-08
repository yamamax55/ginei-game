using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 内政の数値解決（#109 P-1/P-2 最小ループ・純ロジック test-first）。攻城/戦略から呼ばれる唯一の窓口。
    /// 安定度は「目標値へ時間で収束」する：目標＝基準＋思想一致(±)−戦時−補給不足−(未統合ぶんの占領不満)。
    /// 占領直後は <see cref="Province.integration"/>=0 で占領不満が最大→時間で integration↑→安定が回復する。
    /// 産出は安定度に比例（支配≠即産出）。低安定で反乱リスク。
    /// 建設マイクロ・通貨経済は持たない（タイクン回避・EPIC #109 方針）。調整値は const に集約。
    /// </summary>
    public static class GovernanceRules
    {
        // --- 調整値（マジックナンバー禁止＝const に集約） ---
        public const float BaseStability = 50f;             // 中立の基準安定度
        public const float IdeologyMatchBonus = 25f;        // 住民思想と統治勢力が一致
        public const float IdeologyMismatchPenalty = 20f;   // 不一致
        public const float WarPenalty = 15f;                // 戦時（前線・交戦中）
        public const float SupplyPenalty = 20f;             // 補給不足
        public const float OccupationUnrest = 40f;          // 未統合(integration=0)時の占領不満の最大
        public const float IntegrationRate = 0.04f;         // 統合の速さ（/戦略秒。0→1 に約25秒）
        public const float StabilitySpeed = 6f;             // 安定度が目標へ寄る速さ（/戦略秒）
        public const float RebelThreshold = 25f;            // これ未満で反乱リスク域
        public const float OccupiedInitialStability = 15f;  // 占領直後の初期安定度
        public const float MinOutputFactor = 0.3f;          // 安定0でも最低限の産出
        public const float MaxStability = 100f;

        /// <summary>
        /// 安定度の目標値（収束先）を算出する純関数。プリミティブのみ＝テスト容易。
        /// </summary>
        /// <param name="integration">占領統合度(0..1)。低いほど占領不満で目標が下がる。</param>
        /// <param name="ideologyMod">思想一致の補正（一致=+、不一致=−、不明=0。<see cref="IdeologyModifier"/>）。</param>
        public static float EquilibriumStability(float integration, float ideologyMod, bool supplyOk, bool atWar)
        {
            float target = BaseStability + ideologyMod;
            if (!supplyOk) target -= SupplyPenalty;
            if (atWar) target -= WarPenalty;
            // 未統合ぶんの占領不満（integration=1 で消える）
            target -= (1f - Mathf.Clamp01(integration)) * OccupationUnrest;
            return Mathf.Clamp(target, 0f, MaxStability);
        }

        /// <summary>
        /// 1tick の内政更新：統合度を進め、安定度を目標へ寄せる（戦略時間に dt 比例＝timeScale 追従）。
        /// </summary>
        public static void Tick(Province p, FactionData ownerData, bool supplyOk, bool atWar, float deltaTime)
        {
            if (p == null || deltaTime <= 0f) return;

            // 占領地の統合を時間で進める（自国領は既に 1）
            p.integration = Mathf.Clamp01(p.integration + IntegrationRate * deltaTime);

            float mod = IdeologyModifier(ownerData, p.nativeIdeology);
            float target = EquilibriumStability(p.integration, mod, supplyOk, atWar);
            p.stability = Mathf.MoveTowards(p.stability, target, StabilitySpeed * deltaTime);
            p.stability = Mathf.Clamp(p.stability, 0f, MaxStability);
        }

        /// <summary>占領で所有が変わった時：統合をリセットし不安定化する（攻城 ApplySiegeResult から呼ぶ想定）。</summary>
        public static void OnOccupied(Province p)
        {
            if (p == null) return;
            p.integration = 0f;
            p.stability = OccupiedInitialStability;
        }

        /// <summary>産出倍率（安定度に比例＝支配≠即産出。低安定で減る）。MinOutputFactor..1。</summary>
        public static float OutputFactor(Province p)
        {
            if (p == null) return 0f;
            float t = Mathf.Clamp01(p.stability / MaxStability);
            return Mathf.Lerp(MinOutputFactor, 1f, t);
        }

        /// <summary>反乱リスク域か（安定度がしきい値未満）。</summary>
        public static bool IsUnrest(Province p) => p != null && p.stability < RebelThreshold;

        /// <summary>反乱圧（0..1。RebelThreshold で 0、安定0 で 1）。低安定ほど高い。</summary>
        public static float RebelPressure(Province p)
        {
            if (p == null) return 0f;
            if (p.stability >= RebelThreshold) return 0f;
            return Mathf.Clamp01((RebelThreshold - p.stability) / RebelThreshold);
        }

        /// <summary>
        /// 思想一致の補正値：住民思想(nativeIdeology)と統治勢力(ownerData.ideology)を比較。
        /// 一致＝+IdeologyMatchBonus／明確に不一致＝−IdeologyMismatchPenalty／どちらか不明（空）＝0（中立）。
        /// </summary>
        public static float IdeologyModifier(FactionData ownerData, string nativeIdeology)
        {
            if (ownerData == null || string.IsNullOrEmpty(ownerData.ideology) || string.IsNullOrEmpty(nativeIdeology))
                return 0f; // 不明＝中立
            return ownerData.ideology == nativeIdeology ? IdeologyMatchBonus : -IdeologyMismatchPenalty;
        }
    }
}
