using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 諜報の純ロジック（諜報 ESP・唯一の窓口）。潜入・防諜から任務の成否、得られる情報量、
    /// 露見リスク、破壊工作の効果を解決する。工作員の技量(skill)と対象の防諜(targetCounterIntel)・
    /// 自網の潜入度(infiltration)で決まり、乱数は呼び出し側が roll(0..1) を渡す＝決定論的にテストできる。
    /// 基準フィールドは非破壊（実効値パターン）。値の clamp を徹底。test-first。
    /// </summary>
    public static class EspionageRules
    {
        /// <summary>諜報の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct EspionageParams
        {
            /// <summary>技量・潜入が皆無でも残る最低成功率（運の下駄）。</summary>
            public readonly float baseSuccess;

            /// <summary>潜入度1が情報量に与える最大寄与（情報量の上限係数）。</summary>
            public readonly float infoScale;

            /// <summary>露見リスクの基準値（防諜の効き）。潜入が浅いほど割り増される。</summary>
            public readonly float detectionBase;

            /// <summary>技量1が破壊工作に与える最大効果（工作効果の上限係数）。</summary>
            public readonly float sabotageScale;

            public EspionageParams(float baseSuccess, float infoScale, float detectionBase, float sabotageScale)
            {
                this.baseSuccess = Mathf.Clamp01(baseSuccess);
                this.infoScale = Mathf.Clamp01(infoScale);
                this.detectionBase = Mathf.Clamp01(detectionBase);
                this.sabotageScale = Mathf.Clamp01(sabotageScale);
            }

            /// <summary>既定＝最低成功率0.1・情報量係数1.0・露見基準0.5・工作効果係数0.5。</summary>
            public static EspionageParams Default => new EspionageParams(0.1f, 1f, 0.5f, 0.5f);
        }

        /// <summary>
        /// 任務成功率（0..1）。工作員の技量が高いほど上がり、対象の防諜が高いほど下がる。
        /// baseSuccess を下駄に、技量と防諜の差を効かせる。
        /// </summary>
        public static float MissionSuccessChance(float skill, float targetCounterIntel, EspionageParams p)
        {
            float s = Mathf.Clamp01(skill);
            float ci = Mathf.Clamp01(targetCounterIntel);
            // 下駄＋技量で押し上げ、防諜で押し下げる（残余幅に技量を効かせる）。
            float raw = p.baseSuccess + (1f - p.baseSuccess) * s - ci * (1f - p.baseSuccess);
            return Mathf.Clamp01(raw);
        }

        /// <summary>既定パラメータでの任務成功率。</summary>
        public static float MissionSuccessChance(float skill, float targetCounterIntel)
            => MissionSuccessChance(skill, targetCounterIntel, EspionageParams.Default);

        /// <summary>このとき任務が成功するか（roll が成功率を下回れば成功）。</summary>
        public static bool MissionSucceeds(float skill, float targetCounterIntel, EspionageParams p, float roll)
            => roll < MissionSuccessChance(skill, targetCounterIntel, p);

        /// <summary>
        /// 取得情報量（0..1）。潜入度に比例（深く浸透するほど多くを得る）。infoScale が上限を決める。
        /// </summary>
        public static float InfoGain(float infiltration, EspionageParams p)
            => Mathf.Clamp01(Mathf.Clamp01(infiltration) * p.infoScale);

        /// <summary>既定パラメータでの取得情報量。</summary>
        public static float InfoGain(float infiltration)
            => InfoGain(infiltration, EspionageParams.Default);

        /// <summary>
        /// 露見リスク（0..1）。対象の防諜が高いほど・潜入が浅いほど高い（潜り込めていないと足がつく）。
        /// detectionBase を防諜で効かせ、潜入度ぶんだけ低減する。
        /// </summary>
        public static float DetectionRisk(float infiltration, float targetCounterIntel, EspionageParams p)
        {
            float inf = Mathf.Clamp01(infiltration);
            float ci = Mathf.Clamp01(targetCounterIntel);
            // 防諜が露見の主因、潜入度が深いほど露見しにくい。
            float raw = p.detectionBase * ci * (1f - inf);
            return Mathf.Clamp01(raw);
        }

        /// <summary>既定パラメータでの露見リスク。</summary>
        public static float DetectionRisk(float infiltration, float targetCounterIntel)
            => DetectionRisk(infiltration, targetCounterIntel, EspionageParams.Default);

        /// <summary>このとき露見するか（roll が露見リスクを下回れば露見）。</summary>
        public static bool IsDetected(float infiltration, float targetCounterIntel, EspionageParams p, float roll)
            => roll < DetectionRisk(infiltration, targetCounterIntel, p);

        /// <summary>既定パラメータでの露見判定。</summary>
        public static bool IsDetected(float infiltration, float targetCounterIntel, float roll)
            => IsDetected(infiltration, targetCounterIntel, EspionageParams.Default, roll);

        /// <summary>
        /// 破壊工作の効果（0..1＝対象への打撃割合）。工作員の技量に比例。sabotageScale が上限を決める。
        /// 兵站・生産・士気などへの減算係数として消費する想定（係数 #106）。
        /// </summary>
        public static float SabotageEffect(float skill, EspionageParams p)
            => Mathf.Clamp01(Mathf.Clamp01(skill) * p.sabotageScale);

        /// <summary>既定パラメータでの破壊工作効果。</summary>
        public static float SabotageEffect(float skill)
            => SabotageEffect(skill, EspionageParams.Default);
    }
}
