using UnityEngine;

namespace Ginei
{
    /// <summary>追撃戦・殿軍の調整係数。</summary>
    public readonly struct PursuitParams
    {
        /// <summary>追撃で敗走側が受ける基礎損害割合（速度優位最大時）。</summary>
        public readonly float basePursuitLossRatio;
        /// <summary>殿軍が本隊への追撃損害を遮る最大割合（殿軍戦力比が十分なとき）。</summary>
        public readonly float rearguardProtection;
        /// <summary>殿軍自身が受ける損害割合（犠牲の値段＝本隊より重い）。</summary>
        public readonly float rearguardCasualtyRate;
        /// <summary>振り切り（クリーンブレイク）が成立する速度比（敗走側/追撃側）の閾値。</summary>
        public readonly float cleanBreakSpeedRatio;

        public PursuitParams(float basePursuitLossRatio, float rearguardProtection,
                             float rearguardCasualtyRate, float cleanBreakSpeedRatio)
        {
            this.basePursuitLossRatio = Mathf.Clamp01(basePursuitLossRatio);
            this.rearguardProtection = Mathf.Clamp01(rearguardProtection);
            this.rearguardCasualtyRate = Mathf.Clamp01(rearguardCasualtyRate);
            this.cleanBreakSpeedRatio = Mathf.Max(0f, cleanBreakSpeedRatio);
        }

        /// <summary>既定＝基礎損害30%・殿軍遮蔽80%・殿軍損害50%・振り切り速度比1.2。</summary>
        public static PursuitParams Default => new PursuitParams(0.3f, 0.8f, 0.5f, 1.2f);
    }

    /// <summary>
    /// 追撃戦・殿軍の純ロジック（会戦後の損害解決）。敗走は会戦そのものより多くの兵を失う＝追撃側の
    /// 速度優位が大きいほど敗走側の追加損害が嵩む。殿軍（しんがり）を残せば本隊への追撃は大きく遮れるが、
    /// 殿軍自身は重い犠牲を払う＝少数の犠牲で本隊を逃がす取引。速度で振り切れば（クリーンブレイク）
    /// 追撃損害は出ない。会戦中の撤退挙動（`FleetAI`＝Game層）・旗幟（<see cref="LoyaltyRules"/>）とは
    /// 別系統で、会戦後の損害計算のみを扱う。乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PursuitRules
    {
        /// <summary>振り切り成立か＝敗走側/追撃側の速度比が閾値以上（追いつかれない）。</summary>
        public static bool CleanBreak(float retreaterSpeed, float pursuerSpeed, PursuitParams p)
        {
            float pu = Mathf.Max(0f, pursuerSpeed);
            if (pu <= 0f) return true; // 追撃側が動けない＝必ず逃げ切る
            return Mathf.Max(0f, retreaterSpeed) / pu >= p.cleanBreakSpeedRatio;
        }

        public static bool CleanBreak(float retreaterSpeed, float pursuerSpeed)
            => CleanBreak(retreaterSpeed, pursuerSpeed, PursuitParams.Default);

        /// <summary>
        /// 追撃の苛烈度（0..1）＝追撃側の速度優位に比例。速度比（追撃/敗走）1以下は0、
        /// 2以上（倍速で噛みつく）で最大1。振り切り成立なら常に0。
        /// </summary>
        public static float PursuitSeverity(float retreaterSpeed, float pursuerSpeed, PursuitParams p)
        {
            if (CleanBreak(retreaterSpeed, pursuerSpeed, p)) return 0f;
            float re = Mathf.Max(0.0001f, retreaterSpeed);
            float ratio = Mathf.Max(0f, pursuerSpeed) / re;
            return Mathf.Clamp01(ratio - 1f);
        }

        public static float PursuitSeverity(float retreaterSpeed, float pursuerSpeed)
            => PursuitSeverity(retreaterSpeed, pursuerSpeed, PursuitParams.Default);

        /// <summary>
        /// 殿軍の遮蔽率（0..rearguardProtection）。殿軍戦力が本隊の2割で最大遮蔽に達する
        /// （それ以上厚くしても遮蔽は伸びない＝犠牲が増えるだけ）。殿軍なしは0。
        /// </summary>
        public static float RearguardScreen(float rearguardStrength, float mainBodyStrength, PursuitParams p)
        {
            float rg = Mathf.Max(0f, rearguardStrength);
            float main = Mathf.Max(0f, mainBodyStrength);
            if (rg <= 0f) return 0f;
            if (main <= 0f) return p.rearguardProtection; // 本隊なし＝殿軍が全てを引き受ける
            const float FullScreenRatio = 0.2f; // 本隊の2割で遮蔽最大
            float t = Mathf.Clamp01(rg / (main * FullScreenRatio));
            return p.rearguardProtection * t;
        }

        public static float RearguardScreen(float rearguardStrength, float mainBodyStrength)
            => RearguardScreen(rearguardStrength, mainBodyStrength, PursuitParams.Default);

        /// <summary>
        /// 本隊が追撃で失う戦力＝本隊×基礎損害×苛烈度×（1−殿軍遮蔽）。殿軍が立てば本隊の出血は細る。
        /// </summary>
        public static float MainBodyLosses(float mainBodyStrength, float severity, float rearguardScreen, PursuitParams p)
        {
            float unscreened = 1f - Mathf.Clamp01(rearguardScreen);
            return Mathf.Max(0f, mainBodyStrength) * p.basePursuitLossRatio * Mathf.Clamp01(severity) * unscreened;
        }

        public static float MainBodyLosses(float mainBodyStrength, float severity, float rearguardScreen)
            => MainBodyLosses(mainBodyStrength, severity, rearguardScreen, PursuitParams.Default);

        /// <summary>
        /// 殿軍が払う犠牲＝殿軍×殿軍損害率×苛烈度。本隊の損害率より重い（殿軍は殴られ役）。
        /// 苛烈度0（振り切り・追撃なし）なら犠牲も出ない。
        /// </summary>
        public static float RearguardLosses(float rearguardStrength, float severity, PursuitParams p)
        {
            return Mathf.Max(0f, rearguardStrength) * p.rearguardCasualtyRate * Mathf.Clamp01(severity);
        }

        public static float RearguardLosses(float rearguardStrength, float severity)
            => RearguardLosses(rearguardStrength, severity, PursuitParams.Default);
    }
}
