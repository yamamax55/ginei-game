using UnityEngine;

namespace Ginei
{
    /// <summary>基軸通貨特権の調整係数。</summary>
    public readonly struct ReserveCurrencyParams
    {
        /// <summary>基軸度の目標値に占める交易シェアの比重（0..1。残りは軍事覇権）。</summary>
        public readonly float tradeWeight;
        /// <summary>基軸度が目標へ上昇する速度（per 単位時間。通貨覇権は一夜で築けない＝小さい）。</summary>
        public readonly float riseRate;
        /// <summary>基軸度が目標へ下落する基礎速度（per 単位時間）。</summary>
        public readonly float fallRate;
        /// <summary>信認低下が下落速度を加速する倍率係数（信認0で下落は (1+これ) 倍＝信認だけは速く壊れる非対称）。</summary>
        public readonly float trustCrashScale;
        /// <summary>通貨発行益の係数（基軸度×世界交易量に掛かる）。</summary>
        public readonly float seigniorageRate;
        /// <summary>濫用の蓄積速度（per 単位時間）。</summary>
        public readonly float abuseAccumRate;
        /// <summary>濫用をやめたときの蓄積の減衰速度（per 単位時間。疑念はゆっくりとしか消えない＝小さい）。</summary>
        public readonly float abuseDecayRate;
        /// <summary>代替通貨が存在しないときの信認崩壊閾値（1超＝濫用が満タンでも崩れない）。</summary>
        public readonly float noAlternativeThreshold;
        /// <summary>代替通貨が完全に存在するときの信認崩壊閾値（0..1。代替が現れた瞬間にここまで下がる）。</summary>
        public readonly float fullAlternativeThreshold;
        /// <summary>崩壊時の返済ショックの倍率（基軸度に掛かる。享受した特権の逆流）。</summary>
        public readonly float collapseShockScale;

        public ReserveCurrencyParams(float tradeWeight, float riseRate, float fallRate, float trustCrashScale,
            float seigniorageRate, float abuseAccumRate, float abuseDecayRate,
            float noAlternativeThreshold, float fullAlternativeThreshold, float collapseShockScale)
        {
            this.tradeWeight = Mathf.Clamp01(tradeWeight);
            this.riseRate = Mathf.Max(0f, riseRate);
            this.fallRate = Mathf.Max(0f, fallRate);
            this.trustCrashScale = Mathf.Max(0f, trustCrashScale);
            this.seigniorageRate = Mathf.Max(0f, seigniorageRate);
            this.abuseAccumRate = Mathf.Max(0f, abuseAccumRate);
            this.abuseDecayRate = Mathf.Max(0f, abuseDecayRate);
            this.noAlternativeThreshold = Mathf.Max(0.01f, noAlternativeThreshold);
            this.fullAlternativeThreshold = Mathf.Clamp(fullAlternativeThreshold, 0.01f, this.noAlternativeThreshold);
            this.collapseShockScale = Mathf.Max(0f, collapseShockScale);
        }

        /// <summary>既定＝交易比重0.5・上昇0.05・下落0.1・信認加速4・発行益0.05・蓄積0.5・減衰0.05・無代替閾値1.2・全代替閾値0.4・崩壊倍率2。</summary>
        public static ReserveCurrencyParams Default =>
            new ReserveCurrencyParams(0.5f, 0.05f, 0.1f, 4f, 0.05f, 0.5f, 0.05f, 1.2f, 0.4f, 2f);
    }

    /// <summary>
    /// 基軸通貨特権の純ロジック（通貨覇権の力学）。自国通貨が世界の決済標準である国は、
    /// 赤字を自国通貨を刷って埋められる「法外な特権」を持つ＝世界からの無利子借金。
    /// だが返済日は信認が決める：代替通貨がない間は濫用も呑み込まれるが、代替が現れた瞬間に
    /// 崩壊閾値が下がり、享受した特権が基軸度に比例した巨大ショックとして一気に逆流する。
    /// <see cref="FiscalRules"/>（為替係数＝財政悪化→通貨安の国内側）とは別系統で、
    /// こちらは「誰の通貨が標準か」という覇権そのものを扱う。乱数なし・決定論。
    /// 純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class ReserveCurrencyRules
    {
        /// <summary>基軸度が高いほど濫用の蓄積を世界が呑み込む度合い（基軸度1で蓄積はこの分だけ減速）。</summary>
        public const float StatusToleranceDamping = 0.5f;

        /// <summary>
        /// 基軸度の1tick後の値（0..1）。目標＝(交易シェア×比重＋軍事覇権×(1−比重))×信認 へ
        /// ゆっくり収束する。上昇は <see cref="ReserveCurrencyParams.riseRate"/> で遅く
        /// （覇権は一夜で築けない）、下落は信認が低いほど
        /// (1＋(1−信認)×<see cref="ReserveCurrencyParams.trustCrashScale"/>) 倍に加速する
        /// （信認だけは速く壊れる非対称）。
        /// </summary>
        public static float ReserveStatusTick(float status, float tradeShare, float militaryHegemony, float trustInIssuer, float dt, ReserveCurrencyParams p)
        {
            float s = Mathf.Clamp01(status);
            float trade = Mathf.Clamp01(tradeShare);
            float mil = Mathf.Clamp01(militaryHegemony);
            float trust = Mathf.Clamp01(trustInIssuer);
            float target = Mathf.Clamp01((trade * p.tradeWeight + mil * (1f - p.tradeWeight)) * trust);
            float speed = target >= s ? p.riseRate : p.fallRate * (1f + (1f - trust) * p.trustCrashScale);
            return Mathf.Clamp01(s + (target - s) * Mathf.Clamp01(speed * Mathf.Max(0f, dt)));
        }

        public static float ReserveStatusTick(float status, float tradeShare, float militaryHegemony, float trustInIssuer, float dt)
            => ReserveStatusTick(status, tradeShare, militaryHegemony, trustInIssuer, dt, ReserveCurrencyParams.Default);

        /// <summary>
        /// 法外な特権（0..1）＝基軸度×赤字。赤字のうち自国通貨を刷って埋められる割合＝
        /// 世界が黙って引き受けてくれる無利子借金の量。基軸度0の国は赤字を借金か増税でしか埋められない。
        /// </summary>
        public static float ExorbitantPrivilege(float status, float deficit)
        {
            return Mathf.Clamp01(status) * Mathf.Clamp01(deficit);
        }

        /// <summary>
        /// 通貨発行益＝基軸度×世界交易量×<see cref="ReserveCurrencyParams.seigniorageRate"/>。
        /// 世界が自国通貨で決済するだけで懐に入る不労所得の側の式。
        /// </summary>
        public static float SeigniorageIncome(float status, float globalTradeVolume, ReserveCurrencyParams p)
        {
            return Mathf.Clamp01(status) * Mathf.Max(0f, globalTradeVolume) * p.seigniorageRate;
        }

        public static float SeigniorageIncome(float status, float globalTradeVolume)
            => SeigniorageIncome(status, globalTradeVolume, ReserveCurrencyParams.Default);

        /// <summary>
        /// 濫用の蓄積の1tick後の値（0..1）。濫用度に応じて蓄積し、基軸度が高いほど
        /// <see cref="StatusToleranceDamping"/> の分だけ減速する＝代替がないから世界はしばらく我慢する
        /// （ただしゼロにはならない＝返済日は必ず近づく）。濫用をやめれば
        /// <see cref="ReserveCurrencyParams.abuseDecayRate"/> でゆっくり減衰（疑念は急には消えない）。
        /// </summary>
        public static float DebasementToleratedTick(float accumulated, float abuse, float status, float dt, ReserveCurrencyParams p)
        {
            float acc = Mathf.Clamp01(accumulated);
            float a = Mathf.Clamp01(abuse);
            float s = Mathf.Clamp01(status);
            float gain = a * p.abuseAccumRate * (1f - StatusToleranceDamping * s);
            float decay = (1f - a) * p.abuseDecayRate;
            return Mathf.Clamp01(acc + (gain - decay) * Mathf.Max(0f, dt));
        }

        public static float DebasementToleratedTick(float accumulated, float abuse, float status, float dt)
            => DebasementToleratedTick(accumulated, abuse, status, dt, ReserveCurrencyParams.Default);

        /// <summary>
        /// 信認崩壊の閾値＝代替通貨の存在度で
        /// <see cref="ReserveCurrencyParams.noAlternativeThreshold"/>（1超＝濫用が満タンでも崩れない）から
        /// <see cref="ReserveCurrencyParams.fullAlternativeThreshold"/> へ線形に下がる。
        /// 代替が現れた瞬間に、それまで我慢されていた濫用が突然「崩壊条件」に変わる。
        /// </summary>
        public static float TrustCollapseThreshold(float alternativeAvailable, ReserveCurrencyParams p)
        {
            return Mathf.Lerp(p.noAlternativeThreshold, p.fullAlternativeThreshold, Mathf.Clamp01(alternativeAvailable));
        }

        public static float TrustCollapseThreshold(float alternativeAvailable)
            => TrustCollapseThreshold(alternativeAvailable, ReserveCurrencyParams.Default);

        /// <summary>
        /// 信認が崩壊したか＝濫用の蓄積（0..1）が <see cref="TrustCollapseThreshold(float,ReserveCurrencyParams)"/> 以上。
        /// 代替がなければ（閾値1超）どれだけ濫用しても崩れない＝崩壊の引き金は濫用でなく代替の出現。
        /// </summary>
        public static bool IsTrustCollapsed(float abuse, float alternativeAvailable, ReserveCurrencyParams p)
        {
            return Mathf.Clamp01(abuse) >= TrustCollapseThreshold(alternativeAvailable, p);
        }

        public static bool IsTrustCollapsed(float abuse, float alternativeAvailable)
            => IsTrustCollapsed(abuse, alternativeAvailable, ReserveCurrencyParams.Default);

        /// <summary>
        /// 崩壊時の返済ショック＝基軸度×<see cref="ReserveCurrencyParams.collapseShockScale"/>。
        /// 享受した特権（無利子借金）が一気に逆流する＝基軸度が高かった国ほど返済日は重い。
        /// 戻り値は被害係数として歳入・安定度等に掛けて使う想定（実効値パターン・基準非破壊）。
        /// </summary>
        public static float CollapseShock(float status, ReserveCurrencyParams p)
        {
            return Mathf.Clamp01(status) * p.collapseShockScale;
        }

        public static float CollapseShock(float status)
            => CollapseShock(status, ReserveCurrencyParams.Default);
    }
}
