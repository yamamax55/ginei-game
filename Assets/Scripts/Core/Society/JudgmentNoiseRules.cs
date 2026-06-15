using UnityEngine;

namespace Ginei
{
    /// <summary>判断ノイズの調整係数（カーネマン『NOISE』型・KAHN-2 #1834）。</summary>
    public readonly struct JudgmentNoiseParams
    {
        /// <summary>水準ノイズのスケール（判断者の楽観/悲観1ぶれあたりの判断水準のずれ幅）。</summary>
        public readonly float levelNoiseScale;
        /// <summary>機会ノイズの振幅（同一判断者でも時点で散る一過性ぶれの最大幅・±）。</summary>
        public readonly float occasionNoiseAmplitude;
        /// <summary>パターンノイズのスケール（判断者×文脈の相互作用ぶれの幅）。</summary>
        public readonly float patternNoiseScale;
        /// <summary>判断の構造化1のときのノイズ抑制の最大幅（チェックリスト等で散らばりがどれだけ縮むか）。</summary>
        public readonly float decisionHygieneScale;

        public JudgmentNoiseParams(float levelNoiseScale, float occasionNoiseAmplitude,
                                   float patternNoiseScale, float decisionHygieneScale)
        {
            this.levelNoiseScale = Mathf.Max(0f, levelNoiseScale);
            this.occasionNoiseAmplitude = Mathf.Max(0f, occasionNoiseAmplitude);
            this.patternNoiseScale = Mathf.Max(0f, patternNoiseScale);
            this.decisionHygieneScale = Mathf.Clamp01(decisionHygieneScale);
        }

        /// <summary>既定＝水準0.2・機会0.15・パターン0.1・構造化抑制0.8。</summary>
        public static JudgmentNoiseParams Default => new JudgmentNoiseParams(0.2f, 0.15f, 0.1f, 0.8f);
    }

    /// <summary>
    /// 判断ノイズの純ロジック（KAHN-2 #1834・カーネマン『NOISE』参考）。判断ノイズとは、バイアス（系統的な偏り）と
    /// **直交する**判断の「ばらつき」＝同じ条件でも判断者や時点によって結論が散らばる現象。ノイズはバイアスと
    /// 独立に判断の質を下げ、総誤差は両者の直交合成 sqrt(bias²+noise²) になる。ノイズは ①水準ノイズ（判断者ごとの
    /// 楽観/悲観の水準差）②機会ノイズ（同一判断者でも時点で散る一過性ぶれ）③パターンノイズ（判断者×文脈の相互作用）
    /// に分解でき、複数判断の平均（集合知）でノイズは 1/√n に縮み、判断の構造化（decision hygiene）で抑えられる。
    /// 偵察の推定誤差（<see cref="ReconRules"/>＝情報そのものの不確実性）とは別＝同一情報でも判断がばらつくのがノイズ。
    /// 系統的バイアス（OverconfidenceBiasRules・同EPIC KAHN）とは別＝ノイズはバイアスと直交する。
    /// 乱数は roll 引数で決定論。盤面非依存のplain引数。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class JudgmentNoiseRules
    {
        /// <summary>
        /// 水準ノイズ込みの判断（判断者ごとの水準のばらつき）。判断者の楽観/悲観 judgePessimism は [-1,1] に
        /// クランプし（負＝楽観で水準が上がり、正＝悲観で下がる）、baseJudgment に levelNoiseScale 倍のずれを乗せる。
        /// 同じ案件でも、悲観的な判断者は低く、楽観的な判断者は高く判断する＝判断者間の水準差。
        /// </summary>
        public static float LevelNoise(float baseJudgment, float judgePessimism, JudgmentNoiseParams p)
        {
            float pess = Mathf.Clamp(judgePessimism, -1f, 1f);
            return baseJudgment - pess * p.levelNoiseScale;
        }

        public static float LevelNoise(float baseJudgment, float judgePessimism)
            => LevelNoise(baseJudgment, judgePessimism, JudgmentNoiseParams.Default);

        /// <summary>
        /// 機会ノイズ（平均0・±occasionNoiseAmplitude）。同一判断者でも時点（気分・空腹・順番など）で
        /// 判断が散る一過性ぶれ。roll∈[0,1) を [-1,1] に写して決定論的に散らす（roll=0.5で0＝ぶれなし）。
        /// </summary>
        public static float OccasionNoise(float roll, JudgmentNoiseParams p)
        {
            float r = Mathf.Clamp01(roll);
            float signed = r * 2f - 1f;  // [-1,1]
            return signed * p.occasionNoiseAmplitude;
        }

        public static float OccasionNoise(float roll)
            => OccasionNoise(roll, JudgmentNoiseParams.Default);

        /// <summary>
        /// パターンノイズ（判断者×文脈の相互作用・±patternNoiseScale）。判断者 id と文脈 id（ともに0..1）を
        /// 掛け合わせ、両者を [-1,1] に写した積でぶれを出す＝特定の判断者が特定の文脈にだけ強く反応する個性。
        /// 水準ノイズ（一律の水準差）とは別＝文脈依存の予測しにくいぶれ。
        /// </summary>
        public static float PatternNoise(float judgeId01, float contextId01, JudgmentNoiseParams p)
        {
            float j = Mathf.Clamp01(judgeId01) * 2f - 1f;
            float c = Mathf.Clamp01(contextId01) * 2f - 1f;
            return j * c * p.patternNoiseScale;
        }

        public static float PatternNoise(float judgeId01, float contextId01)
            => PatternNoise(judgeId01, contextId01, JudgmentNoiseParams.Default);

        /// <summary>
        /// 総誤差＝sqrt(bias²+noise²)。バイアス（系統的な偏り）とノイズ（ばらつき）は直交するので、
        /// ピタゴラス的に合成される＝バイアスをゼロにしてもノイズが残れば誤差は残る（その逆も）。
        /// 入力は絶対値で扱う（誤差成分の大きさ）。
        /// </summary>
        public static float TotalError(float bias, float noise)
        {
            float b = Mathf.Abs(bias);
            float n = Mathf.Abs(noise);
            return Mathf.Sqrt(b * b + n * n);
        }

        /// <summary>
        /// ノイズ込みの観測判断＝真値 trueValue にバイアス bias を加え、機会ノイズ（振幅 noiseAmplitude・平均0）を
        /// roll で決定論的に乗せる。バイアスは系統的に一方へずらし、ノイズは時点ごとに散らす＝両者は別物。
        /// </summary>
        public static float NoisyJudgment(float trueValue, float bias, float noiseAmplitude, float roll)
        {
            float amp = Mathf.Max(0f, noiseAmplitude);
            float signed = Mathf.Clamp01(roll) * 2f - 1f;
            return trueValue + bias + signed * amp;
        }

        /// <summary>
        /// 集約によるノイズ低減＝singleNoise / √judgeCount。独立な複数判断の平均はノイズが 1/√n に縮む（集合知）。
        /// バイアスは平均しても消えない（系統的だから）が、ノイズは打ち消し合う＝群衆の知恵がノイズだけを削る。
        /// judgeCount は1未満を1へ丸める（最低1人）。
        /// </summary>
        public static float NoiseReductionByAggregation(float singleNoise, int judgeCount)
        {
            int n = judgeCount < 1 ? 1 : judgeCount;
            return Mathf.Abs(singleNoise) / Mathf.Sqrt(n);
        }

        /// <summary>
        /// 判断の構造化（decision hygiene）後のノイズ＝noise×(1−structureLevel×decisionHygieneScale)。
        /// チェックリスト・独立評価・構造化面接などで判断を構造化するほど（structureLevel→1）ノイズが縮む。
        /// バイアスには効かない＝衛生はノイズ対策。structureLevel は0..1にクランプ。
        /// </summary>
        public static float DecisionHygieneEffect(float noise, float structureLevel, JudgmentNoiseParams p)
        {
            float s = Mathf.Clamp01(structureLevel);
            return Mathf.Abs(noise) * (1f - s * p.decisionHygieneScale);
        }

        public static float DecisionHygieneEffect(float noise, float structureLevel)
            => DecisionHygieneEffect(noise, structureLevel, JudgmentNoiseParams.Default);

        /// <summary>
        /// ノイズ対バイアス比＝|noise| / |bias|。1より大きければノイズ支配、小さければバイアス支配。
        /// どちらの誤差源を先に潰すべきかの指標（NOISE の主張＝ノイズはバイアスに劣らず大きいことが多い）。
        /// バイアスが極小（ゼロ近傍）のときは大きな有限値（既定の上限）を返して0除算を避ける。
        /// </summary>
        public static float NoiseToBiasRatio(float noise, float bias)
        {
            float n = Mathf.Abs(noise);
            float b = Mathf.Abs(bias);
            if (b < 1e-6f)
            {
                return n < 1e-6f ? 0f : 9999f;  // バイアスほぼ0＝ノイズが完全支配
            }
            return n / b;
        }

        /// <summary>
        /// ノイズがバイアスを上回るか＝|noise| が |bias|×threshold を超えるか。threshold=1 で「ノイズ＞バイアス」、
        /// より厳しく見たいときは threshold を上げる。ノイズ対策を優先すべき状況の判定。
        /// </summary>
        public static bool IsNoiseDominant(float noise, float bias, float threshold)
        {
            float n = Mathf.Abs(noise);
            float b = Mathf.Abs(bias);
            float t = Mathf.Max(0f, threshold);
            return n > b * t;
        }
    }
}
