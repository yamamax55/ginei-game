using UnityEngine;

namespace Ginei
{
    /// <summary>合意/協力の調整係数（ガンジー #836/#837）。</summary>
    public readonly struct ConsentParams
    {
        /// <summary>協力がこれ未満なら統治不能（非協力崩壊）。</summary>
        public readonly float collapseThreshold;
        /// <summary>抑圧1あたりの協力低下/秒。</summary>
        public readonly float oppressionDecay;
        /// <summary>正統性1あたりの協力回復/秒。</summary>
        public readonly float legitimacyRecover;

        public ConsentParams(float collapseThreshold, float oppressionDecay, float legitimacyRecover)
        {
            this.collapseThreshold = collapseThreshold;
            this.oppressionDecay = oppressionDecay;
            this.legitimacyRecover = legitimacyRecover;
        }

        public static ConsentParams Default => new ConsentParams(0.3f, 0.2f, 0.1f);
    }

    /// <summary>
    /// 「権力は借り物」の純ロジック（ガンジー #835/#836/#837）。統治は被支配者の協力で成り立つ＝
    /// 実効統治力＝直接戦力＋協力×人口。協力は抑圧（収奪 GEO-2 #843）で下がり正統性で回復し、
    /// 閾値割れ（または非協力<see cref="Withdraw"/>）で<b>戦わずに統治不能</b>になる。
    /// 関ヶ原の条件付き忠誠（#817）の文明規模版。軍事的に圧倒しても合意を失えば崩れる。test-first。
    /// </summary>
    public static class ConsentRules
    {
        /// <summary>協力を dt 進める。抑圧で下がり、正統性で回復（差分を加算）。</summary>
        public static void Tick(Polity p, float dt, ConsentParams prm)
        {
            if (p == null || dt <= 0f) return;
            float delta = (prm.legitimacyRecover * p.legitimacy - prm.oppressionDecay * p.oppression) * dt;
            p.cooperation = Mathf.Clamp01(p.cooperation + delta);
        }

        public static void Tick(Polity p, float dt) => Tick(p, dt, ConsentParams.Default);

        /// <summary>実効統治力＝直接戦力＋協力する被支配者（協力×人口が行政・徴税・軍を回す）。</summary>
        public static float ControlStrength(Polity p)
        {
            if (p == null) return 0f;
            return p.rulerForce + p.cooperation * p.population;
        }

        /// <summary>統治不能か（協力が閾値割れ＝非協力＝戦わずに崩壊）。</summary>
        public static bool IsUngovernable(Polity p, ConsentParams prm)
            => p != null && p.cooperation < prm.collapseThreshold;

        public static bool IsUngovernable(Polity p) => IsUngovernable(p, ConsentParams.Default);

        /// <summary>非協力（ボイコット・不服従・塩の行進 #838）＝協力を amount 引き下げる（被支配者の意志の行為）。</summary>
        public static void Withdraw(Polity p, float amount)
        {
            if (p == null) return;
            p.cooperation = Mathf.Max(0f, p.cooperation - amount);
        }
    }
}
