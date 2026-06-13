using UnityEngine;

namespace Ginei
{
    /// <summary>対抗的正統性（替天行道）の調整係数（SHZ-2 #1358・水滸伝）。</summary>
    public readonly struct CounterLegitimacyParams
    {
        /// <summary>官の腐敗が体制の正統性を蝕む重み（0..1）。</summary>
        public readonly float corruptionWeight;
        /// <summary>官の暴政が体制の正統性を蝕む重み（0..1）。</summary>
        public readonly float oppressionWeight;
        /// <summary>体制の正統性喪失が対抗的正統性を育てる速度（per dt）。</summary>
        public readonly float counterGrowthRate;
        /// <summary>義賊の規律（民を害さない振る舞い）が対抗的正統性を底上げする重み（0..1）。</summary>
        public readonly float conductWeight;
        /// <summary>官逼民反＝圧政が民を反乱へ追い込む速度（per dt）。</summary>
        public readonly float driveToRebellionRate;
        /// <summary>民心が体制から義賊へ移る速度（per dt）。</summary>
        public readonly float supportShiftRate;
        /// <summary>限定的標的（腐敗官だけを討つ）が正統性を保つ最大上乗せ（0..1）。</summary>
        public readonly float limitedTargetWeight;

        public CounterLegitimacyParams(float corruptionWeight, float oppressionWeight, float counterGrowthRate,
            float conductWeight, float driveToRebellionRate, float supportShiftRate, float limitedTargetWeight)
        {
            this.corruptionWeight = Mathf.Clamp01(corruptionWeight);
            this.oppressionWeight = Mathf.Clamp01(oppressionWeight);
            this.counterGrowthRate = Mathf.Max(0f, counterGrowthRate);
            this.conductWeight = Mathf.Clamp01(conductWeight);
            this.driveToRebellionRate = Mathf.Max(0f, driveToRebellionRate);
            this.supportShiftRate = Mathf.Max(0f, supportShiftRate);
            this.limitedTargetWeight = Mathf.Clamp01(limitedTargetWeight);
        }

        /// <summary>
        /// 既定＝腐敗重み0.5・暴政重み0.5・対抗成長0.15・義賊規律重み0.4・官逼民反0.12・民心移動0.1・限定標的0.3。
        /// </summary>
        public static CounterLegitimacyParams Default =>
            new CounterLegitimacyParams(0.5f, 0.5f, 0.15f, 0.4f, 0.12f, 0.1f, 0.3f);
    }

    /// <summary>
    /// 対抗的正統性＝「替天行道（天に替わって道を行う）」の純ロジック（SHZ-2 #1358・水滸伝）。
    /// 官（体制）の腐敗・暴政が酷いほど、それに対抗する義賊・反乱勢力の正統性が能動的に成長する＝
    /// 「官逼民反（官が民を反乱に追い込む）」。腐敗した体制が自ら反体制の大義を生み出し、
    /// 義賊は「悪い官を討つ」という対抗的正統性で民の支持を得る。
    /// 式の核：体制の正統性喪失＝腐敗×暴政（自滅的に大義を失う）／その喪失分が反勢力の対抗的正統性を
    /// 能動的に育てる（官が酷いほど義賊が正義に見える）／圧政が民を反乱へ生産し、民心が体制から義賊へ移る。
    /// <para>
    /// 分担：<see cref="ConsentRules"/> は被治者の協力＝体制側の統治力（協力×人口）を扱う。
    /// <see cref="InsurgencyRules"/>（生成済み）は占領地で外部支援された反乱の組織化＝反乱の物理的成長を扱う。
    /// <see cref="OutlawOrganizationRules"/> は義賊組織そのものの編成・結束（同EPIC SHZ）。
    /// <see cref="MetaLegitimacyRules"/>（生成済み）は大義名分一般の構築を扱う。
    /// 本クラスは「官の腐敗が義賊の正統性を育てる」対抗的正統性の一点に責任を持つ
    /// （体制の協力でも反乱の物理でもなく、大義の綱引きそのもの）。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CounterLegitimacyRules
    {
        /// <summary>
        /// 体制の正統性喪失（0..1）＝官の腐敗 corruption×腐敗重み＋暴政 oppression×暴政重み（合計を上限1へクランプ）。
        /// 官が腐敗し暴虐であるほど、体制は自ら正統性を失う＝反体制の大義を自前で生み出す自滅構造。
        /// </summary>
        public static float RegimeDelegitimation(float corruption, float oppression, CounterLegitimacyParams p)
        {
            float loss = Mathf.Clamp01(corruption) * p.corruptionWeight + Mathf.Clamp01(oppression) * p.oppressionWeight;
            return Mathf.Clamp01(loss);
        }

        public static float RegimeDelegitimation(float corruption, float oppression)
            => RegimeDelegitimation(corruption, oppression, CounterLegitimacyParams.Default);

        /// <summary>
        /// 対抗的正統性の1tick後（0..1）＝体制の正統性喪失 regimeDelegitimation×義賊の規律 rebelConduct で能動的に成長。
        /// 官が酷い（喪失大）ほど、かつ義賊が規律を保ち民を害さない（rebelConduct高）ほど対抗的正統性が育つ＝
        /// 「替天行道」。どちらか0なら育たない（官が清廉なら義は立たず、義賊が暴れれば正義に見えない）。
        /// </summary>
        public static float CounterLegitimacyGrowth(float counterLegitimacy, float regimeDelegitimation, float rebelConduct, float dt, CounterLegitimacyParams p)
        {
            float drive = Mathf.Clamp01(regimeDelegitimation) * Mathf.Clamp01(rebelConduct);
            float gain = p.counterGrowthRate * drive * Mathf.Max(0f, dt);
            return Mathf.Clamp01(Mathf.Clamp01(counterLegitimacy) + gain);
        }

        public static float CounterLegitimacyGrowth(float counterLegitimacy, float regimeDelegitimation, float rebelConduct, float dt)
            => CounterLegitimacyGrowth(counterLegitimacy, regimeDelegitimation, rebelConduct, dt, CounterLegitimacyParams.Default);

        /// <summary>
        /// 官逼民反の1tick分の反乱化圧（per dt）＝官の暴政 oppression×民の困窮 desperation で、官が民を反乱へ追い込む。
        /// 体制が圧政と困窮で自ら反乱を生産する＝どちらか欠ければ民は立たない（耐えられる圧政なら反乱に至らない）。
        /// </summary>
        public static float OfficialDrivesRebellion(float oppression, float desperation, float dt, CounterLegitimacyParams p)
        {
            float drive = Mathf.Clamp01(oppression) * Mathf.Clamp01(desperation);
            return Mathf.Max(0f, p.driveToRebellionRate * drive * Mathf.Max(0f, dt));
        }

        public static float OfficialDrivesRebellion(float oppression, float desperation, float dt)
            => OfficialDrivesRebellion(oppression, desperation, dt, CounterLegitimacyParams.Default);

        /// <summary>
        /// 道徳的優位（0..1・替天行道）＝対抗的正統性 counterLegitimacy×体制の正統性喪失 regimeDelegitimation。
        /// 義賊が「悪い官を討つ」道徳的高地を得る＝自らの正統性と相手の不正の両方が揃って初めて成立する
        /// （義賊が正統でも官が清廉なら高地は立たず、官が腐敗でも義賊に正統性が無ければ討つ大義に欠ける）。
        /// </summary>
        public static float MoralHighGround(float counterLegitimacy, float regimeDelegitimation)
            => Mathf.Clamp01(counterLegitimacy) * Mathf.Clamp01(regimeDelegitimation);

        /// <summary>
        /// 民の支持の1tick後（0..1・義賊への支持）＝対抗的正統性 counterLegitimacy と現在の体制支持 regimeSupport の差で、
        /// 民心が体制から義賊へ移る。義賊の正統性が体制支持を上回る分だけ支持が義賊へ流れる（下回れば義賊から離れる）。
        /// </summary>
        public static float PopularSupportShift(float counterLegitimacy, float regimeSupport, float dt, CounterLegitimacyParams p)
        {
            float pull = Mathf.Clamp01(counterLegitimacy) - Mathf.Clamp01(regimeSupport);
            float delta = p.supportShiftRate * pull * Mathf.Max(0f, dt);
            // 戻り値は「義賊への支持」の純増減。呼び出し側は前回の義賊支持に加算する想定だが、
            // ここでは綱引きの方向と大きさだけを返す（counterLegitimacy 自体を基準にクランプ）。
            return Mathf.Clamp01(Mathf.Clamp01(counterLegitimacy) + delta);
        }

        public static float PopularSupportShift(float counterLegitimacy, float regimeSupport, float dt)
            => PopularSupportShift(counterLegitimacy, regimeSupport, dt, CounterLegitimacyParams.Default);

        /// <summary>
        /// 限定的標的による正統性の保ち（0..1）＝義賊の規律 rebelConduct を土台に、腐敗官への標的集中 antiCorruptionFocus で上乗せ。
        /// 体制全体（皇帝）でなく腐敗官僚（奸臣）だけを討つほど対抗的正統性が保たれる＝水滸伝の「替天行道」＝
        /// 天子に弓を引かず悪い官を討つ。標的を絞るほど（focus高）民の支持を失わずに済む。
        /// </summary>
        public static float LimitedTarget(float rebelConduct, float antiCorruptionFocus, CounterLegitimacyParams p)
        {
            float baseConduct = Mathf.Clamp01(rebelConduct);
            float bonus = Mathf.Clamp01(antiCorruptionFocus) * p.limitedTargetWeight;
            return Mathf.Clamp01(baseConduct + bonus);
        }

        public static float LimitedTarget(float rebelConduct, float antiCorruptionFocus)
            => LimitedTarget(rebelConduct, antiCorruptionFocus, CounterLegitimacyParams.Default);

        /// <summary>
        /// 正統性の綱引き（-1..1）＝義賊の対抗的正統性 counterLegitimacy と体制の正統性 regimeLegitimacy の差。
        /// 正なら義賊が大義を握り（民心は義賊へ）、負なら体制が握る。どちらが「天に替わって道を行う」者かの均衡。
        /// </summary>
        public static float LegitimacyContest(float counterLegitimacy, float regimeLegitimacy)
            => Mathf.Clamp(Mathf.Clamp01(counterLegitimacy) - Mathf.Clamp01(regimeLegitimacy), -1f, 1f);

        /// <summary>
        /// 対抗的正統性を得た義の反乱（替天行道）か＝対抗的正統性 counterLegitimacy が threshold 以上。
        /// 閾値を超えた反乱は単なる賊ではなく、民に支持される義の反乱として成立する。
        /// </summary>
        public static bool IsRighteousRebellion(float counterLegitimacy, float threshold)
            => Mathf.Clamp01(counterLegitimacy) >= Mathf.Clamp01(threshold);
    }
}
