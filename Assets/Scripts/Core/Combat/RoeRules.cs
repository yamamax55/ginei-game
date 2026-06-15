namespace Ginei
{
    /// <summary>
    /// 交戦規定（ROE: Rules of Engagement）の純ロジック（#2258）。
    /// スタンスごとに発砲・追尾・前進の可否と前進度を返す唯一の窓口。
    /// 基準値は非破壊（実効値パターン）。test-first。
    /// </summary>
    public static class RoeRules
    {
        /// <summary>
        /// 指定スタンスで発砲できるか。
        /// 射撃管制／退避は false。攻撃的／防御的は true。
        /// </summary>
        public static bool CanFire(EngagementStance s)
        {
            return s == EngagementStance.攻撃的 || s == EngagementStance.防御的;
        }

        /// <summary>
        /// 指定スタンスで敵を追尾（深追い）できるか。
        /// 攻撃的のみ true。防御的以下は false（陣地を離れない）。
        /// </summary>
        public static bool CanPursue(EngagementStance s)
        {
            return s == EngagementStance.攻撃的;
        }

        /// <summary>
        /// 前進の許容度（0.0〜1.0）を返す。
        /// 攻撃的=1.0 / 防御的=0.5 / 射撃管制=0.3 / 退避=0.0。
        /// FleetAI が接近時の移動量を抑制するために使用する（基準速度は非破壊）。
        /// </summary>
        public static float AdvanceFactor(EngagementStance s)
        {
            switch (s)
            {
                case EngagementStance.攻撃的:   return 1.0f;
                case EngagementStance.防御的:   return 0.5f;
                case EngagementStance.射撃管制: return 0.3f;
                case EngagementStance.退避:    return 0.0f;
                default:                       return 1.0f; // 未知値は攻撃的と同等（後方互換）
            }
        }
    }
}
