namespace Ginei
{
    /// <summary>軍団スロット間隔の算出結果（指定最小間隔と実間隔を区別して持つ）。</summary>
    public readonly struct CorpsSpacingResult
    {
        /// <summary>指定された最小間隔（算出に使った値）。</summary>
        public readonly float minSpacing;
        /// <summary>軍団内の最大占有半径。</summary>
        public readonly float maxFootprint;
        /// <summary>占有半径から決まる下限（2×最大占有半径＋余白）。</summary>
        public readonly float footprintFloor;
        /// <summary>実際にスロットへ使った間隔。</summary>
        public readonly float actualSpacing;

        public CorpsSpacingResult(float minSpacing, float maxFootprint, float footprintFloor, float actualSpacing)
        {
            this.minSpacing = minSpacing;
            this.maxFootprint = maxFootprint;
            this.footprintFloor = footprintFloor;
            this.actualSpacing = actualSpacing;
        }

        /// <summary>占有半径の下限が勝った（最小間隔をこれ以下へ調整しても実間隔は変わらない）。</summary>
        public bool FootprintBound => footprintFloor > minSpacing;

        /// <summary>ログ用の要約。</summary>
        public string Describe()
        {
            return "指定最小間隔=" + minSpacing.ToString("0.####") +
                   " 占有下限=" + footprintFloor.ToString("0.####") + "（最大占有半径 " + maxFootprint.ToString("0.####") + "）" +
                   " 実間隔=" + actualSpacing.ToString("0.####") +
                   (FootprintBound
                       ? "［★占有半径の下限が勝つ＝最小間隔をこれ以下へ調整しても実間隔は変わらない］"
                       : "［最小間隔が効いている］");
        }
    }
}
