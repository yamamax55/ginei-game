using UnityEngine;

namespace Ginei
{
    /// <summary>市民的友愛（政治的友愛）と審議崩壊の調整係数（ARIS-4 #1503）。</summary>
    public readonly struct CivicPhiliaParams
    {
        /// <summary>市民的信頼がこれ未満なら政治機能不全（市民的崩壊）。</summary>
        public readonly float breakdownThreshold;
        /// <summary>不平等1あたりの市民的友愛の侵食/秒。</summary>
        public readonly float inequalityErosion;
        /// <summary>僭主圧力1あたりの市民的友愛の侵食/秒。</summary>
        public readonly float tyrantErosion;
        /// <summary>審議崩壊が膠着へ波及する増幅率（崩壊1で +amp ぶん膠着を押し上げる）。</summary>
        public readonly float gridlockAmp;

        public CivicPhiliaParams(float breakdownThreshold, float inequalityErosion, float tyrantErosion, float gridlockAmp)
        {
            this.breakdownThreshold = breakdownThreshold;
            this.inequalityErosion = inequalityErosion;
            this.tyrantErosion = tyrantErosion;
            this.gridlockAmp = gridlockAmp;
        }

        /// <summary>既定＝信頼0.3未満で崩壊・不平等0.15/秒・僭主0.2/秒・膠着増幅0.5。</summary>
        public static CivicPhiliaParams Default => new CivicPhiliaParams(0.3f, 0.15f, 0.2f, 0.5f);
    }

    /// <summary>
    /// 市民的友愛（politike philia＝市民同士の信頼・友愛）と審議崩壊の純ロジック（ARIS-4 #1503・
    /// アリストテレス『政治学』『ニコマコス倫理学』参考）。市民が互いを信頼し共通善を志向するとき
    /// 審議（熟議）が機能してポリスが結束する＝<see cref="CivicTrust"/>→<see cref="DeliberativeCapacity"/>。
    /// 不平等と僭主の圧力がこの友愛を蝕む（<see cref="TrustErosion"/>）と、信頼を失い二極化した市民の
    /// 審議が崩壊し（<see cref="DeliberativeCollapse"/>）、政治の膠着（グリッドロック）が増幅する
    /// （<see cref="GridlockAmplification"/>→<see cref="SeparationOfPowersRules.IsGridlocked"/> へ）。
    /// <see cref="ConsentRules"/>（被治者の協力＝統治可能性）/<see cref="SeparationOfPowersRules"/>
    /// （三権の構造的グリッドロック）/<c>MesoiRules</c>（中間層の安定・同EPIC ARIS）/<c>PluralityRules</c>
    /// （多様性）とは別＝こちらは<b>市民的友愛（ポリス的信頼）が審議を支える</b>層。
    /// すべて plain 引数・全入力クランプ・乱数なし決定論。test-first。
    /// </summary>
    public static class CivicPhiliaRules
    {
        /// <summary>
        /// 市民的信頼（政治的友愛）0..1＝共有された価値×互恵性。共通の価値観があり互いに報い合う
        /// （互恵）ほど市民は友愛で結ばれる。どちらか欠ければ友愛は成り立たない（積）。
        /// </summary>
        public static float CivicTrust(float sharedValues, float reciprocity)
        {
            float sv = Mathf.Clamp01(sharedValues);
            float r = Mathf.Clamp01(reciprocity);
            return sv * r;
        }

        /// <summary>
        /// 熟議・審議の機能度 0..1＝市民的信頼が高いほど審議が機能する。信頼があれば異論を交わし
        /// 妥協できる（友愛は譲り合いを可能にする）。信頼ゼロでは審議は始まらない。
        /// </summary>
        public static float DeliberativeCapacity(float civicTrust)
            => Mathf.Clamp01(civicTrust);

        /// <summary>
        /// 市民的友愛の侵食量＝不平等と僭主の圧力が時間で友愛を蝕む（分断と支配が信頼を壊す）。
        /// dt ぶんの低下量（正の値）を返す。呼び出し側が市民的信頼から差し引く想定。
        /// </summary>
        public static float TrustErosion(float inequality, float tyrantPressure, float dt, CivicPhiliaParams prm)
        {
            if (dt <= 0f) return 0f;
            float ineq = Mathf.Clamp01(inequality);
            float tyr = Mathf.Clamp01(tyrantPressure);
            return (prm.inequalityErosion * ineq + prm.tyrantErosion * tyr) * dt;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float TrustErosion(float inequality, float tyrantPressure, float dt)
            => TrustErosion(inequality, tyrantPressure, dt, CivicPhiliaParams.Default);

        /// <summary>
        /// 審議の崩壊度 0..1＝信頼が失われ二極化（分極化）すると審議が崩壊する。話し合いが成立しない度合い。
        /// 信頼の不足(1-trust)と二極化を掛け合わせる＝信頼が高ければ二極化しても審議は耐え、
        /// 信頼が低く二極化が進むと崩壊する。
        /// </summary>
        public static float DeliberativeCollapse(float civicTrust, float polarization)
        {
            float trust = Mathf.Clamp01(civicTrust);
            float pol = Mathf.Clamp01(polarization);
            return Mathf.Clamp01((1f - trust) * pol);
        }

        /// <summary>
        /// 政治的膠着（グリッドロック）の増幅率 ≥1。審議の崩壊が膠着を増幅する。
        /// 崩壊0で1.0（増幅なし）、崩壊が進むほど <see cref="CivicPhiliaParams.gridlockAmp"/> ぶん上がる。
        /// <see cref="SeparationOfPowersRules.IsGridlocked"/> の判定に掛ける係数として使う想定。
        /// </summary>
        public static float GridlockAmplification(float deliberativeCollapse, CivicPhiliaParams prm)
        {
            float c = Mathf.Clamp01(deliberativeCollapse);
            return 1f + prm.gridlockAmp * c;
        }

        /// <summary>既定パラメータ版。</summary>
        public static float GridlockAmplification(float deliberativeCollapse)
            => GridlockAmplification(deliberativeCollapse, CivicPhiliaParams.Default);

        /// <summary>
        /// 共通目標によるポリスの結束度 0..1＝市民的友愛と共通の目標が結束を生む。
        /// 友愛があり共通善（共有された目標）を志向するときポリスは一つにまとまる（積）。
        /// </summary>
        public static float CommonPurposeStrength(float civicTrust, float sharedGoal)
        {
            float trust = Mathf.Clamp01(civicTrust);
            float goal = Mathf.Clamp01(sharedGoal);
            return trust * goal;
        }

        /// <summary>
        /// 派閥的敵意 0..1＝不平等と僭主圧力が市民を敵対する派閥に分断する（友愛の反対＝敵意）。
        /// 友愛が結束させるのに対し、不平等（持つ者と持たざる者）と僭主の支配が市民を割る。
        /// 両者の相補的な合成（どちらか高ければ敵意は高い）。
        /// </summary>
        public static float FactionalEnmity(float inequality, float tyrantPressure)
        {
            float ineq = Mathf.Clamp01(inequality);
            float tyr = Mathf.Clamp01(tyrantPressure);
            // 1-(1-a)(1-b)＝どちらかが高ければ敵意は高い（補集合の積の補）
            return Mathf.Clamp01(1f - (1f - ineq) * (1f - tyr));
        }

        /// <summary>
        /// 市民的崩壊か（市民的友愛が崩壊し政治が機能不全＝信頼が閾値割れ）。
        /// 友愛が threshold 未満なら審議も結束も成り立たず政治が回らない＝true。
        /// </summary>
        public static bool IsCivicBreakdown(float civicTrust, float threshold)
            => Mathf.Clamp01(civicTrust) < threshold;

        /// <summary>既定パラメータ版（<see cref="CivicPhiliaParams.breakdownThreshold"/> を使う）。</summary>
        public static bool IsCivicBreakdown(float civicTrust)
            => IsCivicBreakdown(civicTrust, CivicPhiliaParams.Default.breakdownThreshold);
    }
}
