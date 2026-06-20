using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
        /// <summary>ウィンドウ化会戦が表示中か（いずれかの会戦ウィンドウが開いている）。</summary>
        public static bool Active { get; private set; }

        private static Camera cam;
        private static RectTransform mapRect;

        /// <summary>
        /// 現在フォーカス中（プレイヤー入力を受ける）会戦シーン（WIN-3 #2570）。複数会戦ウィンドウが
        /// 開いているとき、カーソルが乗っている窓の会戦だけが選択・指揮を受け付ける。
        /// FleetCommander/CameraController が「自分のシーン＝フォーカス中か」を <see cref="IsFocused"/> で判定する。
        /// </summary>
        public static Scene FocusedScene { get; private set; }

        /// <summary>指定シーンが現在フォーカス中の会戦か。</summary>
        public static bool IsFocused(Scene scene) => Active && scene == FocusedScene;

        /// <summary>フォーカス会戦のカメラと表示 RawImage 矩形を登録して有効化する。</summary>
        public static void SetActive(Scene scene, Camera battleCamera, RectTransform rawImageRect)
        {
            FocusedScene = scene;
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
