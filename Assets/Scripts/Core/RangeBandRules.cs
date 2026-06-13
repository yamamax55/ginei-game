namespace Ginei
{
    /// <summary>武器の射程帯区分（近・中・遠）。</summary>
    public enum RangeBand
    {
        近, // Close
        中, // Medium
        遠, // Long
    }

    /// <summary>
    /// 射程帯とキーティング（間合い管理）の純ロジック（#2254）。
    /// 基準値（weaponRange）を書き換えず、理想間合いと接近/後退の方向を返す実効値パターン。
    /// </summary>
    public static class RangeBandRules
    {
        // ── 射程帯ごとの距離係数（weaponRange に乗算）──
        public const float FarFraction    = 0.9f;
        public const float MediumFraction = 0.6f;
        public const float CloseFraction  = 0.35f;

        /// <summary>
        /// 射程帯と実際の射程距離から理想交戦距離を返す。
        /// 遠=range*0.9 / 中=range*0.6 / 近=range*0.35。
        /// </summary>
        public static float IdealRange(RangeBand band, float weaponRange)
        {
            switch (band)
            {
                case RangeBand.遠: return weaponRange * FarFraction;
                case RangeBand.中: return weaponRange * MediumFraction;
                case RangeBand.近: return weaponRange * CloseFraction;
                default:          return weaponRange * MediumFraction;
            }
        }

        /// <summary>
        /// 現在距離・理想距離・デッドゾーン（許容誤差）から接近/後退/保持を返す。
        /// +1=接近（近づく）、-1=後退（離れる）、0=保持（deadzone 内）。
        /// </summary>
        public static int ApproachOrWithdraw(float currentDist, float idealRange, float deadzone)
        {
            if (currentDist > idealRange + deadzone) return +1; // 遠すぎる→接近
            if (currentDist < idealRange - deadzone) return -1; // 近すぎる→後退
            return 0;                                           // 許容範囲内→保持
        }
    }
}
