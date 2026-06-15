using UnityEngine;

namespace Ginei
{
    /// <summary>作戦立案の調整係数。</summary>
    public readonly struct OperationPlanParams
    {
        /// <summary>立案の質が満点に達する準備時間（これ以上かけても伸びない）。</summary>
        public readonly float fullPrepTime;
        /// <summary>計画の質の差が会戦初期条件に与える最大優位（倍率の上乗せ幅）。</summary>
        public readonly float maxInitialAdvantage;
        /// <summary>計画の質が補給効率に返す最大ボーナス。</summary>
        public readonly float maxSupplyBonus;
        /// <summary>計画が接敵後に陳腐化する速さ（戦況テンポ1のときの per dt 劣化）。</summary>
        public readonly float decayRate;

        public OperationPlanParams(float fullPrepTime, float maxInitialAdvantage, float maxSupplyBonus, float decayRate)
        {
            this.fullPrepTime = Mathf.Max(0.0001f, fullPrepTime);
            this.maxInitialAdvantage = Mathf.Max(0f, maxInitialAdvantage);
            this.maxSupplyBonus = Mathf.Max(0f, maxSupplyBonus);
            this.decayRate = Mathf.Max(0f, decayRate);
        }

        /// <summary>既定＝準備満了10・初期優位0.2・補給ボーナス0.3・陳腐化0.1。</summary>
        public static OperationPlanParams Default => new OperationPlanParams(10f, 0.2f, 0.3f, 0.1f);
    }

    /// <summary>
    /// 作戦立案の純ロジック（参謀の `operation` 能力＝現状未使用の最初の使い道）。立案の質は
    /// 「立案者の運営能力×準備時間」で決まり、会戦の初期条件（配置・補給・予備隊）に優劣を与える。
    /// だが計画は接敵の瞬間から陳腐化していく（「作戦計画は敵主力との接触に耐えない」モルトケ）＝
    /// 立案の質は開戦時の貯金であり、長い会戦では現場の指揮が物を言う。能力の参謀補完は
    /// <see cref="CommandStaffRules"/>（EffectiveOperation）へ委譲し、ここは計画→効果の写像のみ。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class OperationPlanRules
    {
        /// <summary>
        /// 計画の質（0..1）＝運営能力 operation(0..100、`AdmiralData.MaxStatValue` 基準)×準備充足。
        /// 準備時間は fullPrepTime で満了（それ以上は伸びない＝完璧な計画は存在しない）。
        /// </summary>
        public static float PlanQuality(float operation, float prepTime, OperationPlanParams p)
        {
            float ability = Mathf.Clamp01(operation / AdmiralData.MaxStatValue);
            float prep = Mathf.Clamp01(Mathf.Max(0f, prepTime) / p.fullPrepTime);
            return ability * prep;
        }

        public static float PlanQuality(float operation, float prepTime)
            => PlanQuality(operation, prepTime, OperationPlanParams.Default);

        /// <summary>
        /// 会戦初期条件の倍率（1±maxInitialAdvantage）。自他の計画の質の差を [-1,1]→倍率へ写す＝
        /// 良い計画は相手の杜撰さの分だけ効く（双方が完璧なら互角＝1.0）。基準値に掛けて使う。
        /// </summary>
        public static float InitialAdvantageFactor(float ownQuality, float enemyQuality, OperationPlanParams p)
        {
            float diff = Mathf.Clamp(Mathf.Clamp01(ownQuality) - Mathf.Clamp01(enemyQuality), -1f, 1f);
            return 1f + diff * p.maxInitialAdvantage;
        }

        public static float InitialAdvantageFactor(float ownQuality, float enemyQuality)
            => InitialAdvantageFactor(ownQuality, enemyQuality, OperationPlanParams.Default);

        /// <summary>補給効率倍率（1..1+maxSupplyBonus）＝計画の質に比例。輜重は事前計画が物を言う。</summary>
        public static float SupplyEfficiencyFactor(float quality, OperationPlanParams p)
        {
            return 1f + Mathf.Clamp01(quality) * p.maxSupplyBonus;
        }

        public static float SupplyEfficiencyFactor(float quality)
            => SupplyEfficiencyFactor(quality, OperationPlanParams.Default);

        /// <summary>
        /// 接敵後の計画残存値（0..1）。接敵からの経過時間×戦況テンポ tempo(0..1)×劣化率で痩せる＝
        /// 「計画は敵との接触に耐えない」。静的な包囲戦（tempo≈0）なら計画は長持ちする。
        /// </summary>
        public static float DecayedQuality(float quality, float timeSinceContact, float tempo, OperationPlanParams p)
        {
            float decay = p.decayRate * Mathf.Clamp01(tempo) * Mathf.Max(0f, timeSinceContact);
            return Mathf.Max(0f, Mathf.Clamp01(quality) - decay);
        }

        public static float DecayedQuality(float quality, float timeSinceContact, float tempo)
            => DecayedQuality(quality, timeSinceContact, tempo, OperationPlanParams.Default);
    }
}
