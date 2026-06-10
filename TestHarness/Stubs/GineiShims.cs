// =============================================================================
// Ginei シム（検証ハーネス専用）
// Formation/ShipClass は #496 で Core の単独ファイルへ切り出されたため、シム供給は不要になった
// （Core ソースをそのまま取り込む）。Unity 型の最小スタブだけを残す。
// =============================================================================
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
