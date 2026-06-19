using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 秘密工作（否認可能な非公然作戦）の純ロジック。政府が「我が国は関与していない」と
    /// しらを切れる状態を保ったまま行う隠密作戦＝クーデター支援・反政府勢力の扶植・偽情報・
    /// 経済妨害。成功確率、否認可能性、暴露リスク、暴露時の外交的反動（ブローバック）、
    /// 費用対効果、そして公然衝突への転化圧までを解決する。
    ///
    /// 分担（重複実装しない）：
    /// - <see cref="SabotageOpsRules"/>（工作員による物理的破壊工作＝施設破壊）とは別＝
    ///   こちらは政治的な隠密作戦の上位レイヤー（政権転覆・世論操作・経済攪乱の計画と帰結）。
    /// - <see cref="AssassinationRules"/>（要人暗殺）とは別＝特定個人の排除でなく
    ///   政治的状況そのものを否認可能に動かす作戦。
    /// 盤面非依存の plain 引数・乱数なし・実効値パターン（基準値非破壊）・値の clamp 徹底。test-first。
    /// </summary>
    public static class CovertActionRules
    {
        /// <summary>秘密工作の係数。トップレベル readonly struct・全 Clamp。</summary>
        public readonly struct CovertActionParams
        {
            /// <summary>中継層1層あたりの否認可能性の減衰指数（層が増えるほど痕跡を辿りにくい）。</summary>
            public readonly float layerDecay;
            /// <summary>痕跡の大きさが否認可能性を削る利き。</summary>
            public readonly float footprintScale;
            /// <summary>敵防諜が暴露リスクを押し上げる利き。</summary>
            public readonly float counterIntelScale;
            /// <summary>暴露が外交的反動に波及する利き。</summary>
            public readonly float blowbackScale;
            /// <summary>反動が公然衝突へ転化する圧の利き。</summary>
            public readonly float escalationScale;
            /// <summary>費用対効果で反動を差し引く重み（反動を嫌う度合い）。</summary>
            public readonly float blowbackPenalty;
            /// <summary>しらを切り通せるとみなす否認可能性の閾値（0..1）。</summary>
            public readonly float denialThreshold;
            /// <summary>実行する価値があるとみなす費用対効果の閾値（-1..1）。</summary>
            public readonly float worthThreshold;

            public CovertActionParams(
                float layerDecay,
                float footprintScale,
                float counterIntelScale,
                float blowbackScale,
                float escalationScale,
                float blowbackPenalty,
                float denialThreshold,
                float worthThreshold)
            {
                this.layerDecay = Mathf.Clamp01(layerDecay);
                this.footprintScale = Mathf.Max(0f, footprintScale);
                this.counterIntelScale = Mathf.Max(0f, counterIntelScale);
                this.blowbackScale = Mathf.Max(0f, blowbackScale);
                this.escalationScale = Mathf.Max(0f, escalationScale);
                this.blowbackPenalty = Mathf.Max(0f, blowbackPenalty);
                this.denialThreshold = Mathf.Clamp01(denialThreshold);
                this.worthThreshold = Mathf.Clamp(worthThreshold, -1f, 1f);
            }

            /// <summary>
            /// 既定＝層減衰0.6・痕跡利き1.0・防諜利き1.2・反動利き1.0・転化利き0.8・
            /// 反動重み1.0・否認閾値0.5・採算閾値0.1。
            /// </summary>
            public static CovertActionParams Default => new CovertActionParams(
                layerDecay: 0.6f,
                footprintScale: 1.0f,
                counterIntelScale: 1.2f,
                blowbackScale: 1.0f,
                escalationScale: 0.8f,
                blowbackPenalty: 1.0f,
                denialThreshold: 0.5f,
                worthThreshold: 0.1f);
        }

        /// <summary>
        /// 作戦の成功度（0..1）。投入資源×現地協力 ÷（現地協力＋標的の抵抗力）＝
        /// 現地に味方が多く標的の抵抗が弱いほど成功し、投入資源で底上げされる。
        /// </summary>
        public static float OperationSuccess(float resourcesCommitted, float localSupport, float targetResilience, CovertActionParams p)
        {
            float res = Mathf.Clamp01(resourcesCommitted);
            float sup = Mathf.Clamp01(localSupport);
            float resi = Mathf.Clamp01(targetResilience);
            float denom = sup + resi;
            if (denom <= 0f) return 0f;
            return Mathf.Clamp01(res * sup / denom);
        }

        public static float OperationSuccess(float resourcesCommitted, float localSupport, float targetResilience)
            => OperationSuccess(resourcesCommitted, localSupport, targetResilience, CovertActionParams.Default);

        /// <summary>
        /// 否認可能性（0..1）。中継層が多いほど痕跡を辿りにくく（層で Pow 減衰）、
        /// 痕跡が小さいほど高い。隔てる手駒を重ね、足跡を消すほどしらを切れる。
        /// </summary>
        public static float Deniability(int intermediaryLayers, float footprintSize, CovertActionParams p)
        {
            int layers = intermediaryLayers < 0 ? 0 : intermediaryLayers;
            float fp = Mathf.Clamp01(footprintSize);
            // 層が増えるほど 1 へ近づく（layerDecay^layers ぶんだけ「辿れる確率」が残る）。
            float traceable = Mathf.Pow(p.layerDecay, layers);
            float layerDeniability = 1f - traceable;
            float footprintDeniability = Mathf.Clamp01(1f - fp * p.footprintScale);
            return Mathf.Clamp01(layerDeniability * footprintDeniability);
        }

        public static float Deniability(int intermediaryLayers, float footprintSize)
            => Deniability(intermediaryLayers, footprintSize, CovertActionParams.Default);

        /// <summary>
        /// 暴露リスク（0..1）。痕跡×敵防諜×(1-否認可能性)＝
        /// 足跡が大きく敵の防諜が鋭く、しらを切れないほど露見しやすい。
        /// </summary>
        public static float ExposureRisk(float footprintSize, float enemyCounterIntel, float deniability, CovertActionParams p)
        {
            float fp = Mathf.Clamp01(footprintSize);
            float ci = Mathf.Clamp01(enemyCounterIntel) * p.counterIntelScale;
            float den = Mathf.Clamp01(deniability);
            return Mathf.Clamp01(fp * ci * (1f - den));
        }

        public static float ExposureRisk(float footprintSize, float enemyCounterIntel, float deniability)
            => ExposureRisk(footprintSize, enemyCounterIntel, deniability, CovertActionParams.Default);

        /// <summary>
        /// ブローバック＝暴露時の外交的反動（0..1）。暴露リスク×作戦の過激さ×利き＝
        /// 露見しやすく作戦が過激なほど評判失墜・報復が重い（やり過ぎは身を滅ぼす）。
        /// </summary>
        public static float Blowback(float exposureRisk, float operationAggressiveness, CovertActionParams p)
        {
            float exp = Mathf.Clamp01(exposureRisk);
            float agg = Mathf.Clamp01(operationAggressiveness);
            return Mathf.Clamp01(exp * agg * p.blowbackScale);
        }

        public static float Blowback(float exposureRisk, float operationAggressiveness)
            => Blowback(exposureRisk, operationAggressiveness, CovertActionParams.Default);

        /// <summary>
        /// しらを切り通せるか（bool）。否認可能性が閾値以上、かつ暴露リスクが
        /// その否認可能性を上回らない＝言い逃れの余地が露見圧に勝るとき真。
        /// </summary>
        public static bool PlausibleDenial(float deniability, float exposureRisk, float threshold)
        {
            float den = Mathf.Clamp01(deniability);
            float exp = Mathf.Clamp01(exposureRisk);
            float th = Mathf.Clamp01(threshold);
            return den >= th && exp <= den;
        }

        /// <summary>既定の否認閾値での言い逃れ判定。</summary>
        public static bool PlausibleDenial(float deniability, float exposureRisk)
            => PlausibleDenial(deniability, exposureRisk, CovertActionParams.Default.denialThreshold);

        /// <summary>
        /// 費用対効果（-1..1）。（成果−反動）を費用で割る＝
        /// 上がった成果からブローバックを差し引き、投じた費用で正規化。
        /// 費用0でも分母に下駄（1+cost）を履かせて発散させない。
        /// </summary>
        public static float CovertCostEffectiveness(float operationSuccess, float blowback, float cost, CovertActionParams p)
        {
            float succ = Mathf.Clamp01(operationSuccess);
            float bb = Mathf.Clamp01(blowback) * p.blowbackPenalty;
            float c = Mathf.Max(0f, cost);
            float net = succ - bb;
            return Mathf.Clamp(net / (1f + c), -1f, 1f);
        }

        public static float CovertCostEffectiveness(float operationSuccess, float blowback, float cost)
            => CovertCostEffectiveness(operationSuccess, blowback, cost, CovertActionParams.Default);

        /// <summary>
        /// 公然衝突への転化圧（0..1）。反動が大きいほど、否認可能な秘密工作が
        /// 公然たる対立（宣戦・報復攻撃）へ転がり落ちる＝後戻りできなくなる圧。
        /// </summary>
        public static float EscalationToOvert(float blowback, CovertActionParams p)
        {
            float bb = Mathf.Clamp01(blowback);
            return Mathf.Clamp01(bb * p.escalationScale);
        }

        public static float EscalationToOvert(float blowback)
            => EscalationToOvert(blowback, CovertActionParams.Default);

        /// <summary>実行する価値があるか（費用対効果が閾値以上）。</summary>
        public static bool IsWorthLaunching(float covertCostEffectiveness, float threshold)
        {
            float cce = Mathf.Clamp(covertCostEffectiveness, -1f, 1f);
            float th = Mathf.Clamp(threshold, -1f, 1f);
            return cce >= th;
        }

        /// <summary>既定の採算閾値での実行可否判定。</summary>
        public static bool IsWorthLaunching(float covertCostEffectiveness)
            => IsWorthLaunching(covertCostEffectiveness, CovertActionParams.Default.worthThreshold);
    }
}
