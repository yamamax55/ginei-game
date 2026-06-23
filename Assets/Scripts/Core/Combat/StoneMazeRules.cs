using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 八門遁甲の門（八陣図／石兵八陣の八つの門）。三吉門（休・生・開）は通り抜けられ、
    /// 五凶門（傷・杜・景・死・驚）に踏み込むと迷宮に閉じ込められる（罠）。
    /// 宣言順＝伝統的な並び（休→生→傷→杜→景→死→驚→開）＝方位の八等分に対応。
    /// </summary>
    public enum EightGate
    {
        休, // 吉
        生, // 吉
        傷, // 凶
        杜, // 凶
        景, // 凶
        死, // 凶
        驚, // 凶
        開  // 吉
    }

    /// <summary>石兵八陣（八陣図）の調整値。罠にかかった敵の機動低下など。</summary>
    public readonly struct StoneMazeParams
    {
        /// <summary>凶門から踏み込んだ敵の機動倍率（&lt;1＝迷って鈍る）。</summary>
        public readonly float disorientMobility;

        public StoneMazeParams(float disorientMobility)
        {
            this.disorientMobility = disorientMobility;
        }

        /// <summary>既定：罠の敵は機動半減。</summary>
        public static StoneMazeParams Default => new StoneMazeParams(0.5f);
    }

    /// <summary>
    /// 石兵八陣（諸葛亮の八陣図・三国志演義）の純ロジック（#石兵八陣・Core純ロジック・test-first）。
    /// 八方に門を配し、凶門から踏み込んだ敵を惑わせ閉じ込める（=機動を奪う）。陸遜を翻弄した巨石の陣。
    /// 八つの石塁のローカル配置（八角形）と、方位ごとの門の吉凶・罠判定・幻惑倍率を司る。
    /// `Squadron`(配置) と `FleetMovement`(敵の機動低下) がこの窓口を消費する（二重実装しない）。
    /// </summary>
    public static class StoneMazeRules
    {
        /// <summary>門（＝石塁）の数。</summary>
        public const int GateCount = 8;

        /// <summary>中心からの方位角（度・0=+X, 反時計回り）が属する門。</summary>
        public static EightGate GateAtAngle(float angleDeg)
        {
            float a = angleDeg % 360f;
            if (a < 0f) a += 360f;
            int idx = Mathf.FloorToInt(a / (360f / GateCount));
            if (idx < 0) idx = 0;
            if (idx >= GateCount) idx = GateCount - 1;
            return (EightGate)idx;
        }

        /// <summary>吉門（通り抜けられる）か。三吉門＝休・生・開。</summary>
        public static bool IsSafeGate(EightGate gate)
        {
            return gate == EightGate.休 || gate == EightGate.生 || gate == EightGate.開;
        }

        /// <summary>その方位から踏み込むと罠（凶門）にかかるか。</summary>
        public static bool IsTrapped(float approachAngleDeg)
        {
            return !IsSafeGate(GateAtAngle(approachAngleDeg));
        }

        /// <summary>罠にかかった敵の機動倍率（凶門=迷って鈍る／吉門=素通り1.0）。</summary>
        public static float DisorientMobilityFactor(bool trapped, StoneMazeParams p)
        {
            return trapped ? Mathf.Clamp(p.disorientMobility, 0.1f, 1f) : 1f;
        }

        /// <summary>八つの石塁のローカル配置（八角形・原点中心・index 0..7 を等間隔に）。</summary>
        public static Vector2 MazeNodeLocal(int index, float radius)
        {
            int i = ((index % GateCount) + GateCount) % GateCount;
            float a = i * (360f / GateCount) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
        }
    }
}
