using UnityEngine;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// ウィンドウ化した会戦（WIN-1 #2568）の入力アダプタ。会戦カメラは画面でなく RenderTexture へ
    /// 描くため、画面マウス座標を「会戦ウィンドウ内の RawImage 矩形 → 会戦カメラのビューポート →
    /// ワールド点」へ変換する唯一の窓口。会戦側（FleetCommander 等）はここを参照する。
    /// 非アクティブ（フルスクリーン会戦/戦略）時は何もしない＝従来動作（後方互換）。
    /// </summary>
    public static class BattleViewport
    {
        /// <summary>ウィンドウ化会戦が表示中か。</summary>
        public static bool Active { get; private set; }

        /// <summary>
        /// 会戦の戦場中心のワールド座標（WIN-1）。ウィンドウ化会戦は戦略マップと同じワールド空間に
        /// additive ロードされるため、遠方オフセットへ置いて戦略マップが会戦カメラに映り込むのを防ぐ。
        /// フルスクリーン会戦では (0,0)（従来どおり）。BattleSetup が配置オフセットに、FleetMovement/FleetAI が
        /// 戦場境界の中心に使う。
        /// </summary>
        public static Vector2 WorldOrigin;

        private static Camera cam;
        private static RectTransform mapRect;

        /// <summary>会戦ウィンドウのカメラと表示 RawImage 矩形を登録して有効化する。</summary>
        public static void SetActive(Camera battleCamera, RectTransform rawImageRect)
        {
            cam = battleCamera;
            mapRect = rawImageRect;
            Active = battleCamera != null && rawImageRect != null;
        }

        /// <summary>無効化（窓を閉じた/フルスクリーンへ戻したとき）。</summary>
        public static void Clear()
        {
            Active = false;
            cam = null;
            mapRect = null;
        }

        /// <summary>カーソルが会戦ウィンドウのマップ領域上にあるか。</summary>
        public static bool PointerInside
        {
            get
            {
                if (!Active || mapRect == null || Mouse.current == null) return false;
                return RectTransformUtility.RectangleContainsScreenPoint(mapRect, Mouse.current.position.ReadValue(), null);
            }
        }

        /// <summary>カーソル下の会戦ワールド点（z=0 面）を返す。領域外・無効なら false。</summary>
        public static bool TryPointerWorld(out Vector3 world)
        {
            world = Vector3.zero;
            if (!Active || cam == null || mapRect == null || Mouse.current == null) return false;
            Vector2 sp = Mouse.current.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(mapRect, sp, null)) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(mapRect, sp, null, out Vector2 local)) return false;
            Rect r = mapRect.rect;
            float vx = Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1e-4f, r.width));
            float vy = Mathf.Clamp01((local.y - r.yMin) / Mathf.Max(1e-4f, r.height));
            float depth = -cam.transform.position.z; // z=0 面までの距離
            world = cam.ViewportToWorldPoint(new Vector3(vx, vy, depth));
            world.z = 0f;
            return true;
        }
    }
}
