using UnityEngine;

namespace Ginei
{
    /// <summary>直言参謀と佞臣の情報環境（諫言の質）の調整係数。</summary>
    public readonly struct AdvisorCandorParams
    {
        /// <summary>情報品質に効く直言（candor）の重み。</summary>
        public readonly float candorWeight;
        /// <summary>情報品質を蝕む追従（flattery）の重み。</summary>
        public readonly float flatteryWeight;
        /// <summary>政策の現実乖離の最大幅（情報品質が低いほど乖離が開く＝裸の王様）。</summary>
        public readonly float gapScale;
        /// <summary>直言の涵養速度（per dt・安全な環境が諫言を育てる）。</summary>
        public readonly float cultivationRate;
        /// <summary>イエスマン連鎖の進行速度（per dt・佞臣が皆を追従へ巻き込む）。</summary>
        public readonly float cascadeRate;
        /// <summary>エコーチェンバー判定の既定しきい値（情報品質がこれ未満で真実が届かない）。</summary>
        public readonly float echoThreshold;

        public AdvisorCandorParams(float candorWeight, float flatteryWeight, float gapScale,
                                   float cultivationRate, float cascadeRate, float echoThreshold)
        {
            this.candorWeight = Mathf.Max(0f, candorWeight);
            this.flatteryWeight = Mathf.Max(0f, flatteryWeight);
            this.gapScale = Mathf.Max(0f, gapScale);
            this.cultivationRate = Mathf.Max(0f, cultivationRate);
            this.cascadeRate = Mathf.Max(0f, cascadeRate);
            this.echoThreshold = Mathf.Clamp01(echoThreshold);
        }

        /// <summary>既定＝直言重み0.6/追従重み0.4/乖離幅0.8/涵養速度0.1/連鎖速度0.1/エコー閾0.3。</summary>
        public static AdvisorCandorParams Default => new AdvisorCandorParams(0.6f, 0.4f, 0.8f, 0.1f, 0.1f, 0.3f);
    }

    /// <summary>
    /// 直言参謀と佞臣（追従者）の純ロジック＝マキャヴェッリ『君主論』（#1141・MKV-3）。
    /// 「君主は真実を聞かねばならないが、媚びへつらう佞臣に囲まれると真実が届かなくなる＝賢明な君主は直言を許し求めるべき」。
    /// 政治情報の品質（直言が通るか佞臣に歪められるか）が、政策と現実の乖離度を決める＝直言を許す君主が誤らない。
    /// 君主の虚栄と異論への不寛容が佞臣の増える環境を作り、宮廷の取り巻きフィルターが真実の到達を阻む。
    /// <see cref="CounselRules"/>（献策の質と君主の採否＝個別の策の提案と採択）／
    /// <see cref="ImpartialObserverRules"/>（内なる公平な観察者＝自己批判）とは別系統＝こちらは
    /// 「諫言の質（情報環境）が政策の現実適合を決める＝直言vs追従」を扱う。佞臣の温床たる寵愛そのものの蓄積は
    /// <see cref="CourtFavorRules"/>（寵という通貨）の担当＝本クラスは寵が情報環境へ及ぼす歪みだけを扱う。
    /// 乱数なし決定論・全入力クランプ・基準値非破壊（実効値パターン）。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class AdvisorCandorRules
    {
        // ===== 情報の品質（直言が通るか佞臣に歪むか） =====

        /// <summary>
        /// 政治情報の品質（0..1）＝直言(candor 0..1)×candorWeight が品質を上げ、追従(flattery 0..1)×flatteryWeight が
        /// 品質を下げる（重み合計で正規化）。直言が高く追従が低いほど真実が君主に届く＝情報の品質が高い。
        /// </summary>
        public static float InformationQuality(float candor, float flattery, AdvisorCandorParams p)
        {
            float c = Mathf.Clamp01(candor);
            float f = Mathf.Clamp01(flattery);
            float weightSum = Mathf.Max(0.0001f, p.candorWeight + p.flatteryWeight);
            // 直言が押し上げ・追従が押し下げる綱引き。
            float quality = (c * p.candorWeight - f * p.flatteryWeight) / weightSum;
            // 直言の素地そのものも下支え（追従ゼロなら直言度がそのまま品質に近づく）。
            return Mathf.Clamp01(0.5f * c + 0.5f * Mathf.Clamp01(quality + 0.5f));
        }

        public static float InformationQuality(float candor, float flattery)
            => InformationQuality(candor, flattery, AdvisorCandorParams.Default);

        // ===== 佞臣の圧力（媚びが得をする環境） =====

        /// <summary>
        /// 佞臣の圧力（0..1）＝君主の虚栄(rulerVanity 0..1)が高く、異論への寛容(dissentTolerance 0..1)が低いほど
        /// 佞臣が増える（媚びが得をする環境）。虚栄×不寛容の積＝両方が揃ったとき佞臣が最も栄える。
        /// </summary>
        public static float FlatteryPressure(float rulerVanity, float dissentTolerance, AdvisorCandorParams p)
        {
            float vanity = Mathf.Clamp01(rulerVanity);
            float intolerance = 1f - Mathf.Clamp01(dissentTolerance);
            // 虚栄が媚びを呼び、不寛容が直言を黙らせる＝両者の積で媚びが得をする。
            return Mathf.Clamp01(vanity * intolerance);
        }

        public static float FlatteryPressure(float rulerVanity, float dissentTolerance)
            => FlatteryPressure(rulerVanity, dissentTolerance, AdvisorCandorParams.Default);

        // ===== 政策と現実の乖離（裸の王様） =====

        /// <summary>
        /// 政策と現実の乖離度（0..gapScale）＝情報品質(informationQuality 0..1)が低いほど政策が現実から外れる。
        /// 品質1なら乖離0・品質0なら乖離最大＝佞臣に囲まれた君主は裸の王様になる。
        /// </summary>
        public static float PolicyRealityGap(float informationQuality, AdvisorCandorParams p)
        {
            float q = Mathf.Clamp01(informationQuality);
            return (1f - q) * p.gapScale;
        }

        public static float PolicyRealityGap(float informationQuality)
            => PolicyRealityGap(informationQuality, AdvisorCandorParams.Default);

        // ===== 真実の到達（宮廷フィルターを越えて） =====

        /// <summary>
        /// 真実が君主に届く度合い（0..1）＝直言(candor 0..1)が宮廷のフィルター(courtFilter 0..1＝取り巻きの遮蔽)を
        /// 越えて届く。フィルター1なら何も届かず（0）、フィルター0なら直言度がそのまま届く＝取り巻きが真実を漉す。
        /// </summary>
        public static float TruthReachingRuler(float candor, float courtFilter)
        {
            float c = Mathf.Clamp01(candor);
            float pass = 1f - Mathf.Clamp01(courtFilter);
            return Mathf.Clamp01(c * pass);
        }

        // ===== 直言の涵養（直言を許す君主） =====

        /// <summary>
        /// 直言の涵養（0..1の時間発展）＝直言が罰されない安全な環境(advisorSafety 0..1)と異論への寛容
        /// (dissentTolerance 0..1)が諫言を育てる。両者が高いほど直言が増し、低いほど萎む（直言を許す君主）。
        /// dt で時間積分・0..1にクランプ。
        /// </summary>
        public static float CandorCultivation(float candor, float dissentTolerance, float advisorSafety, float dt, AdvisorCandorParams p)
        {
            float c = Mathf.Clamp01(candor);
            float tolerance = Mathf.Clamp01(dissentTolerance);
            float safety = Mathf.Clamp01(advisorSafety);
            // 安全と寛容の積を目標とし、そこへ向けて育つ／萎む（安全でなければ直言は黙る）。
            float target = tolerance * safety;
            return Mathf.Clamp01(Mathf.MoveTowards(c, target, p.cultivationRate * Mathf.Max(0f, dt)));
        }

        public static float CandorCultivation(float candor, float dissentTolerance, float advisorSafety, float dt)
            => CandorCultivation(candor, dissentTolerance, advisorSafety, dt, AdvisorCandorParams.Default);

        // ===== イエスマンの連鎖（真実が消える） =====

        /// <summary>
        /// イエスマンの連鎖（0..1の時間発展）＝佞臣(flattery 0..1)が増えると同調圧力(conformity 0..1)で皆が追従し
        /// 真実が消える（追従が追従を呼ぶ）。佞臣度×同調度を目標に追従が広がる＝イエスマンの連鎖。
        /// dt で時間積分・0..1にクランプ。
        /// </summary>
        public static float YesManCascade(float flattery, float conformity, float dt, AdvisorCandorParams p)
        {
            float f = Mathf.Clamp01(flattery);
            float conf = Mathf.Clamp01(conformity);
            // 佞臣度×同調度が追従の到達点＝佞臣が多く同調的なほど全員がイエスマンへ。
            float target = f * conf;
            return Mathf.Clamp01(Mathf.MoveTowards(f, target, p.cascadeRate * Mathf.Max(0f, dt)));
        }

        public static float YesManCascade(float flattery, float conformity, float dt)
            => YesManCascade(flattery, conformity, dt, AdvisorCandorParams.Default);

        // ===== 意思決定の質 =====

        /// <summary>
        /// 意思決定の質（0..1）＝良い情報(informationQuality 0..1)×君主の判断力(rulerJudgment 0..1)。
        /// どちらか欠ければ良い決定にならない＝真実が届いても暗君なら誤り、賢君も誤情報では誤る。
        /// </summary>
        public static float DecisionQuality(float informationQuality, float rulerJudgment)
        {
            float q = Mathf.Clamp01(informationQuality);
            float judgment = Mathf.Clamp01(rulerJudgment);
            return Mathf.Clamp01(q * judgment);
        }

        // ===== エコーチェンバー判定 =====

        /// <summary>
        /// エコーチェンバー（佞臣に囲まれ真実が届かない反響室）の判定＝情報品質(informationQuality 0..1)が
        /// しきい値(threshold 0..1)未満なら true。佞臣だけが囁き真実が消えた状態。
        /// </summary>
        public static bool IsEchoChamber(float informationQuality, float threshold)
            => Mathf.Clamp01(informationQuality) < Mathf.Clamp01(threshold);

        /// <summary>エコーチェンバー判定（既定しきい値 echoThreshold を使用）。</summary>
        public static bool IsEchoChamber(float informationQuality, AdvisorCandorParams p)
            => IsEchoChamber(informationQuality, p.echoThreshold);

        public static bool IsEchoChamber(float informationQuality)
            => IsEchoChamber(informationQuality, AdvisorCandorParams.Default);
    }
}
