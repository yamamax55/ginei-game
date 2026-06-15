using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 諜報・情報戦の薄いオーケストレータ（純ロジック・唯一の窓口）。既存の未配線な諜報 Core を合成し、
    /// 「諜報予算で能力を伸ばす」「相手勢力の可視度を出す」「妨害工作の成否を解決する」を提供する。
    /// 数式は既存へ委譲し二重実装しない＝可視度は <see cref="FogOfWarRules"/>/<see cref="SignalIntelligenceRules"/>、
    /// 工作成否は <see cref="EspionageRules"/>/<see cref="CounterIntelligenceRules"/> が持つ。
    /// 乱数は roll(0..1) を呼び出し側が渡す＝決定論。状態は <see cref="IntelState"/> で受ける。null 安全。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class IntelligenceTickRules
    {
        /// <summary>諜報育成の調整係数（マジックナンバー禁止＝集約）。</summary>
        public readonly struct IntelParams
        {
            /// <summary>諜報予算1のとき capability/counterIntel を1tick(dt=1)で伸ばす最大速度。</summary>
            public readonly float growthRate;
            /// <summary>予算のうち防諜（守り）へ回る割合（残りが capability＝攻め）。</summary>
            public readonly float counterShare;

            public IntelParams(float growthRate, float counterShare)
            {
                this.growthRate = Mathf.Max(0f, growthRate);
                this.counterShare = Mathf.Clamp01(counterShare);
            }

            /// <summary>既定＝育成速度0.2・防諜配分0.4（攻め6:守り4）。</summary>
            public static IntelParams Default => new IntelParams(0.2f, 0.4f);
        }

        /// <summary>
        /// 諜報予算 intelFunding(0..1) で capability/counterIntel を伸ばす（1tick）。残余幅に比例して伸びる
        /// 飽和（bounded＝1へ近づくほど鈍る・ラチェットで上限超えしない）。dt でフレーム非依存・null 安全。
        /// </summary>
        public static void TickYear(IntelState s, float intelFunding, float dt, IntelParams p)
        {
            if (s == null) return;
            float fund = Mathf.Clamp01(intelFunding);
            float step = p.growthRate * fund * Mathf.Max(0f, dt);

            // 予算を攻め/守りに配分。残余幅(1-現値)に比例して伸ばす＝飽和・上限超えなし。
            float capStep = step * (1f - p.counterShare);
            float ciStep = step * p.counterShare;

            s.capability = Mathf.Clamp01(s.capability + capStep * (1f - Mathf.Clamp01(s.capability)));
            s.counterIntel = Mathf.Clamp01(s.counterIntel + ciStep * (1f - Mathf.Clamp01(s.counterIntel)));
        }

        /// <summary>既定パラメータでの諜報育成。</summary>
        public static void TickYear(IntelState s, float intelFunding, float dt)
            => TickYear(s, intelFunding, dt, IntelParams.Default);

        /// <summary>
        /// 相手勢力の可視度 0..1。観測側の諜報能力 observerCapability が高いほど・対象の防諜 targetCounterIntel
        /// が高いほど見えにくい。物理的可視性（<see cref="FogOfWarRules.Visibility"/>＝観測能力を reconLevel として）と
        /// 信号諜報の正味価値（<see cref="SignalIntelligenceRules.NetIntelValue"/>）の相乗で出す＝既存へ委譲。
        /// </summary>
        public static float Visibility(float observerCapability, float targetCounterIntel)
        {
            float cap = Mathf.Clamp01(observerCapability);
            float ci = Mathf.Clamp01(targetCounterIntel);

            // 物理的可視性：観測能力を偵察精度として、距離0（直視）の見え。能力が高いほど晴れる。
            float physical = FogOfWarRules.Visibility(0f, cap);
            // 信号諜報：観測能力を事前察知度、防諜を欺瞞リスクとみなした正味価値。
            float signal = SignalIntelligenceRules.NetIntelValue(cap, ci);

            // 相乗＝両レイヤーで見えてはじめて可視。防諜で信号側が削られる。
            return Mathf.Clamp01(physical * signal);
        }

        /// <summary>
        /// 妨害工作の成否（決定論）。工作員技量 capability と対象の防諜 targetCounterIntel から
        /// <see cref="EspionageRules.MissionSuccessChance"/> で成功率を出し、roll(0..1) で判定する＝既存へ委譲。
        /// </summary>
        public static bool SabotageSuccess(float capability, float targetCounterIntel, float roll)
        {
            float chance = EspionageRules.MissionSuccessChance(capability, targetCounterIntel);
            return roll < chance;
        }

        /// <summary>
        /// 妨害工作が成功したときの打撃割合 0..1（<see cref="EspionageRules.SabotageEffect"/> へ委譲）。
        /// 成否判定は <see cref="SabotageSuccess"/>、効果量はこちら＝係数 #106 として消費する想定。
        /// </summary>
        public static float SabotageEffect(float capability)
            => EspionageRules.SabotageEffect(capability);
    }
}
