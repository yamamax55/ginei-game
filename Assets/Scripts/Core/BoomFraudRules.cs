using UnityEngine;

namespace Ginei
{
    /// <summary>ブーム詐欺の調整係数（景気循環に同期した詐欺の生起・潜伏・発覚）。</summary>
    public readonly struct BoomFraudParams
    {
        /// <summary>詐欺出現の基礎係数（好況最強×監督ゼロのときの出現確率）。</summary>
        public readonly float emergenceBase;
        /// <summary>好況中の隠蔽の最大倍率上乗せ幅（上げ相場が不正をこの倍ぶん覆い隠す）。</summary>
        public readonly float concealmentScale;
        /// <summary>発覚の基礎係数（収縮最深×詐欺蓄積最大のときの発覚確率）。</summary>
        public readonly float exposureBase;
        /// <summary>発覚した詐欺が信頼を崩す係数（露見詐欺量1あたりの信頼喪失幅）。</summary>
        public readonly float trustCollapseScale;
        /// <summary>詐欺ストックの蓄積速度（per dt・出現率1のとき）。</summary>
        public readonly float accumulationRate;
        /// <summary>詐欺ストックが発覚で減る速度（per dt・発覚率1のとき）。</summary>
        public readonly float exposureDrainRate;
        /// <summary>連鎖（システミック）リスク係数（蓄積詐欺×収縮の積1のときの連鎖リスク）。</summary>
        public readonly float systemicScale;

        public BoomFraudParams(float emergenceBase, float concealmentScale, float exposureBase,
                               float trustCollapseScale, float accumulationRate,
                               float exposureDrainRate, float systemicScale)
        {
            this.emergenceBase = Mathf.Clamp01(emergenceBase);
            this.concealmentScale = Mathf.Max(0f, concealmentScale);
            this.exposureBase = Mathf.Clamp01(exposureBase);
            this.trustCollapseScale = Mathf.Max(0f, trustCollapseScale);
            this.accumulationRate = Mathf.Max(0f, accumulationRate);
            this.exposureDrainRate = Mathf.Max(0f, exposureDrainRate);
            this.systemicScale = Mathf.Max(0f, systemicScale);
        }

        /// <summary>既定＝出現0.6・隠蔽上乗せ2.0・発覚0.8・信頼崩壊1.0・蓄積0.1・発覚減0.2・連鎖1.0。</summary>
        public static BoomFraudParams Default
            => new BoomFraudParams(0.6f, 2f, 0.8f, 1f, 0.1f, 0.2f, 1f);
    }

    /// <summary>
    /// ブーム詐欺と信頼崩壊の純ロジック（KNDB-5 #1621・キンドルバーガー『熱狂、恐慌、崩壊』参考）。
    /// 「潮が引くと誰が裸で泳いでいたか分かる」＝好況（boomIntensity）は詐欺を育て隠し、不況（contractionSeverity）が暴く。
    /// 熱狂期は審査が甘くなり監督（oversight）が緩むほど詐欺が湧き（<see cref="FraudEmergenceChance"/>）、
    /// 上げ相場は不正を覆い隠す（<see cref="ConcealmentDuringBoom"/>）。収縮が深く詐欺蓄積が多いほど発覚し
    /// （<see cref="ExposureChance"/>）、発覚した詐欺量で信頼が崩れ（<see cref="TrustCollapse"/>）、蓄積詐欺×収縮が連鎖を呼ぶ
    /// （<see cref="SystemicRisk"/>）。詐欺ストックは好況で積もり発覚で減る（<see cref="AccumulatedFraudTick"/>）。
    /// <see cref="EspionageRules"/>（諜報＝意図的な情報窃取）/<see cref="ScandalRules"/>（醜聞一般＝個人の失脚）とは別＝
    /// 景気循環に同期した詐欺の生起・潜伏・発覚を扱う。危機の伝播そのものは CrisisCycleRules（危機サイクル・同EPIC）が扱う。
    /// EventEngine へ接続想定だが本Issueは純ロジック部のみ。乱数は外から roll∈[0,1) を渡す決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class BoomFraudRules
    {
        /// <summary>
        /// 詐欺の出現確率（0..1）＝基礎係数×好況強度(0..1)×（1−監督(0..1)）。
        /// 好況が強く監督が緩いほど詐欺が湧く＝熱狂は審査を甘くする。
        /// 監督が完璧（1）なら出現せず、好況がなければ（0）湧く土壌がない。
        /// </summary>
        public static float FraudEmergenceChance(float boomIntensity, float oversight, BoomFraudParams p)
        {
            return Mathf.Clamp01(p.emergenceBase * Mathf.Clamp01(boomIntensity) * (1f - Mathf.Clamp01(oversight)));
        }

        public static float FraudEmergenceChance(float boomIntensity, float oversight)
            => FraudEmergenceChance(boomIntensity, oversight, BoomFraudParams.Default);

        /// <summary>詐欺出現判定（決定論）＝roll∈[0,1) が出現確率未満なら新たな詐欺が生起する。</summary>
        public static bool FraudEmerges(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 好況中の隠蔽倍率（≧1）＝1＋隠蔽上乗せ幅×好況強度(0..1)。
        /// 上げ相場ほど不正を覆い隠す＝この倍率ぶん発覚しにくくなる（発覚率を割って使う）。
        /// 好況0で1.0（隠蔽なし＝素通し）、好況1で既定なら3.0倍隠せる。
        /// </summary>
        public static float ConcealmentDuringBoom(float boomIntensity, BoomFraudParams p)
        {
            return 1f + p.concealmentScale * Mathf.Clamp01(boomIntensity);
        }

        public static float ConcealmentDuringBoom(float boomIntensity)
            => ConcealmentDuringBoom(boomIntensity, BoomFraudParams.Default);

        /// <summary>
        /// 詐欺の発覚確率（0..1）＝基礎係数×収縮の深さ(0..1)×詐欺蓄積(0..1)。
        /// 収縮が深く詐欺が積もっているほど露わになる＝潮が引くと裸が見える。
        /// 好況中（収縮0）は誰も暴かれず、蓄積がなければ（0）暴くものがない。
        /// </summary>
        public static float ExposureChance(float contractionSeverity, float fraudStock, BoomFraudParams p)
        {
            return Mathf.Clamp01(p.exposureBase * Mathf.Clamp01(contractionSeverity) * Mathf.Clamp01(fraudStock));
        }

        public static float ExposureChance(float contractionSeverity, float fraudStock)
            => ExposureChance(contractionSeverity, fraudStock, BoomFraudParams.Default);

        /// <summary>詐欺発覚判定（決定論）＝roll∈[0,1) が発覚確率未満なら蓄積詐欺が暴かれる。</summary>
        public static bool FraudExposed(float chance, float roll)
        {
            return Mathf.Clamp01(roll) < Mathf.Clamp01(chance);
        }

        /// <summary>
        /// 発覚後の信頼水準（0..1）＝従前の信頼(0..1)−露見した詐欺量(0..1)×信頼崩壊係数。
        /// 発覚した詐欺の量だけ信頼が崩れる＝大きな詐欺の露見ほど信頼を深く損なう。下限0。
        /// </summary>
        public static float TrustCollapse(float exposedFraud, float priorTrust, BoomFraudParams p)
        {
            float loss = Mathf.Clamp01(exposedFraud) * p.trustCollapseScale;
            return Mathf.Clamp01(Mathf.Clamp01(priorTrust) - loss);
        }

        public static float TrustCollapse(float exposedFraud, float priorTrust)
            => TrustCollapse(exposedFraud, priorTrust, BoomFraudParams.Default);

        /// <summary>
        /// 詐欺ストックの1tick後の値（0..1）＝現ストック＋蓄積（出現率に比例）−発覚減（発覚率に比例し既存ストックを削る）。
        /// 好況で出現が続けば積もり、収縮で発覚が進めば吐き出される＝好況が育て不況が暴くストックの動学。
        /// </summary>
        public static float AccumulatedFraudTick(float stock, float emergenceRate, float exposureRate,
                                                 float dt, BoomFraudParams p)
        {
            float s = Mathf.Clamp01(stock);
            float d = Mathf.Max(0f, dt);
            float grow = p.accumulationRate * Mathf.Clamp01(emergenceRate) * d;
            float drain = p.exposureDrainRate * Mathf.Clamp01(exposureRate) * s * d;
            return Mathf.Clamp01(s + grow - drain);
        }

        public static float AccumulatedFraudTick(float stock, float emergenceRate, float exposureRate, float dt)
            => AccumulatedFraudTick(stock, emergenceRate, exposureRate, dt, BoomFraudParams.Default);

        /// <summary>
        /// 連鎖（システミック）リスク（0..1）＝連鎖係数×詐欺蓄積(0..1)×収縮の深さ(0..1)。
        /// 蓄積した詐欺が深い収縮で一斉に暴かれるほど信頼崩壊が連鎖する＝隠れた裸が多いほど引き潮が致命的。
        /// </summary>
        public static float SystemicRisk(float fraudStock, float contractionSeverity, BoomFraudParams p)
        {
            return Mathf.Clamp01(p.systemicScale * Mathf.Clamp01(fraudStock) * Mathf.Clamp01(contractionSeverity));
        }

        public static float SystemicRisk(float fraudStock, float contractionSeverity)
            => SystemicRisk(fraudStock, contractionSeverity, BoomFraudParams.Default);
    }
}
