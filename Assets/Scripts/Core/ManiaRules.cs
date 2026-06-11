using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// マニア（集団的熱狂・信念）の感染状態を表す純データ struct（SIR モデル）。
    /// susceptible(感受性)→infected(感染＝熱狂中)→recovered(回復＝醒めた) の3区画で
    /// 母集団を 0..1 に正規化して持つ（合計≈1）。可変フィールド（Community/Polity 流儀）。
    /// </summary>
    public struct ManiaState
    {
        /// <summary>感受性人口 0..1（まだ感染していない＝熱狂に巻き込まれうる層）。</summary>
        public float susceptible;
        /// <summary>感染人口 0..1（いま熱狂している層＝マニアの強度そのもの）。</summary>
        public float infected;
        /// <summary>回復人口 0..1（一度感染して醒めた層＝再感染しにくい）。</summary>
        public float recovered;

        public ManiaState(float susceptible, float infected, float recovered)
        {
            this.susceptible = Mathf.Clamp01(susceptible);
            this.infected = Mathf.Clamp01(infected);
            this.recovered = Mathf.Clamp01(recovered);
        }

        /// <summary>3区画の合計（保存則の検算用＝理論上≈1）。</summary>
        public float Total => susceptible + infected + recovered;

        /// <summary>熱狂の強度＝現在の感染率（infected）。バブル/運動/熱狂の盛り上がり。</summary>
        public float Intensity => infected;

        /// <summary>未感染の感受性層だけが残る初期状態（infected を種火に与える）。</summary>
        public static ManiaState Seed(float seedInfected)
        {
            float i = Mathf.Clamp01(seedInfected);
            return new ManiaState(1f - i, i, 0f);
        }
    }

    /// <summary>
    /// マニア感染の調整値（純構造体・既定 .Default）。マジックナンバーを1か所へ集約する。
    /// </summary>
    public readonly struct ManiaParams
    {
        /// <summary>横断合成の経済の重み。</summary>
        public readonly float economicWeight;
        /// <summary>横断合成の政治の重み。</summary>
        public readonly float politicalWeight;
        /// <summary>横断合成の宗教の重み。</summary>
        public readonly float religiousWeight;
        /// <summary>既往感染が感受性を削る強さ（1＝醒めた者は二度と感染しない）。</summary>
        public readonly float immunityStrength;

        public ManiaParams(float economicWeight, float politicalWeight, float religiousWeight, float immunityStrength)
        {
            this.economicWeight = economicWeight;
            this.politicalWeight = politicalWeight;
            this.religiousWeight = religiousWeight;
            this.immunityStrength = immunityStrength;
        }

        /// <summary>既定の調整値（経済/政治/宗教を均等重み・既往感染は完全免疫寄り）。</summary>
        public static ManiaParams Default => new ManiaParams(
            economicWeight: 1f,
            politicalWeight: 1f,
            religiousWeight: 1f,
            immunityStrength: 1f);
    }

    /// <summary>
    /// マニア（集団的熱狂・信念）の感染モデル（MNIA-1 #1620・マッカイ『狂気とバブル』参考・純ロジック test-first）。
    /// 信念を感染症数理（SIR モデル）で解く＝感受性→感染→回復の伝播で、信念は感染症のように広がり、
    /// ピークを越えて飽和し、やがて醒める。運命を決めるのは基本再生産数 R0＝β/γ：R0&gt;1 なら流行し、
    /// R0&lt;1 なら消える。経済バブル・政治運動・宗教熱狂を横断する汎用エンジン。
    /// 役割分担：<see cref="ReligionRules"/>＝改宗圧力（信仰の均衡収束）、<see cref="PropagandaRules"/>＝世論操作
    /// （発信側の到達×信用）、<see cref="HopeRules"/>＝希望と末人。本クラスは伝播の数理（SIR）そのものを担う。
    /// 全入力クランプ・SIR の保存則を保つ・乱数なし決定論。Game層非依存＝Core 純ロジック。
    /// </summary>
    public static class ManiaRules
    {
        /// <summary>
        /// SIR を1ステップ進めた新 <see cref="ManiaState"/> を返す純関数（決定論・オイラー差分）。
        /// 新規感染 = contactRate(β)×S×I、回復 = recoveryRate(γ)×I。dS=-βSI, dI=βSI-γI, dR=γI。
        /// 各区画を 0..1 にクランプし、丸めで生じた誤差を recovered に吸わせて合計を保存する。
        /// </summary>
        public static ManiaState Tick(ManiaState s, float contactRate, float recoveryRate, float dt)
        {
            if (dt <= 0f) return s;
            float beta = Mathf.Max(0f, contactRate);
            float gamma = Mathf.Max(0f, recoveryRate);

            float total = s.Total;                              // 入力時点の母集団（保存対象）
            float susc = Mathf.Clamp01(s.susceptible);
            float inf = Mathf.Clamp01(s.infected);
            float rec = Mathf.Clamp01(s.recovered);

            float newInfections = beta * susc * inf * dt;       // βSI：感受性が感染へ
            float recoveries = gamma * inf * dt;                // γI：感染が回復へ
            // 各遷移は元の区画在庫を超えない（負在庫を作らない）。
            newInfections = Mathf.Min(newInfections, susc);
            recoveries = Mathf.Min(recoveries, inf);

            susc = Mathf.Clamp01(susc - newInfections);
            inf = Mathf.Clamp01(inf + newInfections - recoveries);
            rec = Mathf.Clamp01(rec + recoveries);

            // 保存則：合計を入力 total に合わせ、ズレは recovered で調整（負にしない）。
            float drift = total - (susc + inf + rec);
            rec = Mathf.Clamp01(rec + drift);

            return new ManiaState(susc, inf, rec);
        }

        /// <summary>
        /// 基本再生産数 R0＝β/γ（接触率÷回復率）。1人の感染者が生む平均感染者数。
        /// R0&gt;1 で流行（マニアが広がる）、R0&lt;1 で消える。γ=0 は無限大の代理として大きな値を返す。
        /// </summary>
        public static float BasicReproduction(float contactRate, float recoveryRate)
        {
            float beta = Mathf.Max(0f, contactRate);
            float gamma = Mathf.Max(0f, recoveryRate);
            if (gamma <= 0f) return beta > 0f ? 999f : 0f;      // 回復しない＝事実上の暴走
            return beta / gamma;
        }

        /// <summary>R0&gt;1 なら流行が起きる（マニアが広がる）。</summary>
        public static bool WillSpread(float r0)
        {
            return r0 > 1f;
        }

        /// <summary>
        /// 流行ピークの感染率の近似（log 不要の代数形）。R0≤1 は流行しないので 0。
        /// 感受性が R0 で削られるまで増え、残りが回復へ抜けるぶんを差し引いた飽和近似
        /// peak ≈ s0×(1 − 1/R0)^2（R0 が大きいほど高く、初期感受性 s0 に比例）。
        /// </summary>
        public static float PeakInfected(float r0, float s0)
        {
            if (r0 <= 1f) return 0f;
            float s = Mathf.Clamp01(s0);
            float gap = 1f - (1f / r0);                         // 集団免疫閾値ぶんの伸びしろ
            float peak = s * gap * gap;                         // log を使わない飽和近似
            return Mathf.Clamp01(peak);
        }

        /// <summary>
        /// 集団免疫閾値＝1 − 1/R0（感受性をここまで削れば流行は失速する）。
        /// R0≤1 は閾値0（そもそも流行しない）。バブルが醒める転換点の目安。
        /// </summary>
        public static float HerdImmunityThreshold(float r0)
        {
            if (r0 <= 1f) return 0f;
            return Mathf.Clamp01(1f - (1f / r0));
        }

        /// <summary>
        /// 感受性(0..1)：思想共鳴が高く既往感染が少ないほど感染しやすい。
        /// ideologyResonance が高いほど巻き込まれやすく、priorExposure（一度醒めた度合い）が
        /// 高いほど免疫で削られる（一度醒めた者は再感染しにくい）。p.immunityStrength で免疫の効きを調整。
        /// </summary>
        public static float Susceptibility(float ideologyResonance, float priorExposure, ManiaParams p)
        {
            float resonance = Mathf.Clamp01(ideologyResonance);
            float immune = Mathf.Clamp01(priorExposure) * Mathf.Clamp01(p.immunityStrength);
            return Mathf.Clamp01(resonance * (1f - immune));
        }

        /// <summary>
        /// 横断的な合成熱狂度(0..1)：経済バブル・政治運動・宗教熱狂を重み付き平均で1つの強度に束ねる。
        /// 「信念は領域を越えて感染する」＝どれかが燃えれば全体の熱が上がる汎用指標。重み和0なら0。
        /// </summary>
        public static float CrossDomainIntensity(float economic, float political, float religious, ManiaParams p)
        {
            float e = Mathf.Clamp01(economic);
            float pol = Mathf.Clamp01(political);
            float r = Mathf.Clamp01(religious);
            float we = Mathf.Max(0f, p.economicWeight);
            float wp = Mathf.Max(0f, p.politicalWeight);
            float wr = Mathf.Max(0f, p.religiousWeight);
            float wsum = we + wp + wr;
            if (wsum <= 0f) return 0f;
            return Mathf.Clamp01((e * we + pol * wp + r * wr) / wsum);
        }
    }
}
