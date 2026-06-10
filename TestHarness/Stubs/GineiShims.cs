// =============================================================================
// Ginei シム（検証ハーネス専用）
// MonoBehaviour ファイル内で定義されている enum を、当該ファイルを除外したまま供給する。
// 本体定義（Squadron.cs の Formation／EscortShip.cs の ShipClass）と一致させること。
// =============================================================================
namespace Ginei
{
    /// <summary>陣形（本体定義＝Squadron.cs）。</summary>
    public enum Formation { 紡錘陣, 鶴翼陣, 円陣, 横陣, 方陣 }

    /// <summary>艦種（#80・本体定義＝EscortShip.cs）。</summary>
    public enum ShipClass { 戦艦, 巡航艦, 駆逐艦 }
}

namespace UnityEngine
{
    /// <summary>Transform 最小スタブ（IShipTarget が型参照するだけ）。</summary>
    public class Transform : Object
    {
        public Vector3 position;
        public Vector3 up = Vector3.up;
        public Vector3 localScale = Vector3.one;
    }
}
