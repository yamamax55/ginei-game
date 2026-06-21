using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 陣形変更の指揮スキルポイント経済（#陣形コスト）。陣形変更は提督の指揮スキルポイントを消費し、
    /// 多用を抑制する。<b>戦闘中は指揮系統が乱れるためコストが重い</b>（combatCostMultiplier）。
    /// プールの上限・回復は提督の統率に比例（有能な提督ほど柔軟に再編できる）。純ロジック・test-first。
    /// 数値はここ（Params）に集約し、Game 層（Squadron）は値を渡して判定を委譲する＝二重実装しない。
    /// </summary>
    public static class FormationChangeCostRules
    {
        /// <summary>陣形変更コスト経済の調整パラメータ（統率→上限/回復、平時コスト、戦闘倍率）。</summary>
        public struct Params
        {
            public float maxPointsMin;        // 統率0でのスキルポイント上限
            public float maxPointsMax;        // 統率100でのスキルポイント上限
            public float regenMin;            // 統率0での毎秒回復
            public float regenMax;            // 統率100での毎秒回復
            public float peaceCost;           // 非戦闘時の1回あたりコスト
            public float combatCostMultiplier;// 戦闘中のコスト倍率（>1＝重い）

            public Params(float maxPointsMin, float maxPointsMax, float regenMin, float regenMax,
                float peaceCost, float combatCostMultiplier)
            {
                this.maxPointsMin = maxPointsMin;
                this.maxPointsMax = maxPointsMax;
                this.regenMin = regenMin;
                this.regenMax = regenMax;
                this.peaceCost = peaceCost;
                this.combatCostMultiplier = combatCostMultiplier;
            }

            /// <summary>既定：統率0で上限60/回復4、統率100で上限140/回復10、平時コスト25、戦闘×3。</summary>
            public static Params Default => new Params(60f, 140f, 4f, 10f, 25f, 3f);
        }

        /// <summary>統率（0..100）→スキルポイント上限（Lerp）。有能な提督ほどプールが大きい。</summary>
        public static float MaxPoints(float leadership, Params p)
            => Mathf.Lerp(p.maxPointsMin, p.maxPointsMax, Mathf.Clamp01(leadership / 100f));

        /// <summary>統率（0..100）→毎秒回復（Lerp）。有能な提督ほど立て直しが速い。</summary>
        public static float RegenPerSecond(float leadership, Params p)
            => Mathf.Lerp(p.regenMin, p.regenMax, Mathf.Clamp01(leadership / 100f));

        /// <summary>1回の陣形変更コスト。戦闘中は <see cref="Params.combatCostMultiplier"/> 倍で重い。</summary>
        public static float Cost(bool inCombat, Params p)
            => Mathf.Max(0f, p.peaceCost) * (inCombat ? Mathf.Max(1f, p.combatCostMultiplier) : 1f);

        /// <summary>現在のポイントで陣形変更を賄えるか。</summary>
        public static bool CanAfford(float points, bool inCombat, Params p)
            => points >= Cost(inCombat, p);

        /// <summary>ポイントを dt 秒ぶん回復（上限クランプ）。フレームレート非依存（呼び出し側が Time.deltaTime を渡す）。</summary>
        public static float Regen(float points, float maxPoints, float regenPerSecond, float dt)
            => Mathf.Min(maxPoints, points + Mathf.Max(0f, regenPerSecond) * Mathf.Max(0f, dt));
    }
}
