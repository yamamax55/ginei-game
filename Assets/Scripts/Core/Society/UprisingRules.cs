using UnityEngine;

namespace Ginei
{
    /// <summary>民衆蜂起（大衆の武装蜂起と政権の対応）の調整係数。</summary>
    public readonly struct UprisingParams
    {
        /// <summary>引き金事件が不満を蜂起へ転化する最大重み（0..1・スパークが乾いた薪に火を点ける強さ）。</summary>
        public readonly float sparkWeight;
        /// <summary>大衆の窮状が参加規模を底上げする最大上乗せ（0..1・失うものが無いほど街頭へ出る）。</summary>
        public readonly float desperationWeight;
        /// <summary>治安部隊の離反の増幅最大倍率（兵の共感×政権の非正統が同胞射撃を拒ませる・1以上）。</summary>
        public readonly float defectionAmplify;
        /// <summary>鎮圧能力に効く発砲意志の重み（0..1・忠誠部隊があっても撃つ意志が無ければ鎮圧は空回り）。</summary>
        public readonly float willKillWeight;
        /// <summary>蜂起の波及の最大倍率（通信網が地方都市へ蜂起を伝播させる・1以上）。</summary>
        public readonly float spreadAmplify;
        /// <summary>政権の結束に効くエリート結束の重み（0..1・支配層が割れずに踏みとどまる度合い）。</summary>
        public readonly float eliteWeight;
        /// <summary>革命の成否の鎮圧減算の重み（0..1・鎮圧能力が波及×離反の革命圧をどれだけ押し返すか）。</summary>
        public readonly float suppressionWeight;

        public UprisingParams(float sparkWeight, float desperationWeight, float defectionAmplify,
            float willKillWeight, float spreadAmplify, float eliteWeight, float suppressionWeight)
        {
            this.sparkWeight = Mathf.Clamp01(sparkWeight);
            this.desperationWeight = Mathf.Clamp01(desperationWeight);
            this.defectionAmplify = Mathf.Max(1f, defectionAmplify);
            this.willKillWeight = Mathf.Clamp01(willKillWeight);
            this.spreadAmplify = Mathf.Max(1f, spreadAmplify);
            this.eliteWeight = Mathf.Clamp01(eliteWeight);
            this.suppressionWeight = Mathf.Clamp01(suppressionWeight);
        }

        /// <summary>
        /// 既定＝スパーク重み0.5・窮状重み0.4・離反増幅2.0・発砲意志重み0.6・波及増幅2.0・
        /// エリート結束重み0.7・鎮圧減算重み0.8。
        /// </summary>
        public static UprisingParams Default =>
            new UprisingParams(0.5f, 0.4f, 2.0f, 0.6f, 2.0f, 0.7f, 0.8f);
    }

    /// <summary>
    /// 民衆蜂起（大衆の武装蜂起と政権の対応）の純ロジック。
    /// 積もった不満が引き金事件で発火し、窮した大衆が街頭へ出て参加規模が膨らみ、
    /// 兵の共感と政権の非正統が治安部隊の離反を呼び、政権の鎮圧能力（忠誠部隊×発砲意志）と
    /// 蜂起の波及（通信網による地方拡大）がせめぎ合って、革命が成るか鎮圧されるかが決まる。
    /// <para>
    /// 分担：<see cref="MutinyRules"/> は軍内部の反乱（命令拒否）を、<see cref="DesertionRules"/> は脱走を、
    /// <see cref="RevolutionRules"/>/<see cref="CivilWarRules"/> があれば国家規模の体制転覆を扱う。
    /// 本クラスは「大衆が武装して街頭で政権と対峙する」一過程＝引き金→参加→治安部隊の離反→
    /// 鎮圧vs波及→革命の成否、の一点に責任を持つ（決定論・観測専用ではなく純計算の窓口）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。符号付き出力は -1..1 にクランプ。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class UprisingRules
    {
        /// <summary>
        /// 蜂起の発火（0..1）＝不満水準 grievanceLevel を土台に、引き金事件 sparkEvent が点火する。
        /// 不満が乾いた薪、引き金が火＝不満が無ければ火種があっても燃えず、引き金が無ければ不満は燻ったまま。
        /// </summary>
        public static float UprisingTrigger(float grievanceLevel, float sparkEvent, UprisingParams p)
        {
            float grievance = Mathf.Clamp01(grievanceLevel);
            float spark = Mathf.Clamp01(sparkEvent) * p.sparkWeight;
            // 不満が土台、引き金がそれを街頭へ押し上げる（grievance==0 なら火が点かない）。
            return Mathf.Clamp01(grievance * spark);
        }

        public static float UprisingTrigger(float grievanceLevel, float sparkEvent)
            => UprisingTrigger(grievanceLevel, sparkEvent, UprisingParams.Default);

        /// <summary>
        /// 大衆の参加規模（0..1）＝発火 uprisingTrigger を、大衆の窮状 massDesperation が上乗せで膨らませる。
        /// 失うものが無い者ほど街頭へ出る＝発火が無ければ参加0、窮状が高いほど同じ発火でも動員が大きい。
        /// </summary>
        public static float PopularParticipation(float uprisingTrigger, float massDesperation, UprisingParams p)
        {
            float trig = Mathf.Clamp01(uprisingTrigger);
            float boost = 1f + Mathf.Clamp01(massDesperation) * p.desperationWeight;
            return Mathf.Clamp01(trig * boost);
        }

        public static float PopularParticipation(float uprisingTrigger, float massDesperation)
            => PopularParticipation(uprisingTrigger, massDesperation, UprisingParams.Default);

        /// <summary>
        /// 治安部隊の離反（0..1）＝参加 participation × 兵の共感 troopSympathy ×（1-政権正統性）を増幅倍率で膨らませる。
        /// 街頭の大衆が大きく、兵が大衆に共感し、政権が正統性を欠くほど兵は同胞へ銃を向けず離反する。
        /// </summary>
        public static float SecurityForceDefection(float participation, float troopSympathy, float regimeLegitimacy, UprisingParams p)
        {
            float crowd = Mathf.Clamp01(participation);
            float sympathy = Mathf.Clamp01(troopSympathy);
            float illegitimacy = 1f - Mathf.Clamp01(regimeLegitimacy);
            float core = crowd * sympathy * illegitimacy;
            return Mathf.Clamp01(core * p.defectionAmplify);
        }

        public static float SecurityForceDefection(float participation, float troopSympathy, float regimeLegitimacy)
            => SecurityForceDefection(participation, troopSympathy, regimeLegitimacy, UprisingParams.Default);

        /// <summary>
        /// 政権の鎮圧能力（0..1）＝忠誠部隊 loyalForces × 発砲意志 willingnessToKill。
        /// 部隊があっても撃つ意志が無ければ鎮圧は空回り＝発砲意志が重み付きで効く（意志0でも部隊地力は残す）。
        /// </summary>
        public static float RegimeSuppressionCapacity(float loyalForces, float willingnessToKill, UprisingParams p)
        {
            float forces = Mathf.Clamp01(loyalForces);
            // 発砲意志を重みで混ぜる＝意志1で満額、意志0でも (1-willKillWeight) ぶんの地力は残る。
            float willFactor = 1f - p.willKillWeight + p.willKillWeight * Mathf.Clamp01(willingnessToKill);
            return Mathf.Clamp01(forces * willFactor);
        }

        public static float RegimeSuppressionCapacity(float loyalForces, float willingnessToKill)
            => RegimeSuppressionCapacity(loyalForces, willingnessToKill, UprisingParams.Default);

        /// <summary>
        /// 蜂起の波及（0..1）＝参加 participation を、通信網 communicationNetworks が地方都市へ伝播させる。
        /// 首都の街頭が燃え、通信網が広いほど蜂起は地方へ拡大する＝参加が無ければ波及0。
        /// </summary>
        public static float UprisingSpread(float participation, float communicationNetworks, UprisingParams p)
        {
            float crowd = Mathf.Clamp01(participation);
            float reach = 1f + Mathf.Clamp01(communicationNetworks) * (p.spreadAmplify - 1f);
            return Mathf.Clamp01(crowd * reach);
        }

        public static float UprisingSpread(float participation, float communicationNetworks)
            => UprisingSpread(participation, communicationNetworks, UprisingParams.Default);

        /// <summary>
        /// 政権の結束（0..1）＝エリート結束 eliteUnity ×（1-治安部隊の離反 securityForceDefection）。
        /// 支配層が割れず、治安部隊が離反しないほど政権は踏みとどまる＝離反が進むと結束は崩れる。
        /// </summary>
        public static float RegimeCohesion(float eliteUnity, float securityForceDefection, UprisingParams p)
        {
            float unity = Mathf.Clamp01(eliteUnity);
            float held = 1f - Mathf.Clamp01(securityForceDefection);
            // エリート結束を重みで効かせ、離反が結束の地盤を削る。
            float baseUnity = 1f - p.eliteWeight + p.eliteWeight * unity;
            return Mathf.Clamp01(baseUnity * held);
        }

        public static float RegimeCohesion(float eliteUnity, float securityForceDefection)
            => RegimeCohesion(eliteUnity, securityForceDefection, UprisingParams.Default);

        /// <summary>
        /// 革命の成否（-1..1）＝波及 uprisingSpread × 離反 securityForceDefection の革命圧から、
        /// 鎮圧能力 regimeSuppressionCapacity を重み付きで差し引く。正＝革命優位、負＝政権が押し返す、0付近＝拮抗。
        /// 蜂起が全土へ広がり兵が離反するほど正へ、政権が撃つ力を保つほど負へ振れる。
        /// </summary>
        public static float RevolutionarySuccess(float uprisingSpread, float securityForceDefection, float regimeSuppressionCapacity, UprisingParams p)
        {
            float revolt = Mathf.Clamp01(uprisingSpread) * Mathf.Clamp01(securityForceDefection);
            float suppress = Mathf.Clamp01(regimeSuppressionCapacity) * p.suppressionWeight;
            return Mathf.Clamp(revolt - suppress, -1f, 1f);
        }

        public static float RevolutionarySuccess(float uprisingSpread, float securityForceDefection, float regimeSuppressionCapacity)
            => RevolutionarySuccess(uprisingSpread, securityForceDefection, regimeSuppressionCapacity, UprisingParams.Default);

        /// <summary>
        /// 政権が打倒されるか＝革命の成否 revolutionarySuccess が threshold を超えるか。
        /// threshold は -1..1 にクランプ＝既定委譲版は中立寄りの 0.2 で判定（革命がはっきり優位なら打倒）。
        /// </summary>
        public static bool IsRegimeOverthrown(float revolutionarySuccess, float threshold)
        {
            float success = Mathf.Clamp(revolutionarySuccess, -1f, 1f);
            return success > Mathf.Clamp(threshold, -1f, 1f);
        }

        public static bool IsRegimeOverthrown(float revolutionarySuccess)
            => IsRegimeOverthrown(revolutionarySuccess, 0.2f);
    }
}
