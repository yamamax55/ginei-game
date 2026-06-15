using UnityEngine;

namespace Ginei
{
    /// <summary>開戦事由（戦争目標）。征服＝領土併合／従属＝属国化／解放＝被占領地の奪還・独立／懲罰＝賠償・面子。</summary>
    public enum CasusBelli
    {
        征服,
        従属,
        解放,
        懲罰,
    }

    /// <summary>
    /// 戦争と講和の純ロジック（外交EPIC #189・DIP-3 #192・唯一の窓口）。
    /// <b>厭戦(<see cref="WarWeariness"/>)</b>＝長期化・損害で高まる戦争疲れ／<b>開戦事由の正当性(<see cref="GoalLegitimacy"/>)</b>＝
    /// 解放は正当・征服は不当が基準（関係値で増減）／<b>講和受諾度(<see cref="PeaceAcceptance"/>)</b>＝戦況不利・厭戦で講和に傾く／
    /// <b>賠償(<see cref="Reparations"/>)</b>＝戦況優位ぶんの取り立て。いずれも 0..1 に丸める。
    /// 細かい数値管理は持たず（タイクン化回避）、高位の決断（戦い続けるか・手打ちにするか）と帰結を扱う。test-first。
    /// </summary>
    public static class WarGoalRules
    {
        /// <summary>戦争・講和の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct WarGoalParams
        {
            public readonly float turnsWearinessRate;   // 1ターンあたりの厭戦上昇
            public readonly float casualtyWearinessWeight; // 損害(0..1)が厭戦へ寄与する重み
            public readonly float wearinessCap;          // 厭戦の上限（1.0 で完全厭戦）
            public readonly float warScoreWeight;        // 戦況スコアが講和受諾へ寄与する重み
            public readonly float wearinessWeight;       // 厭戦が講和受諾へ寄与する重み
            public readonly float legitimacyOpinionWeight; // 関係値が正当性へ寄与する重み（×opinion レンジ）

            public WarGoalParams(float turnsWearinessRate, float casualtyWearinessWeight, float wearinessCap,
                float warScoreWeight, float wearinessWeight, float legitimacyOpinionWeight)
            {
                this.turnsWearinessRate = Mathf.Max(0f, turnsWearinessRate);
                this.casualtyWearinessWeight = Mathf.Max(0f, casualtyWearinessWeight);
                this.wearinessCap = Mathf.Clamp01(wearinessCap);
                this.warScoreWeight = Mathf.Max(0f, warScoreWeight);
                this.wearinessWeight = Mathf.Max(0f, wearinessWeight);
                this.legitimacyOpinionWeight = Mathf.Max(0f, legitimacyOpinionWeight);
            }

            /// <summary>既定＝ターン厭戦0.02/損害重み0.6/上限1.0・戦況0.5/厭戦0.5・正当性の関係値重み0.3。</summary>
            public static WarGoalParams Default => new WarGoalParams(
                0.02f, 0.6f, 1f,
                0.5f, 0.5f, 0.3f);
        }

        /// <summary>事由ごとの正当性の基準値（征服＝不当〜解放＝正当）。</summary>
        public const float ConquestBaseLegitimacy = 0.2f;
        public const float VassalizeBaseLegitimacy = 0.35f;
        public const float LiberationBaseLegitimacy = 0.8f;
        public const float PunishBaseLegitimacy = 0.5f;

        /// <summary>0..1 に丸める。</summary>
        public static float Clamp01(float v) => Mathf.Clamp01(v);

        /// <summary>
        /// 厭戦（戦争疲れ 0..1）。戦争ターン数（turnsAtWar≥0）×ターン率＋損害(casualties 0..1)×重み。
        /// 上限 wearinessCap で頭打ち（長期化・大損害ほど高い）。
        /// </summary>
        public static float WarWeariness(int turnsAtWar, float casualties, WarGoalParams p)
        {
            int turns = Mathf.Max(0, turnsAtWar);
            float cas = Mathf.Clamp01(casualties);
            float w = turns * p.turnsWearinessRate + cas * p.casualtyWearinessWeight;
            return Mathf.Clamp(w, 0f, p.wearinessCap);
        }

        /// <summary>事由ごとの正当性の基準値（関係値抜き）。</summary>
        public static float BaseLegitimacy(CasusBelli cb)
        {
            switch (cb)
            {
                case CasusBelli.征服: return ConquestBaseLegitimacy;
                case CasusBelli.従属: return VassalizeBaseLegitimacy;
                case CasusBelli.解放: return LiberationBaseLegitimacy;
                case CasusBelli.懲罰: return PunishBaseLegitimacy;
                default: return PunishBaseLegitimacy;
            }
        }

        /// <summary>
        /// 開戦事由の正当性（0..1）。事由の基準値に、相手への関係値(opinion -100..100)を加味
        /// （険悪なほど正当化されやすい＝opinion 負で正当性↑）。
        /// </summary>
        public static float GoalLegitimacy(CasusBelli cb, float opinion, WarGoalParams p)
        {
            float baseVal = BaseLegitimacy(cb);
            // opinion を [-1,1] に正規化し、負（険悪）ほど正当性を押し上げる。
            float norm = Mathf.Clamp(opinion, -100f, 100f) / 100f;
            float v = baseVal - norm * p.legitimacyOpinionWeight;
            return Clamp01(v);
        }

        /// <summary>関係値抜きの正当性（既定 Params で解決）。</summary>
        public static float GoalLegitimacy(CasusBelli cb, float opinion)
            => GoalLegitimacy(cb, opinion, WarGoalParams.Default);

        /// <summary>
        /// 講和受諾度（0..1）。戦況スコア(warScore -1..1＝負＝劣勢)が低いほど・厭戦(weariness 0..1)が高いほど
        /// 講和に傾く。劣勢ぶん((1-warScore)/2)と厭戦の加重和を正規化。
        /// </summary>
        public static float PeaceAcceptance(float warScore, float weariness, WarGoalParams p)
        {
            float score = Mathf.Clamp(warScore, -1f, 1f);
            float disadvantage = (1f - score) * 0.5f; // 劣勢ぶん 0..1（warScore=-1 で 1）
            float wear = Mathf.Clamp01(weariness);
            float weightSum = p.warScoreWeight + p.wearinessWeight;
            if (weightSum <= 0f) return 0f;
            float v = (disadvantage * p.warScoreWeight + wear * p.wearinessWeight) / weightSum;
            return Clamp01(v);
        }

        /// <summary>
        /// 賠償（取り立て 0..1）。戦況優位ぶん（warScore 正）のみを賠償率にする。
        /// warScore≤0（互角・劣勢）は賠償なし＝0、warScore=1 で最大 1。
        /// </summary>
        public static float Reparations(float warScore)
        {
            float score = Mathf.Clamp(warScore, -1f, 1f);
            return Clamp01(Mathf.Max(0f, score));
        }
    }
}
