namespace Ginei
{
    /// <summary>
    /// 触媒（仲介・ブローカー特化）の調整係数（CEND-3 #2361）。フェザーン #160 類型＝他者の変容を
    /// 加速する「触媒」に特化した勢力が、その専門化ゆえに自身は超越（CEND-1）できなくなる構造的非対称を表す。
    /// <see cref="catalystStrength"/> が同盟勢力の成長係数への上乗せ、<see cref="penaltyStrength"/> が
    /// 自己進展への抑制、<see cref="exclusionThreshold"/> が「これ以上の触媒特化は変容を阻む」境界。
    /// 純データ（非 MonoBehaviour・test-first）。解決は <see cref="CatalystRules"/>（static）。
    /// </summary>
    public readonly struct CatalystParams
    {
        /// <summary>触媒ボーナスの強さ（同盟勢力の成長係数への上乗せ率の上限・既定0.3）。</summary>
        public readonly float catalystStrength;

        /// <summary>自己進展ペナルティの強さ（触媒特化が自勢力の進展を削る率の上限・既定0.4）。</summary>
        public readonly float penaltyStrength;

        /// <summary>超越除外の閾値（触媒特化比率がこれ以上なら自身は変容できない・既定0.7）。</summary>
        public readonly float exclusionThreshold;

        public CatalystParams(float catalystStrength, float penaltyStrength, float exclusionThreshold)
        {
            this.catalystStrength = catalystStrength;
            this.penaltyStrength = penaltyStrength;
            this.exclusionThreshold = exclusionThreshold;
        }

        /// <summary>既定＝触媒0.3／ペナルティ0.4／除外閾値0.7（仲介の最上位は「変容しないこと」で成る）。</summary>
        public static CatalystParams Default => new CatalystParams(0.3f, 0.4f, 0.7f);
    }
}
