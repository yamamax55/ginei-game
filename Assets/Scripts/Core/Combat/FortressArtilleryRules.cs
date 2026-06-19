using UnityEngine;

namespace Ginei
{
    /// <summary>要塞主砲（トゥール・ハンマー型・超大型固定砲）の調整係数。</summary>
    public readonly struct FortressArtilleryParams
    {
        /// <summary>炉出力1・収束1のときの一撃の基準破壊力（威力の規模）。</summary>
        public readonly float yieldScale;
        /// <summary>威力1あたりの基準充填時間（強力なほど長い）。</summary>
        public readonly float chargeScale;
        /// <summary>射界の許容半角（これより方位がずれると捉えられない・度）。</summary>
        public readonly float arcHalfAngle;
        /// <summary>密集効果が威力に上乗せできる最大倍率（密集1で +この値）。</summary>
        public readonly float clusterGain;
        /// <summary>外したときの隙が無防備度1に届くチャージ時間（秒）。</summary>
        public readonly float missWindowScale;
        /// <summary>過充填の暴発リスクが立ち上がり始める充填率の閾値。</summary>
        public readonly float overchargeKnee;
        /// <summary>心理的威圧に実証戦果が効く強さ（0..1）。</summary>
        public readonly float intimidationWeight;

        public FortressArtilleryParams(float yieldScale, float chargeScale, float arcHalfAngle,
            float clusterGain, float missWindowScale, float overchargeKnee, float intimidationWeight)
        {
            this.yieldScale = Mathf.Max(0f, yieldScale);
            this.chargeScale = Mathf.Max(0f, chargeScale);
            this.arcHalfAngle = Mathf.Clamp(arcHalfAngle, 0f, 180f);
            this.clusterGain = Mathf.Max(0f, clusterGain);
            this.missWindowScale = Mathf.Max(0f, missWindowScale);
            this.overchargeKnee = Mathf.Clamp01(overchargeKnee);
            this.intimidationWeight = Mathf.Clamp01(intimidationWeight);
        }

        /// <summary>既定＝威力規模10・充填15/威力・射界半角30度・密集+1.0・隙60秒で無防備1・過充填膝0.8・威圧実証0.5。</summary>
        public static FortressArtilleryParams Default
            => new FortressArtilleryParams(10f, 15f, 30f, 1f, 60f, 0.8f, 0.5f);
    }

    /// <summary>
    /// 要塞の超大型主砲（トゥール・ハンマー型）の純ロジック。炉出力と収束装置が一撃の破壊力を決め、
    /// 威力が大きいほど蓄電が要る＝長い充填時間と、外したときの長い隙が表裏になる。砲は旋回不能ゆえ
    /// 要塞ごと向ける＝目標方位が射界に入るかで命中度が決まり、艦隊密集には威力が増幅して殲滅する。
    /// 過充填は暴発リスクを呼び、実証された戦果は心理的威圧を生む＝「撃つ価値があるか（殲滅効果が
    /// 隙に見合うか）」を判断する。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class FortressArtilleryRules
    {
        /// <summary>
        /// 一撃の破壊力＝炉出力×収束装置（実効値）×威力規模。収束は焦点を絞るほど威力が乗る。
        /// </summary>
        public static float BurstYield(float reactorOutput, float focusingArray, FortressArtilleryParams p)
        {
            float reactor = Mathf.Max(0f, reactorOutput);
            float focus = Mathf.Clamp01(focusingArray);
            return reactor * focus * p.yieldScale;
        }

        public static float BurstYield(float reactorOutput, float focusingArray)
            => BurstYield(reactorOutput, focusingArray, FortressArtilleryParams.Default);

        /// <summary>
        /// 充填時間（秒）＝威力×充填係数÷蓄電容量。強力なほど・蓄電が貧弱なほど長くかかる。
        /// 蓄電0は無限大（撃てない）。
        /// </summary>
        public static float ChargeTime(float burstYield, float capacitorBank, FortressArtilleryParams p)
        {
            float y = Mathf.Max(0f, burstYield);
            float cap = Mathf.Max(0f, capacitorBank);
            if (cap <= 0f) return float.PositiveInfinity;
            return y * p.chargeScale / cap;
        }

        public static float ChargeTime(float burstYield, float capacitorBank)
            => ChargeTime(burstYield, capacitorBank, FortressArtilleryParams.Default);

        /// <summary>
        /// 射界に捉える度合い（0..1）＝要塞の旋回（方位・度）と目標方位（度）の差が射界半角内なら
        /// 1へ、半角を超えると0へ線形に落ちる。旋回不能の固定砲を要塞ごと向ける表現。
        /// </summary>
        public static float FiringArc(float fortressRotation, float targetBearing, FortressArtilleryParams p)
        {
            float diff = Mathf.Abs(fortressRotation - targetBearing);
            diff = diff % 360f;
            if (diff > 180f) diff = 360f - diff;
            float half = p.arcHalfAngle;
            if (half <= 0f) return diff <= 0f ? 1f : 0f;
            float aligned = 1f - diff / half;
            return Mathf.Clamp01(aligned);
        }

        public static float FiringArc(float fortressRotation, float targetBearing)
            => FiringArc(fortressRotation, targetBearing, FortressArtilleryParams.Default);

        /// <summary>
        /// 艦隊密集への殲滅効果＝威力×(1 + 密集×密集ゲイン)。敵が固まっているほど一撃が薙ぎ払う。
        /// </summary>
        public static float ClusterEffect(float burstYield, float enemyDensity, FortressArtilleryParams p)
        {
            float y = Mathf.Max(0f, burstYield);
            float density = Mathf.Clamp01(enemyDensity);
            return y * (1f + density * p.clusterGain);
        }

        public static float ClusterEffect(float burstYield, float enemyDensity)
            => ClusterEffect(burstYield, enemyDensity, FortressArtilleryParams.Default);

        /// <summary>
        /// 外したとき（または撃つまで）の無防備な隙（0..1）＝充填時間が長いほど大きい。
        /// 充填中は次弾を撃てず要塞は隙だらけ＝missWindowScale 秒で1に飽和。
        /// </summary>
        public static float MissedShotWindow(float chargeTime, FortressArtilleryParams p)
        {
            float t = Mathf.Max(0f, chargeTime);
            if (p.missWindowScale <= 0f) return t > 0f ? 1f : 0f;
            return Mathf.Clamp01(t / p.missWindowScale);
        }

        public static float MissedShotWindow(float chargeTime)
            => MissedShotWindow(chargeTime, FortressArtilleryParams.Default);

        /// <summary>
        /// 過充填の暴発・故障リスク（0..1）＝充填率が膝（overchargeKnee）を超えたぶんを膝〜1で正規化。
        /// 膝以下は0、満充填1で1。安全圏を超えて威力を絞り出すほど壊れる賭け。
        /// </summary>
        public static float OverchargeRisk(float chargeRatio, FortressArtilleryParams p)
        {
            float r = Mathf.Clamp01(chargeRatio);
            float knee = p.overchargeKnee;
            if (knee >= 1f) return r >= 1f ? 1f : 0f;
            float over = (r - knee) / (1f - knee);
            return Mathf.Clamp01(over);
        }

        public static float OverchargeRisk(float chargeRatio)
            => OverchargeRisk(chargeRatio, FortressArtilleryParams.Default);

        /// <summary>
        /// 心理的威圧（0..1）＝威力（規模で正規化）に実証された戦果を重み付けで上乗せ。
        /// 撃って見せた実績があるほど敵を竦ませる＝抑止としての主砲。
        /// </summary>
        public static float PsychologicalIntimidation(float burstYield, float demonstratedFirepower, FortressArtilleryParams p)
        {
            float baseScale = p.yieldScale > 0f ? p.yieldScale : 1f;
            float y = Mathf.Clamp01(Mathf.Max(0f, burstYield) / (baseScale * 10f));
            float demo = Mathf.Clamp01(demonstratedFirepower);
            float w = p.intimidationWeight;
            float intimidation = y * (1f - w) + y * demo * w + demo * w * (1f - y);
            return Mathf.Clamp01(intimidation);
        }

        public static float PsychologicalIntimidation(float burstYield, float demonstratedFirepower)
            => PsychologicalIntimidation(burstYield, demonstratedFirepower, FortressArtilleryParams.Default);

        /// <summary>
        /// 撃つ価値があるか＝殲滅効果（密集で増幅）が外したときの隙に見合うか。
        /// clusterEffect ÷ (1 + missedShotWindow) が閾値以上なら true。隙が大きいほど割に合わなくなる。
        /// </summary>
        public static bool IsWorthFiring(float clusterEffect, float missedShotWindow, float threshold)
        {
            float effect = Mathf.Max(0f, clusterEffect);
            float window = Mathf.Clamp01(missedShotWindow);
            float ratio = effect / (1f + window);
            return ratio >= threshold;
        }

        public static bool IsWorthFiring(float clusterEffect, float missedShotWindow)
            => IsWorthFiring(clusterEffect, missedShotWindow, 5f);
    }
}
