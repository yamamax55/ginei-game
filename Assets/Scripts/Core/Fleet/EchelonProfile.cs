namespace Ginei
{
    /// <summary>
    /// 梯団の標準プロファイル（ORBAT-2 #1718）＝「指揮官階級 tier」と「規模レンジ（隻）」を結ぶ値。
    /// 出所は <see cref="CommandCapacityRules.ProfileFor"/>（梯団↔階級↔規模の一表）。read-only な値型。
    /// </summary>
    public readonly struct EchelonProfile
    {
        public readonly EchelonType echelon;
        public readonly int commanderTier; // 標準指揮官階級 tier（必要tierの下限・#14）
        public readonly int minShips;      // 規模下限（隻）
        public readonly int maxShips;      // 規模上限（隻・上限なしは int.MaxValue）

        public EchelonProfile(EchelonType echelon, int commanderTier, int minShips, int maxShips)
        {
            this.echelon = echelon;
            this.commanderTier = commanderTier;
            this.minShips = minShips;
            this.maxShips = maxShips;
        }

        /// <summary>その兵力（隻）がこの梯団の標準規模レンジ内か。</summary>
        public bool Contains(int ships) => ships >= minShips && ships <= maxShips;

        /// <summary>規模レンジの表示文字列（上限なしは「N隻〜」）。</summary>
        public string ScaleText => maxShips == int.MaxValue
            ? $"{minShips:#,0}隻〜"
            : $"{minShips:#,0}〜{maxShips:#,0}隻";
    }
}
