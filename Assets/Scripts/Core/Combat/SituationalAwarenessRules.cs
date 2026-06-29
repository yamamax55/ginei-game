using UnityEngine;

namespace Ginei
{
    /// <summary>状況認識（戦場の全体把握とOODA）の調整係数。</summary>
    public readonly struct SituationalAwarenessParams
    {
        /// <summary>収集（第一層）の偵察資産の寄与（センサー網に対し偵察がどれだけ収集を底上げするか・0..1）。</summary>
        public readonly float reconWeight;
        /// <summary>理解（第二層）が将来投射(第三層)へ伝わる伝達率（理解が浅いと予測も浅い・0..1）。</summary>
        public readonly float comprehensionTransfer;
        /// <summary>OODAループの決定遅延の効き（遅延が速さをどれだけ鈍らせるか・大きいほど遅延に敏感）。</summary>
        public readonly float latencyPenalty;
        /// <summary>認識優位における戦場の霧の重み（霧が先手をどれだけ削るか・0..1）。</summary>
        public readonly float fogWeight;
        /// <summary>誤認リスクにおける認知バイアスの効き（バイアスが霧を誤認へ増幅する度合い・0..1）。</summary>
        public readonly float biasWeight;

        public SituationalAwarenessParams(float reconWeight, float comprehensionTransfer, float latencyPenalty, float fogWeight, float biasWeight)
        {
            this.reconWeight = Mathf.Clamp01(reconWeight);
            this.comprehensionTransfer = Mathf.Clamp01(comprehensionTransfer);
            this.latencyPenalty = Mathf.Max(0f, latencyPenalty);
            this.fogWeight = Mathf.Clamp01(fogWeight);
            this.biasWeight = Mathf.Clamp01(biasWeight);
        }

        /// <summary>既定＝偵察寄与0.4・理解伝達0.8・遅延効き1.0・霧重み0.5・バイアス効き0.6。</summary>
        public static SituationalAwarenessParams Default => new SituationalAwarenessParams(0.4f, 0.8f, 1f, 0.5f, 0.6f);
    }

    /// <summary>
    /// 状況認識（Situational Awareness＝戦場の全体像の把握とOODAループ）の純ロジック。
    /// 指揮官がどれだけ戦場を正しく掴み、敵より速く回し、先手を取るかを扱う。
    /// 認識は三層＝収集(Perception＝センサーと偵察で情報を集める)→理解(Comprehension＝集めた情報を
    /// 分析して状況を意味づける)→投射(Projection＝理解から次に何が起きるかを予測する)で深まる。
    /// 戦場の霧(FogOfWar)は敵の隠蔽と収集不足からギャップを生み、認識のギャップは誤認(Misperception)の
    /// 温床になる。OODAループの速さ(OodaSpeed)は理解の明晰さと決定遅延で決まり、速さと敵意図の読みが
    /// 霧を上回ったぶんが認識優位(AwarenessAdvantage＝先手)になる。
    /// 分担：<see cref="BattlePerceptionRules"/> は観見二つの目（戦場視界の広さ・大局眼）、
    /// <see cref="ReconRules"/> は偵察の推定精度、<see cref="CommunicationsRules"/> は指揮の遅延を担う。
    /// ここは「三層の認識→OODA→認識優位・誤認リスク」という状況認識の積み上げだけを扱う。
    /// 乱数なし決定論・全入力クランプ・符号付き出力は-1..1。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class SituationalAwarenessRules
    {
        /// <summary>
        /// 収集（第一層・0..1）＝センサー網の被覆(0..1)に偵察資産(0..1)を reconWeight ぶん混ぜて情報を集める。
        /// reconWeight ぶんを偵察へ、残りをセンサーへ配分＝偵察を出すほど死角が埋まり収集が増える。
        /// </summary>
        public static float Perception(float sensorCoverage, float reconAssets, SituationalAwarenessParams p)
        {
            float sensor = Mathf.Clamp01(sensorCoverage);
            float recon = Mathf.Clamp01(reconAssets);
            float w = p.reconWeight;
            return Mathf.Clamp01(sensor * (1f - w) + recon * w);
        }

        public static float Perception(float sensorCoverage, float reconAssets)
            => Perception(sensorCoverage, reconAssets, SituationalAwarenessParams.Default);

        /// <summary>
        /// 理解（第二層・0..1）＝収集した情報(0..1)を分析力(0..1)で意味づける。両者の積＝集めても分析できねば
        /// 理解は深まらず、分析力があっても情報が無ければ意味づけられない（どちらも要る）。
        /// </summary>
        public static float Comprehension(float perception, float analysisCapacity, SituationalAwarenessParams p)
        {
            float perc = Mathf.Clamp01(perception);
            float analysis = Mathf.Clamp01(analysisCapacity);
            return Mathf.Clamp01(perc * analysis);
        }

        public static float Comprehension(float perception, float analysisCapacity)
            => Comprehension(perception, analysisCapacity, SituationalAwarenessParams.Default);

        /// <summary>
        /// 投射（第三層・0..1）＝理解(0..1)から予測モデル(0..1)で将来を読む（次に何が起きるか）。
        /// 理解は comprehensionTransfer ぶんだけ投射へ伝わる＝理解が浅いと予測の足場が崩れる。
        /// </summary>
        public static float Projection(float comprehension, float predictiveModeling, SituationalAwarenessParams p)
        {
            float comp = Mathf.Clamp01(comprehension);
            float model = Mathf.Clamp01(predictiveModeling);
            // 理解を伝達率ぶん減衰させた足場の上に予測モデルを掛ける＝理解なき予測は薄い。
            float basis = comp * p.comprehensionTransfer;
            return Mathf.Clamp01(basis * model);
        }

        public static float Projection(float comprehension, float predictiveModeling)
            => Projection(comprehension, predictiveModeling, SituationalAwarenessParams.Default);

        /// <summary>
        /// 戦場の霧によるギャップ(0..1)＝敵の隠蔽(0..1)×(1-収集)。収集が満ちれば霧は晴れ、
        /// 隠蔽が高くても収集で見破れる＝隠蔽と収集不足が掛かったところにだけ認識の穴があく。
        /// </summary>
        public static float FogOfWarGap(float enemyConcealment, float perception, SituationalAwarenessParams p)
        {
            float concealment = Mathf.Clamp01(enemyConcealment);
            float perc = Mathf.Clamp01(perception);
            return Mathf.Clamp01(concealment * (1f - perc));
        }

        public static float FogOfWarGap(float enemyConcealment, float perception)
            => FogOfWarGap(enemyConcealment, perception, SituationalAwarenessParams.Default);

        /// <summary>
        /// OODAループの速さ(0..1)＝理解(0..1)/(1+決定遅延×latencyPenalty)。理解が明晰なほど速く回り、
        /// 決定の遅延が増すほど鈍る＝同じ理解でも判断が遅ければループは遅い。
        /// </summary>
        public static float OodaSpeed(float comprehension, float decisionLatency, SituationalAwarenessParams p)
        {
            float comp = Mathf.Clamp01(comprehension);
            float latency = Mathf.Max(0f, decisionLatency);
            return Mathf.Clamp01(comp / (1f + latency * p.latencyPenalty));
        }

        public static float OodaSpeed(float comprehension, float decisionLatency)
            => OodaSpeed(comprehension, decisionLatency, SituationalAwarenessParams.Default);

        /// <summary>
        /// 敵の意図の読み(0..1)＝将来投射(0..1)×敵の行動パターン(0..1)。投射で先を読み、敵の行動が
        /// パターン化しているほど意図が透ける＝予測力と相手の癖の両方があって意図が読める。
        /// </summary>
        public static float EnemyIntentReading(float projection, float behaviorPatterns, SituationalAwarenessParams p)
        {
            float proj = Mathf.Clamp01(projection);
            float patterns = Mathf.Clamp01(behaviorPatterns);
            return Mathf.Clamp01(proj * patterns);
        }

        public static float EnemyIntentReading(float projection, float behaviorPatterns)
            => EnemyIntentReading(projection, behaviorPatterns, SituationalAwarenessParams.Default);

        /// <summary>
        /// 認識優位(-1..1)＝OODAの速さ×敵意図の読みで掴んだ先手から、戦場の霧を fogWeight ぶん引く。
        /// 速く回して敵意図を読めば先手(正)、霧が深ければ後手(負)＝認識が優位なら主導権を握る。
        /// </summary>
        public static float AwarenessAdvantage(float oodaSpeed, float enemyIntentReading, float fogOfWarGap, SituationalAwarenessParams p)
        {
            float ooda = Mathf.Clamp01(oodaSpeed);
            float intent = Mathf.Clamp01(enemyIntentReading);
            float fog = Mathf.Clamp01(fogOfWarGap);
            // 先手＝速さ×意図読み（0..1）、後手＝霧×重み（0..1）。差分を-1..1へ。
            float initiative = ooda * intent;
            return Mathf.Clamp(initiative - fog * p.fogWeight, -1f, 1f);
        }

        public static float AwarenessAdvantage(float oodaSpeed, float enemyIntentReading, float fogOfWarGap)
            => AwarenessAdvantage(oodaSpeed, enemyIntentReading, fogOfWarGap, SituationalAwarenessParams.Default);

        /// <summary>
        /// 誤認のリスク(0..1)＝戦場の霧(0..1)×認知バイアス(0..1)を biasWeight で効かせる。
        /// 霧が深いほど穴を埋めるのは推測になり、バイアスが強いほどその推測が歪む＝霧とバイアスの積が誤認。
        /// </summary>
        public static float MisperceptionRisk(float fogOfWarGap, float cognitiveBias, SituationalAwarenessParams p)
        {
            float fog = Mathf.Clamp01(fogOfWarGap);
            float bias = Mathf.Clamp01(cognitiveBias);
            return Mathf.Clamp01(fog * bias * p.biasWeight);
        }

        public static float MisperceptionRisk(float fogOfWarGap, float cognitiveBias)
            => MisperceptionRisk(fogOfWarGap, cognitiveBias, SituationalAwarenessParams.Default);
    }
}
