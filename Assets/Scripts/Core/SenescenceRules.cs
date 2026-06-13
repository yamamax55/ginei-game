using UnityEngine;

namespace Ginei
{
    /// <summary>名将の衰え（加齢曲線）の調整係数。</summary>
    public readonly struct SenescenceParams
    {
        /// <summary>全盛期の終わり＝峠の年齢（ここまでは能力満額）。</summary>
        public readonly float peakAge;
        /// <summary>峠を越えてからの低下速度（超過1歳あたりの低下率・体力系の基準）。</summary>
        public readonly float declineRate;
        /// <summary>能力倍率の下限（老いても最低これだけは残る）。</summary>
        public readonly float minFactor;
        /// <summary>判断系（統率・運営）の峠が体力系より遅れる年数。</summary>
        public readonly float judgmentPeakDelay;
        /// <summary>判断系の低下速度の比（体力系の declineRate に掛ける。0..1＝判断は遅く衰える）。</summary>
        public readonly float judgmentRateRatio;
        /// <summary>自己認識の遅れ年数（本人は何年前の自分だと思っているか＝衰えに気づかない核）。</summary>
        public readonly float awarenessLag;
        /// <summary>名誉ある引き際の窓の長さ（峠の直後この年数だけ開く）。</summary>
        public readonly float exitWindowYears;

        public SenescenceParams(float peakAge, float declineRate, float minFactor,
                                float judgmentPeakDelay, float judgmentRateRatio,
                                float awarenessLag, float exitWindowYears)
        {
            this.peakAge = Mathf.Max(0f, peakAge);
            this.declineRate = Mathf.Max(0f, declineRate);
            this.minFactor = Mathf.Clamp01(minFactor);
            this.judgmentPeakDelay = Mathf.Max(0f, judgmentPeakDelay);
            this.judgmentRateRatio = Mathf.Clamp01(judgmentRateRatio);
            this.awarenessLag = Mathf.Max(0f, awarenessLag);
            this.exitWindowYears = Mathf.Max(0f, exitWindowYears);
        }

        /// <summary>既定＝峠45歳・低下率0.01/年・下限0.6・判断系の峠+10年・判断低下比0.5・自己認識の遅れ8年・引き際の窓5年。</summary>
        public static SenescenceParams Default => new SenescenceParams(45f, 0.01f, 0.6f, 10f, 0.5f, 8f, 5f);
    }

    /// <summary>
    /// 名将の衰えの純ロジック（能力の加齢曲線）。峠（全盛期の終わり）までは能力満額、越えると漸減し
    /// 下限で止まる＝上り（<see cref="GrowthRules"/>＝経験成長）とは別の、誰にも来る下り坂。
    /// 体力系（機動・攻撃）は早く・速く衰え、判断系（統率・運営）は遅く・緩やかに衰える＝老将は
    /// 前線指揮より大局で生きる。本人の自己評価は数年前の自分のまま＝衰えに気づかない
    /// （<see cref="SelfAwarenessGap"/>＝引き際を誤る入力）。「老兵は死なず、ただ衰えに気づかない」。
    /// 分担：<see cref="LifecycleRules"/>＝死亡（生死そのもの）／<see cref="RetirementRules"/>＝停年
    /// （制度上の引退）／<see cref="GrowthRules"/>＝経験成長（上り）／本クラス＝能力の加齢曲線（下り）。
    /// 倍率は <see cref="AdmiralData"/> の Effectivexxx 等の基準値に掛けて使う（実効値パターン・基準非破壊）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SenescenceRules
    {
        /// <summary>
        /// 加齢の能力倍率（minFactor..1）。峠までは1.0、超過分×低下率で漸減し下限で止まる。
        /// 提督能力の実効値に掛けて使う（基準フィールドは書き換えない）。
        /// </summary>
        public static float AgeFactor(float age, SenescenceParams p)
        {
            return Factor(age, p.peakAge, p.declineRate, p.minFactor);
        }

        public static float AgeFactor(float age) => AgeFactor(age, SenescenceParams.Default);

        /// <summary>
        /// 体力系と判断系の2系統の倍率。体力系（機動・攻撃）＝峠 peakAge から declineRate で衰える。
        /// 判断系（統率・運営）＝峠が judgmentPeakDelay 年遅く、低下も judgmentRateRatio 倍で緩やか。
        /// 常に 判断系 ≥ 体力系（老将は手は鈍っても眼は曇りにくい）。
        /// </summary>
        public static (float physical, float judgment) PhysicalVsJudgment(float age, SenescenceParams p)
        {
            float physical = Factor(age, p.peakAge, p.declineRate, p.minFactor);
            float judgment = Factor(age, p.peakAge + p.judgmentPeakDelay,
                                    p.declineRate * p.judgmentRateRatio, p.minFactor);
            return (physical, judgment);
        }

        public static (float physical, float judgment) PhysicalVsJudgment(float age)
            => PhysicalVsJudgment(age, SenescenceParams.Default);

        /// <summary>
        /// 自己評価と実態の乖離（0..1−minFactor）。本人は awarenessLag 年前の自分だと思っている＝
        /// 「自分の倍率」− 実際の倍率。峠の前は0（衰えていないので乖離なし）、下り坂の途中で最大
        /// （いちばん引き際を誤る時期）、下限に達した晩年は再び0へ収束（さすがに自覚する）。
        /// </summary>
        public static float SelfAwarenessGap(float age, SenescenceParams p)
        {
            float actual = AgeFactor(age, p);
            float perceived = AgeFactor(Mathf.Max(0f, age) - p.awarenessLag, p);
            return Mathf.Max(0f, perceived - actual);
        }

        public static float SelfAwarenessGap(float age) => SelfAwarenessGap(age, SenescenceParams.Default);

        /// <summary>峠を越えたか（age が peakAge を超過）。峠ちょうどはまだ全盛期＝false。</summary>
        public static bool IsPastPrime(float age, SenescenceParams p)
        {
            return Mathf.Max(0f, age) > p.peakAge;
        }

        public static bool IsPastPrime(float age) => IsPastPrime(age, SenescenceParams.Default);

        /// <summary>
        /// 名誉ある引き際の窓が開いているか＝峠の直後 exitWindowYears 年間（峠超過〜窓の終わりまで）。
        /// 衰えが浅いうちに退けば名声は満額のまま＝窓を逃すと「衰えた名将」として記憶される。
        /// </summary>
        public static bool GracefulExitWindow(float age, SenescenceParams p)
        {
            float a = Mathf.Max(0f, age);
            return a > p.peakAge && a <= p.peakAge + p.exitWindowYears;
        }

        public static bool GracefulExitWindow(float age) => GracefulExitWindow(age, SenescenceParams.Default);

        /// <summary>共通の下り坂カーブ：峠までは1.0、超過分×低下率で漸減し下限で止まる。</summary>
        private static float Factor(float age, float peak, float rate, float min)
        {
            float a = Mathf.Max(0f, age);
            if (a <= peak) return 1f;
            float decline = (a - peak) * rate;
            return Mathf.Max(min, 1f - decline);
        }
    }
}
