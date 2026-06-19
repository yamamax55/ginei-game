using UnityEngine;

namespace Ginei
{
    /// <summary>教化・思想注入（信念体系の植え付け）の調整係数。</summary>
    public readonly struct IndoctrinationParams
    {
        /// <summary>教化強度に占めるメッセージ反復の重み（残りが環境統制の重み）。</summary>
        public readonly float repetitionWeight;
        /// <summary>批判的思考の抑圧に占める情報遮断の重み（残りが教化強度の重み）。</summary>
        public readonly float isolationWeight;
        /// <summary>批判的思考が完全に育つ基準年齢（これ以上は受容性が下限へ）。</summary>
        public readonly float criticalAge;
        /// <summary>最も若い層の受容性の最大値（年齢0で得る上限の受容性）。</summary>
        public readonly float youthReceptivity;
        /// <summary>受容性が0でも残る最低受容性（大人でもゼロにはしない床）。</summary>
        public readonly float adultReceptivityFloor;
        /// <summary>抵抗に占める既存信念の重み（既存信念がどれだけ防壁になるか）。</summary>
        public readonly float priorBeliefWeight;
        /// <summary>抵抗に占める批判力の重み。</summary>
        public readonly float criticalWeight;
        /// <summary>抵抗に占める外部接触の重み（priorBeliefWeight+criticalWeight+exposureWeight=1へ正規化）。</summary>
        public readonly float exposureWeight;
        /// <summary>制度的統制1のときの再生産強化幅（教化世代が次世代へ受け継がせる上乗せ）。</summary>
        public readonly float institutionalBoost;
        /// <summary>教化が効いたとみなす既定の信念採用しきい値。</summary>
        public readonly float effectiveThreshold;

        public IndoctrinationParams(float repetitionWeight, float isolationWeight, float criticalAge,
                                    float youthReceptivity, float adultReceptivityFloor,
                                    float priorBeliefWeight, float criticalWeight, float exposureWeight,
                                    float institutionalBoost, float effectiveThreshold)
        {
            this.repetitionWeight = Mathf.Clamp01(repetitionWeight);
            this.isolationWeight = Mathf.Clamp01(isolationWeight);
            this.criticalAge = Mathf.Max(1f, criticalAge);
            this.youthReceptivity = Mathf.Clamp01(youthReceptivity);
            this.adultReceptivityFloor = Mathf.Clamp01(adultReceptivityFloor);
            this.priorBeliefWeight = Mathf.Max(0f, priorBeliefWeight);
            this.criticalWeight = Mathf.Max(0f, criticalWeight);
            this.exposureWeight = Mathf.Max(0f, exposureWeight);
            this.institutionalBoost = Mathf.Clamp01(institutionalBoost);
            this.effectiveThreshold = Mathf.Clamp01(effectiveThreshold);
        }

        /// <summary>既定＝反復0.6/遮断0.5・批判育成年齢30・若年受容0.9/大人床0.2・抵抗重み(信念0.4/批判0.4/接触0.2)・制度0.4・効力閾値0.5。</summary>
        public static IndoctrinationParams Default =>
            new IndoctrinationParams(0.6f, 0.5f, 30f, 0.9f, 0.2f, 0.4f, 0.4f, 0.2f, 0.4f, 0.5f);
    }

    /// <summary>
    /// 教化・思想注入の純ロジック（信念体系の植え付け）。メッセージの反復と環境統制で教化の強度が生まれ、
    /// 情報遮断が批判的思考を抑圧する。受容性は若年層ほど高く、強度×受容性×思考抑圧で思想が内面化する。
    /// 既存信念・批判力・外部接触は教化への抵抗を生み、内面化×(1−抵抗)で信念が採用される。採用された
    /// 信念は制度的統制を通じて次の教化世代へ再生産される。
    /// <para>
    /// 純ロジック（非 MonoBehaviour・盤面非依存の plain 引数・実効値パターン）。乱数は持たない（決定論）。
    /// 値は 0..1 域で表現し、入力は全て Clamp、配列は持たない（スカラのみ）。test-first。
    /// </para>
    /// </summary>
    public static class IndoctrinationRules
    {
        /// <summary>
        /// 教化の強度（0..1）＝メッセージ反復 messageRepetition(0..1) と環境統制 environmentalControl(0..1) の
        /// 重み付き和。反復だけでも統制だけでも片寄せできるが、両方揃うほど強い。
        /// </summary>
        public static float IndoctrinationIntensity(float messageRepetition, float environmentalControl,
                                                    IndoctrinationParams p)
        {
            float rep = Mathf.Clamp01(messageRepetition);
            float ctrl = Mathf.Clamp01(environmentalControl);
            float intensity = rep * p.repetitionWeight + ctrl * (1f - p.repetitionWeight);
            return Mathf.Clamp01(intensity);
        }

        public static float IndoctrinationIntensity(float messageRepetition, float environmentalControl)
            => IndoctrinationIntensity(messageRepetition, environmentalControl, IndoctrinationParams.Default);

        /// <summary>
        /// 批判的思考の抑圧（0..1）＝教化強度 indoctrinationIntensity(0..1) と情報遮断 informationIsolation(0..1) の
        /// 重み付き和。情報を遮断するほど（外部の反証が届かないほど）批判が育たない。
        /// </summary>
        public static float CriticalThinkingSuppression(float indoctrinationIntensity, float informationIsolation,
                                                        IndoctrinationParams p)
        {
            float intensity = Mathf.Clamp01(indoctrinationIntensity);
            float iso = Mathf.Clamp01(informationIsolation);
            float suppression = iso * p.isolationWeight + intensity * (1f - p.isolationWeight);
            return Mathf.Clamp01(suppression);
        }

        public static float CriticalThinkingSuppression(float indoctrinationIntensity, float informationIsolation)
            => CriticalThinkingSuppression(indoctrinationIntensity, informationIsolation, IndoctrinationParams.Default);

        /// <summary>
        /// 年齢による受容性（adultReceptivityFloor..youthReceptivity）＝若いほど刷り込まれやすい。
        /// 年齢0で youthReceptivity、批判育成年齢 criticalAge で adultReceptivityFloor へ線形に低下し、
        /// それ以上は床のまま（大人もゼロにはしない）。
        /// </summary>
        public static float AgeReceptivity(float targetAge, IndoctrinationParams p)
        {
            float age = Mathf.Max(0f, targetAge);
            float t = Mathf.Clamp01(age / p.criticalAge);
            return Mathf.Lerp(p.youthReceptivity, p.adultReceptivityFloor, t);
        }

        public static float AgeReceptivity(float targetAge)
            => AgeReceptivity(targetAge, IndoctrinationParams.Default);

        /// <summary>
        /// 思想の内面化（0..1）＝教化強度 × 年齢受容性 × 批判的思考抑圧。三者の積＝強く刷り込まれ、若く、
        /// 批判が抑えられているほど深く内面化する（どれか一つが欠ければ内面化は浅い）。
        /// </summary>
        public static float Internalization(float indoctrinationIntensity, float ageReceptivity,
                                            float criticalThinkingSuppression, IndoctrinationParams p)
        {
            float intensity = Mathf.Clamp01(indoctrinationIntensity);
            float recept = Mathf.Clamp01(ageReceptivity);
            float suppress = Mathf.Clamp01(criticalThinkingSuppression);
            return Mathf.Clamp01(intensity * recept * suppress);
        }

        public static float Internalization(float indoctrinationIntensity, float ageReceptivity,
                                            float criticalThinkingSuppression)
            => Internalization(indoctrinationIntensity, ageReceptivity, criticalThinkingSuppression,
                               IndoctrinationParams.Default);

        /// <summary>
        /// 教化への抵抗（0..1）＝既存信念 priorBeliefs(0..1)・批判力 criticalCapacity(0..1)・外部接触
        /// exposureToAlternatives(0..1) の重み付き和（重みは三者で正規化）。確たる信念・批判力・外の世界への
        /// 接触が、植え付けを跳ね返す防壁になる。
        /// </summary>
        public static float Resistance(float priorBeliefs, float criticalCapacity, float exposureToAlternatives,
                                       IndoctrinationParams p)
        {
            float prior = Mathf.Clamp01(priorBeliefs);
            float crit = Mathf.Clamp01(criticalCapacity);
            float expo = Mathf.Clamp01(exposureToAlternatives);
            float wSum = p.priorBeliefWeight + p.criticalWeight + p.exposureWeight;
            if (wSum <= 0f) return 0f;
            float resistance = (prior * p.priorBeliefWeight + crit * p.criticalWeight + expo * p.exposureWeight) / wSum;
            return Mathf.Clamp01(resistance);
        }

        public static float Resistance(float priorBeliefs, float criticalCapacity, float exposureToAlternatives)
            => Resistance(priorBeliefs, criticalCapacity, exposureToAlternatives, IndoctrinationParams.Default);

        /// <summary>
        /// 信念の採用（0..1）＝内面化 × (1−抵抗)。深く内面化しても抵抗が強ければ採用されず、抵抗が無ければ
        /// 内面化のぶんがそのまま信念として根付く。
        /// </summary>
        public static float BeliefAdoption(float internalization, float resistance, IndoctrinationParams p)
        {
            float intern = Mathf.Clamp01(internalization);
            float resist = Mathf.Clamp01(resistance);
            return Mathf.Clamp01(intern * (1f - resist));
        }

        public static float BeliefAdoption(float internalization, float resistance)
            => BeliefAdoption(internalization, resistance, IndoctrinationParams.Default);

        /// <summary>
        /// 教化世代の再生産（0..1）＝信念採用 beliefAdoption(0..1) を、制度的統制 institutionalControl(0..1) が
        /// 増幅して次世代へ受け継がせる強さ。(1 + institutionalControl×institutionalBoost) 倍＝制度（学校・
        /// 報道・組織）が強いほど教化された世代が自己再生産する。
        /// </summary>
        public static float GenerationalReproduction(float beliefAdoption, float institutionalControl,
                                                     IndoctrinationParams p)
        {
            float adoption = Mathf.Clamp01(beliefAdoption);
            float control = Mathf.Clamp01(institutionalControl);
            return Mathf.Clamp01(adoption * (1f + control * p.institutionalBoost));
        }

        public static float GenerationalReproduction(float beliefAdoption, float institutionalControl)
            => GenerationalReproduction(beliefAdoption, institutionalControl, IndoctrinationParams.Default);

        /// <summary>教化が効いたか＝信念採用が閾値 threshold(0..1) を超える（同値は不成立）。</summary>
        public static bool IsIndoctrinationEffective(float beliefAdoption, float threshold)
        {
            return Mathf.Clamp01(beliefAdoption) > Mathf.Clamp01(threshold);
        }

        /// <summary>既定閾値（<see cref="IndoctrinationParams.Default"/> の effectiveThreshold）委譲版。</summary>
        public static bool IsIndoctrinationEffective(float beliefAdoption)
            => IsIndoctrinationEffective(beliefAdoption, IndoctrinationParams.Default.effectiveThreshold);
    }
}
