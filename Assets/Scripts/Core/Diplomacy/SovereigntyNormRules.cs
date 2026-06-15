using UnityEngine;

namespace Ginei
{
    /// <summary>主権規範（ウェストファリア体制）の調整係数。</summary>
    public readonly struct SovereigntyNormParams
    {
        /// <summary>規範成熟の基礎速度（戦争の惨禍×条約の確立が満ちたとき毎秒これだけ成熟する）。</summary>
        public readonly float maturationRate;
        /// <summary>規範成熟への戦争トラウマの寄与（干渉戦争の惨禍が規範を生む重み）。</summary>
        public readonly float warTraumaWeight;
        /// <summary>規範成熟への条約確立の寄与（ウェストファリア条約＝制度的裏付けの重み）。</summary>
        public readonly float treatyWeight;
        /// <summary>規範が干渉の正当性を削る強さ（成熟した規範ほど内政干渉を不当とみなす）。</summary>
        public readonly float interventionSuppression;
        /// <summary>力の非対称が建前を侵食する強さ（大国は規範を破りうる＝主権平等の限界）。</summary>
        public readonly float asymmetryErosion;
        /// <summary>規範違反の外交コスト基礎倍率（確立した規範を破ると国際社会の非難を招く）。</summary>
        public readonly float violationCostWeight;
        /// <summary>主権国家体系が確立したと見なす規範成熟度の閾値。</summary>
        public readonly float westphalianThreshold;

        public SovereigntyNormParams(float maturationRate, float warTraumaWeight, float treatyWeight,
            float interventionSuppression, float asymmetryErosion, float violationCostWeight, float westphalianThreshold)
        {
            this.maturationRate = Mathf.Clamp01(maturationRate);
            this.warTraumaWeight = Mathf.Clamp01(warTraumaWeight);
            this.treatyWeight = Mathf.Clamp01(treatyWeight);
            this.interventionSuppression = Mathf.Clamp01(interventionSuppression);
            this.asymmetryErosion = Mathf.Clamp01(asymmetryErosion);
            this.violationCostWeight = Mathf.Max(0f, violationCostWeight);
            this.westphalianThreshold = Mathf.Clamp01(westphalianThreshold);
        }

        /// <summary>既定＝成熟速度0.1/s・トラウマ0.6/条約0.4（合計1.0）・干渉抑制0.9・力非対称侵食0.5・違反コスト1.0・体制確立閾値0.7。</summary>
        public static SovereigntyNormParams Default =>
            new SovereigntyNormParams(0.1f, 0.6f, 0.4f, 0.9f, 0.5f, 1.0f, 0.7f);
    }

    /// <summary>
    /// 主権規範の純ロジック（TYW-5 #1428・三十年戦争／ウェストファリア体制）＝主権国家体系の確立。
    /// 「宗教を口実にした干渉戦争（三十年戦争）の惨禍の後、領土主権・内政不干渉の原則が国際秩序の
    /// 基礎になった」を式に出す：干渉戦争の惨禍×条約の確立で主権規範が時間で成熟し
    /// （<see cref="NormMaturity"/>）、規範が成熟するほど内政干渉（宗教・イデオロギー口実）の正当性が
    /// 下がり（<see cref="InterventionLegitimacy"/>/<see cref="NonInterferenceNorm"/>）、宗教を口実にした
    /// 干渉の正当性が蝕まれ（<see cref="ReligiousPretextDecay"/>＝cuius regio＝領主の宗教が領内を決める）、
    /// 規範上は主権平等でも現実の力の差が建前を侵食し（<see cref="SovereignEquality"/>）、確立した規範を
    /// 破れば外交的コストを払い（<see cref="NormViolationCost"/>）、主権規範と勢力均衡が国際秩序を
    /// 安定させる（<see cref="SystemStability"/>/<see cref="IsWestphalianOrder"/>）。
    /// <see cref="DiplomacyRules"/>（外交状態の遷移・関係値）とは別＝こちらは内政不干渉という<b>規範</b>の成熟を扱う。
    /// <see cref="InfluenceRules"/>（勢力圏の非公式な浸透）とは別＝規範は浸透の正当性を制約する側。
    /// <see cref="ReligionRules"/>（宗教の社会力学）とは別＝こちらは宗教を口実にした<b>干渉</b>の正当性低下を扱う。
    /// <see cref="WarPurposeDriftRules"/>（同EPIC TYW・宗教→権力への戦争目的の漂流）とは別＝あちらは戦争目的の変質、
    /// こちらは戦後に成熟する国際秩序の規範＝惨禍が規範を生む後段を扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SovereigntyNormRules
    {
        /// <summary>
        /// 主権規範の成熟（戻り値＝新しい規範成熟度0..1）。干渉戦争の惨禍×条約の確立で時間成熟する：
        /// 成熟駆動＝成熟速度×(戦争トラウマ×warTraumaWeight＋条約確立×treatyWeight)。
        /// 戦争の惨禍が規範を生み、条約が制度的裏付けを与える＝両方そろうほど速く成熟する。dtは負を0に。
        /// </summary>
        public static float NormMaturity(float sovereigntyNorm, float warTrauma, float treatyEstablishment, float dt, SovereigntyNormParams p)
        {
            float norm = Mathf.Clamp01(sovereigntyNorm);
            float trauma = Mathf.Clamp01(warTrauma);
            float treaty = Mathf.Clamp01(treatyEstablishment);
            float t = Mathf.Max(0f, dt);
            float drive = p.maturationRate * (p.warTraumaWeight * trauma + p.treatyWeight * treaty);
            return Mathf.Clamp01(norm + drive * t);
        }

        public static float NormMaturity(float sovereigntyNorm, float warTrauma, float treatyEstablishment, float dt)
            => NormMaturity(sovereigntyNorm, warTrauma, treatyEstablishment, dt, SovereigntyNormParams.Default);

        /// <summary>
        /// 内政干渉の正当性（0..1）＝口実の強さ×(1−主権規範×干渉抑制)。主権規範が成熟するほど
        /// 宗教・イデオロギーを口実にした干渉の正当性が下がる＝惨禍を経て干渉戦争が割に合わなくなる。
        /// 規範ゼロなら口実がそのまま正当性になり、規範満点なら(1−interventionSuppression)まで圧縮される。
        /// </summary>
        public static float InterventionLegitimacy(float sovereigntyNorm, float pretext, SovereigntyNormParams p)
        {
            float norm = Mathf.Clamp01(sovereigntyNorm);
            float pre = Mathf.Clamp01(pretext);
            return Mathf.Clamp01(pre * (1f - norm * p.interventionSuppression));
        }

        public static float InterventionLegitimacy(float sovereigntyNorm, float pretext)
            => InterventionLegitimacy(sovereigntyNorm, pretext, SovereigntyNormParams.Default);

        /// <summary>
        /// 内政不干渉の原則の強さ（0..1）＝主権規範そのもの。成熟した規範ほど他国の内政（特に宗教）への
        /// 干渉を抑える＝ウェストファリアの核となる原則。規範に正比例。
        /// </summary>
        public static float NonInterferenceNorm(float sovereigntyNorm)
        {
            return Mathf.Clamp01(sovereigntyNorm);
        }

        /// <summary>
        /// 宗教口実の正当性の減衰（0..1＝蝕まれて残る正当性）＝宗教的正当化×(1−主権規範)。
        /// 主権規範が宗教を口実にした干渉の正当性を蝕む＝cuius regio, eius religio（領主の宗教が領内を決める）＝
        /// 領内の宗教は領主の専権で他国は容喙できない。規範満点なら宗教口実は完全に無効化される。
        /// </summary>
        public static float ReligiousPretextDecay(float sovereigntyNorm, float religiousJustification)
        {
            float norm = Mathf.Clamp01(sovereigntyNorm);
            float just = Mathf.Clamp01(religiousJustification);
            return Mathf.Clamp01(just * (1f - norm));
        }

        /// <summary>
        /// 実効的な主権平等（0..1）＝主権規範×(1−力の非対称×asymmetryErosion)。規範上は主権平等だが、
        /// 現実の力の差が建前を侵食する＝大国は規範を破りうる。力が対称（asymmetry=0）なら規範どおりの
        /// 平等が保たれ、非対称が大きいほど建前が削られる。
        /// </summary>
        public static float SovereignEquality(float sovereigntyNorm, float powerAsymmetry, SovereigntyNormParams p)
        {
            float norm = Mathf.Clamp01(sovereigntyNorm);
            float asym = Mathf.Clamp01(powerAsymmetry);
            return Mathf.Clamp01(norm * (1f - asym * p.asymmetryErosion));
        }

        public static float SovereignEquality(float sovereigntyNorm, float powerAsymmetry)
            => SovereignEquality(sovereigntyNorm, powerAsymmetry, SovereigntyNormParams.Default);

        /// <summary>
        /// 規範違反の外交コスト（0..）＝違反の大きさ×主権規範×violationCostWeight。確立した規範を破ると
        /// 国際社会の非難という外交的コストを払う＝規範の拘束力。規範が未成熟なら破っても咎められず
        /// コスト0、成熟した規範下では違反が大きいほど高くつく。
        /// </summary>
        public static float NormViolationCost(float sovereigntyNorm, float violation, SovereigntyNormParams p)
        {
            float norm = Mathf.Clamp01(sovereigntyNorm);
            float v = Mathf.Clamp01(violation);
            return Mathf.Max(0f, v * norm * p.violationCostWeight);
        }

        public static float NormViolationCost(float sovereigntyNorm, float violation)
            => NormViolationCost(sovereigntyNorm, violation, SovereigntyNormParams.Default);

        /// <summary>
        /// 国際秩序の安定度（0..1）＝規範成熟×勢力均衡。主権規範と勢力均衡の両輪がそろってこそ
        /// ウェストファリア体制は安定する＝どちらか欠ければ秩序は揺らぐ（積＝相補的）。
        /// </summary>
        public static float SystemStability(float normMaturity, float balanceOfPower)
        {
            float norm = Mathf.Clamp01(normMaturity);
            float bop = Mathf.Clamp01(balanceOfPower);
            return Mathf.Clamp01(norm * bop);
        }

        /// <summary>
        /// 主権国家体系が確立したかの判定＝主権規範が threshold 以上か。閾値超えで「ウェストファリア体制＝
        /// 領土主権・内政不干渉が国際秩序の基礎になった」と見なす。
        /// </summary>
        public static bool IsWestphalianOrder(float sovereigntyNorm, float threshold)
        {
            return Mathf.Clamp01(sovereigntyNorm) >= Mathf.Clamp01(threshold);
        }

        public static bool IsWestphalianOrder(float sovereigntyNorm, SovereigntyNormParams p)
            => IsWestphalianOrder(sovereigntyNorm, p.westphalianThreshold);

        public static bool IsWestphalianOrder(float sovereigntyNorm)
            => IsWestphalianOrder(sovereigntyNorm, SovereigntyNormParams.Default.westphalianThreshold);
    }
}
