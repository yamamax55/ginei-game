using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 勝利条件の幾何・時間判定ロジック（純ロジック・Core）。
    /// BattleManager が EvaluateVictory() 内で呼ぶ。MonoBehaviour 非依存。
    /// 対象条件: 突破 / 拠点保持。
    /// </summary>
    public static class VictoryRules
    {
        // ── 突破 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// 指定座標が戦場半径の外側（＝脱出成功）かどうかを返す。
        /// </summary>
        /// <param name="fleetPos">旗艦のワールド座標（XY平面）</param>
        /// <param name="battlefieldRadius">戦場端の半径（0以下なら常に false）</param>
        /// <returns>原点からの距離が battlefieldRadius 以上なら true</returns>
        public static bool BreakthroughAchieved(Vector2 fleetPos, float battlefieldRadius)
        {
            if (battlefieldRadius <= 0f) return false;
            return fleetPos.sqrMagnitude >= battlefieldRadius * battlefieldRadius;
        }

        // ── 拠点保持 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 指定座標が拠点ゾーン内かどうかを返す。
        /// </summary>
        /// <param name="fleetPos">旗艦のワールド座標（XY平面）</param>
        /// <param name="center">拠点の中心点</param>
        /// <param name="radius">拠点の判定半径（0以下なら常に false）</param>
        /// <returns>center からの距離が radius 以下なら true</returns>
        public static bool IsInZone(Vector2 fleetPos, Vector2 center, float radius)
        {
            if (radius <= 0f) return false;
            return (fleetPos - center).sqrMagnitude <= radius * radius;
        }

        /// <summary>
        /// 連続保持時間が要求時間を満たしているか返す。
        /// </summary>
        /// <param name="accumulatedSeconds">現在の連続保持秒数（game-time）</param>
        /// <param name="requiredSeconds">必要な保持時間（秒）。0以下なら常に false</param>
        /// <returns>accumulatedSeconds が requiredSeconds 以上なら true</returns>
        public static bool HoldAchieved(float accumulatedSeconds, float requiredSeconds)
        {
            if (requiredSeconds <= 0f) return false;
            return accumulatedSeconds >= requiredSeconds;
        }
    }
}
