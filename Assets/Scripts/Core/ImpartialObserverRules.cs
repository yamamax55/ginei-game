using UnityEngine;

namespace Ginei
{
    /// <summary>胸中の公平な観察者の調整係数（TMS-2 #1582・アダム・スミス『道徳感情論』）。</summary>
    public readonly struct ImpartialObserverParams
    {
        /// <summary>私利が自己欺瞞へ寄与する重み。</summary>
        public readonly float selfInterestWeight;
        /// <summary>激情が自己欺瞞へ寄与する重み。</summary>
        public readonly float passionWeight;
        /// <summary>良心が観察者の強さへ寄与する重み。</summary>
        public readonly float conscienceWeight;
        /// <summary>社会の目（見られている意識）が観察者の強さへ寄与する重み。</summary>
        public readonly float exposureWeight;
        /// <summary>観察者が最大でも腐敗速度をここまでしか落とせない（下限倍率）。</summary>
        public readonly float minBrake;
        /// <summary>良心の涵養速度/秒（道徳的実践1のとき）。</summary>
        public readonly float conscienceGrowthRate;
        /// <summary>良心の衰え速度/秒（腐敗圧1のとき）。</summary>
        public readonly float conscienceDecayRate;

        public ImpartialObserverParams(float selfInterestWeight, float passionWeight, float conscienceWeight,
            float exposureWeight, float minBrake, float conscienceGrowthRate, float conscienceDecayRate)
        {
            this.selfInterestWeight = selfInterestWeight;
            this.passionWeight = passionWeight;
            this.conscienceWeight = conscienceWeight;
            this.exposureWeight = exposureWeight;
            this.minBrake = minBrake;
            this.conscienceGrowthRate = conscienceGrowthRate;
            this.conscienceDecayRate = conscienceDecayRate;
        }

        public static ImpartialObserverParams Default =>
            new ImpartialObserverParams(0.6f, 0.4f, 0.6f, 0.4f, 0.4f, 0.1f, 0.1f);
    }

    /// <summary>
    /// 公平な観察者フィルター＝アダム・スミス『道徳感情論』の「胸中の公平な観察者（impartial spectator）」の
    /// 純ロジック（TMS-2 #1582）。私利（private interest）と激情は自己を甘く評価する<b>自己欺瞞</b>を生むが、
    /// 内なる公平な観察者が強いほどそれを正し、腐敗の加速にブレーキをかける。
    /// 「私利は自己を甘く見る自己欺瞞を生み、内なる公平な観察者が強いほどそれを正し腐敗を遅らせる」を式に出す。
    /// 観察者の強さは良心の涵養×社会の目で育ち、弱いと私利は自己合理化される。
    /// <para>
    /// 分担：<see cref="RegimeRules"/>（腐敗が制度疲労で進む実体）とは別＝こちらは内なる観察者による
    /// 自己欺瞞の補正と腐敗加速のブレーキ倍率を返す（<see cref="RegimeRules"/> の腐敗速度に掛ける）。
    /// 同 EPIC TMS の <c>EmpathyRules</c>（共感＝他者の感情を内に映す）とは別＝こちらは自己を第三者の目で
    /// 見る自制。<see cref="SecurityRules"/>（秘密警察＝外からの監視・抑圧）とは別＝内なる良心による自律。
    /// </para>
    /// 全入力クランプ・乱数なし決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ImpartialObserverRules
    {
        /// <summary>
        /// 自己欺瞞バイアス（0..1）＝私利と激情が自己を甘く見させる度合い。
        /// 人は自分に都合よく判断する＝私利と激情が強いほど大きい。
        /// </summary>
        public static float SelfDeceptionBias(float selfInterest, float passion, ImpartialObserverParams p)
        {
            float si = Mathf.Clamp01(selfInterest);
            float pa = Mathf.Clamp01(passion);
            float wsum = p.selfInterestWeight + p.passionWeight;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((p.selfInterestWeight * si + p.passionWeight * pa) / wsum);
        }

        public static float SelfDeceptionBias(float selfInterest, float passion) =>
            SelfDeceptionBias(selfInterest, passion, ImpartialObserverParams.Default);

        /// <summary>
        /// 内なる公平な観察者の強さ（0..1）＝良心の涵養×社会の目。
        /// 見られている意識が良心を育てる＝良心と社会の露出が高いほど観察者が強い。
        /// </summary>
        public static float ObserverStrength(float conscience, float socialExposure, ImpartialObserverParams p)
        {
            float c = Mathf.Clamp01(conscience);
            float e = Mathf.Clamp01(socialExposure);
            float wsum = p.conscienceWeight + p.exposureWeight;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((p.conscienceWeight * c + p.exposureWeight * e) / wsum);
        }

        public static float ObserverStrength(float conscience, float socialExposure) =>
            ObserverStrength(conscience, socialExposure, ImpartialObserverParams.Default);

        /// <summary>
        /// 甘い自己評価を公平へ引き戻す（補正後の判断 0..1）。観察者が強いほど偏りが減る。
        /// 公平な基準を 0.5 とし、甘い自己評価を観察者の強さに比例して中庸へ寄せる。
        /// </summary>
        public static float CorrectedJudgment(float selfJudgment, float selfDeceptionBias, float observerStrength)
        {
            float sj = Mathf.Clamp01(selfJudgment);
            float bias = Mathf.Clamp01(selfDeceptionBias);
            float obs = Mathf.Clamp01(observerStrength);
            // 甘さ＝自己評価のうち欺瞞バイアスで嵩上げされた分。観察者が強いほどその分を削る。
            float inflated = sj * bias;
            return Mathf.Clamp01(sj - inflated * obs);
        }

        /// <summary>
        /// 腐敗加速のブレーキ倍率（minBrake..1）。観察者が強いほど腐敗の進行を遅らせる。
        /// <see cref="RegimeRules"/>/<see cref="DynastyRules"/> の腐敗速度に掛ける減速（1.0 以下）。
        /// </summary>
        public static float CorruptionBrake(float observerStrength, ImpartialObserverParams p)
        {
            float obs = Mathf.Clamp01(observerStrength);
            return Mathf.Clamp(Mathf.Lerp(1f, p.minBrake, obs), p.minBrake, 1f);
        }

        public static float CorruptionBrake(float observerStrength) =>
            CorruptionBrake(observerStrength, ImpartialObserverParams.Default);

        /// <summary>
        /// 道徳的自己合理化（0..1）＝観察者が弱いと私利を正当化する度合い。
        /// 私利が高く観察者が弱いほど大きい（私利×(1-観察者の強さ)）。
        /// </summary>
        public static float MoralRationalization(float selfInterest, float observerStrength)
        {
            float si = Mathf.Clamp01(selfInterest);
            float obs = Mathf.Clamp01(observerStrength);
            return Mathf.Clamp01(si * (1f - obs));
        }

        /// <summary>
        /// 良心を dt 進める。道徳的実践で育ち、腐敗環境（腐敗圧）で衰える。
        /// 涵養＝実践×成長速度、衰え＝腐敗圧×衰え速度。
        /// </summary>
        public static float ConscienceTick(float conscience, float moralPractice, float corruptingPressure,
            float dt, ImpartialObserverParams p)
        {
            if (dt <= 0f) return Mathf.Clamp01(conscience);
            float c = Mathf.Clamp01(conscience);
            float practice = Mathf.Clamp01(moralPractice);
            float pressure = Mathf.Clamp01(corruptingPressure);
            float gain = p.conscienceGrowthRate * practice * dt;
            float loss = p.conscienceDecayRate * pressure * dt;
            return Mathf.Clamp01(c + gain - loss);
        }

        public static float ConscienceTick(float conscience, float moralPractice, float corruptingPressure, float dt) =>
            ConscienceTick(conscience, moralPractice, corruptingPressure, dt, ImpartialObserverParams.Default);

        /// <summary>
        /// 観察者が私利に呑まれたか＝自己腐敗の判定。
        /// 私利が観察者の強さを threshold ぶん上回ったら true（内なる良心が私利に負けた）。
        /// </summary>
        public static bool IsSelfCorrupted(float observerStrength, float selfInterest, float threshold)
        {
            float obs = Mathf.Clamp01(observerStrength);
            float si = Mathf.Clamp01(selfInterest);
            float th = Mathf.Clamp01(threshold);
            return si - obs > th;
        }

        /// <summary>
        /// 「称賛に値することvs称賛されること」（praiseworthiness vs praise）の乖離（-1..1）。
        /// スミス＝実徳（称賛に値する）と虚栄（称賛されたいだけ）の差。
        /// 正＝実徳が虚栄を上回る（真の徳）、負＝称賛を求めるだけの虚栄が勝る。
        /// </summary>
        public static float PraiseworthinessVsPraise(float actualVirtue, float soughtPraise)
        {
            float v = Mathf.Clamp01(actualVirtue);
            float s = Mathf.Clamp01(soughtPraise);
            return Mathf.Clamp(v - s, -1f, 1f);
        }
    }
}
