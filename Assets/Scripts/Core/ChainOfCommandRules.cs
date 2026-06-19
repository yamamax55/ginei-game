using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮系統（チェーン・オブ・コマンド）の調整値。スパン負荷の効き、階層深度の希薄化、
    /// 命令貫徹の重み、継承の備え、指揮空白の発生と回復。すべて ctor でクランプ。
    /// 実効値パターン（基準値は別に持ち、ここは係数だけ）。
    /// </summary>
    public readonly struct ChainOfCommandParams
    {
        /// <summary>スパン負荷の効き（指揮能力に対する部下数の超過がどれだけ統制を下げるか）。</summary>
        public readonly float spanStrength;
        /// <summary>階層深度1段あたりの統制希薄化（Pow の底の減衰率・0..1）。</summary>
        public readonly float dilutionPerLevel;
        /// <summary>階層希薄化の減衰カーブ指数（大きいほど深部の落ち込みが急）。</summary>
        public readonly float dilutionExponent;
        /// <summary>統制の希薄化の下限（深い階層でもこの統制は残る・0..1）。</summary>
        public readonly float dilutionFloor;
        /// <summary>継承計画が継承の備えに寄与する重み（0..1）。</summary>
        public readonly float planWeight;
        /// <summary>指揮空白からの回復で部隊結束が効く重み（0..1）。</summary>
        public readonly float cohesionWeight;
        /// <summary>指揮系統が機能しているとみなす実効統制の既定閾値。</summary>
        public readonly float intactThreshold;

        public ChainOfCommandParams(
            float spanStrength,
            float dilutionPerLevel,
            float dilutionExponent,
            float dilutionFloor,
            float planWeight,
            float cohesionWeight,
            float intactThreshold)
        {
            this.spanStrength = Mathf.Max(0f, spanStrength);
            this.dilutionPerLevel = Mathf.Clamp01(dilutionPerLevel);
            this.dilutionExponent = Mathf.Max(0.01f, dilutionExponent);
            this.dilutionFloor = Mathf.Clamp01(dilutionFloor);
            this.planWeight = Mathf.Clamp01(planWeight);
            this.cohesionWeight = Mathf.Clamp01(cohesionWeight);
            this.intactThreshold = Mathf.Clamp(intactThreshold, -1f, 1f);
        }

        /// <summary>既定：スパン1.0・希薄化0.15/段・指数1.0・下限0.2・計画重み0.5・結束重み0.5・閾値0.0。</summary>
        public static ChainOfCommandParams Default => new ChainOfCommandParams(
            DefaultSpanStrength, DefaultDilutionPerLevel, DefaultDilutionExponent, DefaultDilutionFloor,
            DefaultPlanWeight, DefaultCohesionWeight, DefaultIntactThreshold);

        public const float DefaultSpanStrength = 1f;
        public const float DefaultDilutionPerLevel = 0.15f;
        public const float DefaultDilutionExponent = 1f;
        public const float DefaultDilutionFloor = 0.2f;
        public const float DefaultPlanWeight = 0.5f;
        public const float DefaultCohesionWeight = 0.5f;
        public const float DefaultIntactThreshold = 0f;
    }

    /// <summary>
    /// 指揮系統そのものの健全性の純ロジック（階層・継承・統制の力学・盤面非依存）。
    /// <b>指揮の階層が深いほど末端への統制が希薄化し、指揮範囲（スパン）が過大だと統制が落ち、
    /// 上官を喪えば指揮空白が生じる</b>。副官の質と継承計画で空白に備え、部隊結束で回復する。
    /// 減衰は Pow で組む（Log/Exp 不使用＝終盤ラグ規律・決定論）。実効値パターン（基準値非破壊）。
    /// <para>
    /// 分担：<see cref="CommandDelayRules"/>（命令が末端に届くまでの<b>伝達ラグ</b>）とは別＝
    /// こちらは指揮<b>構造</b>の健全性（階層深度・スパン・継承・空白）を数値化する。
    /// <see cref="BattlefieldCommandRules"/>（指揮官戦死時の臨時指揮継承）とも別系統＝
    /// 本クラスは継承の「備え」と空白の発生・回復を抽象スカラで扱う。
    /// </para>
    /// </summary>
    public static class ChainOfCommandRules
    {
        // ---- スパン・オブ・コントロール（指揮範囲の負荷） ----

        /// <summary>既定パラメータで指揮範囲の負荷を返す。</summary>
        public static float SpanOfControl(float subordinateCount, float commanderCapacity)
            => SpanOfControl(subordinateCount, commanderCapacity, ChainOfCommandParams.Default);

        /// <summary>
        /// 部下数と指揮能力から指揮範囲の負荷（0..1）を返す。`load = clamp01((sub/cap - 1) * spanStrength)`。
        /// 部下数が指揮能力以内なら負荷0、超過するほど統制が低下する。能力0以下は超過扱い（負荷最大）。
        /// </summary>
        public static float SpanOfControl(float subordinateCount, float commanderCapacity, ChainOfCommandParams p)
        {
            float sub = Mathf.Max(0f, subordinateCount);
            float cap = Mathf.Max(0f, commanderCapacity);
            if (cap <= 0f) return sub > 0f ? 1f : 0f;
            float overload = (sub / cap) - 1f;
            return Mathf.Clamp01(overload * p.spanStrength);
        }

        // ---- 統制の希薄化（階層が深いほど末端への統制が薄れる） ----

        /// <summary>既定パラメータで階層深度による統制希薄化を返す。</summary>
        public static float ControlDilution(int hierarchyDepth)
            => ControlDilution(hierarchyDepth, ChainOfCommandParams.Default);

        /// <summary>
        /// 階層深度から末端への統制（0..1・1=希薄化なし）を返す。
        /// `control = clamp(pow(1 - dilutionPerLevel, depth^exponent), floor, 1)`。
        /// 階層が深いほど統制が薄れるが、下限 <see cref="ChainOfCommandParams.dilutionFloor"/> までで止まる。深度0で1。
        /// </summary>
        public static float ControlDilution(int hierarchyDepth, ChainOfCommandParams p)
        {
            float depth = Mathf.Max(0, hierarchyDepth);
            if (depth <= 0f) return 1f;
            float curvedDepth = Mathf.Pow(depth, p.dilutionExponent);
            float control = Mathf.Pow(1f - p.dilutionPerLevel, curvedDepth);
            return Mathf.Clamp(control, p.dilutionFloor, 1f);
        }

        // ---- 命令の貫徹度（末端まで明瞭に届くか） ----

        /// <summary>既定パラメータで命令の貫徹度を返す。</summary>
        public static float OrderPenetration(float commandClarity, float spanLoad, float controlDilution)
            => OrderPenetration(commandClarity, spanLoad, controlDilution, ChainOfCommandParams.Default);

        /// <summary>
        /// 命令の明瞭さ・スパン負荷・統制（希薄化後）から末端への貫徹度（0..1）を返す。
        /// `penetration = clamp01(clarity / (1 + spanLoad) * control)`。
        /// 明瞭でも負荷が高ければ薄まり、深い階層で統制が希薄なら末端まで届かない。
        /// </summary>
        public static float OrderPenetration(float commandClarity, float spanLoad, float controlDilution, ChainOfCommandParams p)
        {
            float clarity = Mathf.Clamp01(commandClarity);
            float load = Mathf.Clamp01(spanLoad);
            float control = Mathf.Clamp01(controlDilution);
            return Mathf.Clamp01(clarity / (1f + load) * control);
        }

        // ---- 指揮継承の備え（上官喪失への備え） ----

        /// <summary>既定パラメータで指揮継承の備えを返す。</summary>
        public static float SuccessionReadiness(float deputyQuality, float successionPlan)
            => SuccessionReadiness(deputyQuality, successionPlan, ChainOfCommandParams.Default);

        /// <summary>
        /// 副官の質と継承計画から指揮継承の備え（0..1）を返す。
        /// `readiness = clamp01(deputy * lerp(1, plan, planWeight))`。
        /// 優れた副官がいて継承計画が整うほど、上官喪失に備えられる。副官が居なければ計画があっても0。
        /// </summary>
        public static float SuccessionReadiness(float deputyQuality, float successionPlan, ChainOfCommandParams p)
        {
            float deputy = Mathf.Clamp01(deputyQuality);
            float plan = Mathf.Clamp01(successionPlan);
            float planFactor = Mathf.Lerp(1f, plan, p.planWeight);
            return Mathf.Clamp01(deputy * planFactor);
        }

        // ---- 指揮空白（上官喪失で生じる統制の穴） ----

        /// <summary>既定パラメータで指揮空白を返す。</summary>
        public static float CommandVacuum(float leaderLost, float successionReadiness)
            => CommandVacuum(leaderLost, successionReadiness, ChainOfCommandParams.Default);

        /// <summary>
        /// 指揮官喪失（0..1・1=完全喪失）と継承の備えから指揮空白（0..1）を返す。
        /// `vacuum = clamp01(lost * (1 - readiness))`。備えが整っているほど空白は小さい。
        /// </summary>
        public static float CommandVacuum(float leaderLost, float successionReadiness, ChainOfCommandParams p)
        {
            float lost = Mathf.Clamp01(leaderLost);
            float readiness = Mathf.Clamp01(successionReadiness);
            return Mathf.Clamp01(lost * (1f - readiness));
        }

        // ---- 空白からの回復（備えと結束で立て直す） ----

        /// <summary>既定パラメータで指揮空白からの回復力を返す。</summary>
        public static float VacuumRecovery(float successionReadiness, float unitCohesion)
            => VacuumRecovery(successionReadiness, unitCohesion, ChainOfCommandParams.Default);

        /// <summary>
        /// 継承の備えと部隊結束から指揮空白の回復力（0..1）を返す。
        /// `recovery = clamp01(readiness * lerp(1, cohesion, cohesionWeight))`。
        /// 備えが整い結束が固いほど空白から早く立て直す。備えが無ければ結束だけでは回復しない。
        /// </summary>
        public static float VacuumRecovery(float successionReadiness, float unitCohesion, ChainOfCommandParams p)
        {
            float readiness = Mathf.Clamp01(successionReadiness);
            float cohesion = Mathf.Clamp01(unitCohesion);
            float cohesionFactor = Mathf.Lerp(1f, cohesion, p.cohesionWeight);
            return Mathf.Clamp01(readiness * cohesionFactor);
        }

        // ---- 実効統制（貫徹度−指揮空白） ----

        /// <summary>既定パラメータで実効統制を返す。</summary>
        public static float EffectiveControl(float orderPenetration, float commandVacuum)
            => EffectiveControl(orderPenetration, commandVacuum, ChainOfCommandParams.Default);

        /// <summary>
        /// 命令貫徹度から指揮空白を差し引いた実効統制（-1..1）を返す。
        /// `control = clamp(penetration - vacuum, -1, 1)`。空白が貫徹度を上回ると統制が崩れ負に転じる。
        /// </summary>
        public static float EffectiveControl(float orderPenetration, float commandVacuum, ChainOfCommandParams p)
        {
            float penetration = Mathf.Clamp01(orderPenetration);
            float vacuum = Mathf.Clamp01(commandVacuum);
            return Mathf.Clamp(penetration - vacuum, -1f, 1f);
        }

        // ---- 指揮系統の機能判定 ----

        /// <summary>既定の閾値で指揮系統が機能しているかを返す。</summary>
        public static bool IsChainIntact(float effectiveControl)
            => IsChainIntact(effectiveControl, ChainOfCommandParams.DefaultIntactThreshold);

        /// <summary>実効統制が閾値より大きければ指揮系統は機能している（命令が末端まで通る）。</summary>
        public static bool IsChainIntact(float effectiveControl, float threshold)
            => Mathf.Clamp(effectiveControl, -1f, 1f) > Mathf.Clamp(threshold, -1f, 1f);
    }
}
