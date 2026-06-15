namespace Ginei
{
    /// <summary>
    /// 武器種ごとの対象適性ルール（#2256）。
    /// 三すくみ的な編成の読み合い：長距離砲は対旗艦に有利、対小型砲は対配下艦に有利、点防御は守勢用で控えめ。
    /// 倍率はすべて const に集約し、基準ダメージを非破壊のまま実効値として乗算する（実効値パターン）。
    /// test-first・Core 純ロジック（MonoBehaviour 非依存）。
    /// </summary>
    public static class WeaponTypeRules
    {
        // ---- TargetAptitude の定数（調整値を一か所に集約） ----

        // ビーム／ミサイル：万能（対象の種別を問わず中立）
        public const float BeamAptitudeFlagship  = 1.00f;
        public const float BeamAptitudeEscort    = 1.00f;
        public const float MissileAptitudeFlagship = 1.00f;
        public const float MissileAptitudeEscort   = 1.00f;

        // 長距離砲：旗艦（大型・低速）を狙いやすく、散らばった配下艦には当てにくい
        public const float LongRangeAptitudeFlagship = 1.25f;
        public const float LongRangeAptitudeEscort   = 0.85f;

        // 対小型：速射で配下艦（小型多数）を薙ぎ払う。旗艦（大型）には威力が分散して効きにくい
        public const float AntiSmallAptitudeFlagship = 0.70f;
        public const float AntiSmallAptitudeEscort   = 1.40f;

        // 点防御：守勢的な武装。どちらも控えめ（攻勢火力としての適性は低い）
        public const float PointDefenseAptitudeFlagship = 0.80f;
        public const float PointDefenseAptitudeEscort   = 0.80f;

        // ---- FireIntervalFactor の定数（発射間隔の補正係数） ----

        // 長距離砲：装填が遅い
        public const float LongRangeIntervalFactor  = 1.50f;
        // 対小型：連射が速い
        public const float AntiSmallIntervalFactor  = 0.70f;
        // その他：変更なし（1.0 倍）
        public const float DefaultIntervalFactor    = 1.00f;

        /// <summary>
        /// 武器種と標的の種別（旗艦か配下艦か）から、baseDamage に乗算する適性倍率を返す。
        /// 実効値パターン：基準 damage フィールドは変えず、呼び出し側で乗算して実効ダメージを求める。
        /// </summary>
        /// <param name="type">攻撃側の武器種。</param>
        /// <param name="targetIsFlagship">標的が旗艦（FleetStrength）なら true、配下艦（EscortShip）なら false。</param>
        /// <returns>与ダメージへの乗算倍率（1.0 で中立）。</returns>
        public static float TargetAptitude(WeaponType type, bool targetIsFlagship)
        {
            return type switch
            {
                WeaponType.ビーム   => targetIsFlagship ? BeamAptitudeFlagship   : BeamAptitudeEscort,
                WeaponType.ミサイル => targetIsFlagship ? MissileAptitudeFlagship : MissileAptitudeEscort,
                WeaponType.長距離砲 => targetIsFlagship ? LongRangeAptitudeFlagship : LongRangeAptitudeEscort,
                WeaponType.対小型   => targetIsFlagship ? AntiSmallAptitudeFlagship : AntiSmallAptitudeEscort,
                WeaponType.点防御   => targetIsFlagship ? PointDefenseAptitudeFlagship : PointDefenseAptitudeEscort,
                _                   => 1.0f,
            };
        }

        /// <summary>
        /// 武器種から発射間隔の倍率を返す（長距離砲＝遅い、対小型＝速い。基準 fireInterval を非破壊で運用）。
        /// 呼び出し側で fireInterval に乗算して実効インターバルを得る。
        /// </summary>
        public static float FireIntervalFactor(WeaponType type)
        {
            return type switch
            {
                WeaponType.長距離砲 => LongRangeIntervalFactor,
                WeaponType.対小型   => AntiSmallIntervalFactor,
                _                   => DefaultIntervalFactor,
            };
        }
    }
}
