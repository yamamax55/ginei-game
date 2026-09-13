using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 集約された艦隊を示す<b>画面空間のバッジ</b>（#戦略MAPの艦艇表示）。
    ///
    /// なぜ画面空間か：ワールド空間の TextMesh を拡大するだけでは、①縮尺で実ピクセルが変わって読めない
    /// ②星系の3Dモデル（恒星/要塞）や星系のクリック領域と重なる ③縦横比で見え方が変わる、が解決しない。
    /// バッジなら実ピクセルで大きさを保証でき、星系の<b>すぐ脇</b>へ置いて重なりを避けられる。
    /// そして<b>描いた矩形をそのまま当たり判定に使う</b>ので「見えている場所を押せば開く」が保証される。
    ///
    /// 背景は陣営色（暗く落とした地に陣営色の縁）＝所属が一目で読め、所有リングとも役割が分かれる。
    /// </summary>
    public class FleetMarkerBadgeLayer : MonoBehaviour
    {
        /// <summary>バッジの実ピクセル最小文字サイズ（低解像度・縦長でも読める）。</summary>
        public const float MinFontPx = 15f;
        private const float BaseFont = 16f;

        private Canvas canvas;
        private RectTransform root;
        private TMP_FontAsset jpFont;

        private class Badge
        {
            public RectTransform rt;
            public Image bg;
            public Image accent;
            public TextMeshProUGUI label;
            public FleetCluster cluster;
            public Rect screenRect;   // 実スクリーン座標での矩形（＝当たり判定）
        }

        private readonly List<Badge> pool = new List<Badge>();
        private int usedCount;

        private void Awake()
        {
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");

            var canvasObj = new GameObject("FleetBadgeCanvas");
            canvasObj.transform.SetParent(transform, false);
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 870; // 通知(880)より後ろ・盤面より前
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            // バッジは押せる必要があるのでレイキャストを受ける（クリックは GalaxyView が矩形で判定）。
            canvasObj.AddComponent<GraphicRaycaster>();
            root = (RectTransform)canvasObj.transform;
        }

        /// <summary>
        /// まとまりの一覧からバッジを作り直す。<paramref name="colorOf"/> は陣営色を返す関数。
        /// <paramref name="viewport"/> はカメラの正規化矩形（マップ窓）＝バッジをこの中へ収める。
        /// </summary>
        /// <summary>星系の見かけ半径（スクリーンピクセル）。バッジをモデルの下へ逃がす量に使う。</summary>
        private float starScreenRadius = 24f;

        /// <summary>星系の見かけ半径を伝える（ワールド半径→スクリーン）。要塞は普通の星より大きい。</summary>
        public void SetStarScreenRadius(float px) => starScreenRadius = Mathf.Clamp(px, 8f, 200f);

        public void UpdateBadges(IList<FleetCluster> clusters, Camera cam,
            System.Func<Faction, Color> colorOf, Rect viewport, bool visible)
        {
            usedCount = 0;
            placed.Clear();
            if (visible && clusters != null && cam != null)
            {
                float font = StrategyScreenLayoutRules.MinDesignForActual(BaseFont, MinFontPx, Screen.width);
                for (int i = 0; i < clusters.Count; i++)
                {
                    FleetCluster c = clusters[i];
                    if (c == null || c.FleetCount == 0) continue;
                    Place(c, cam, colorOf, viewport, font);
                }
            }
            for (int i = usedCount; i < pool.Count; i++)
                if (pool[i].rt != null) pool[i].rt.gameObject.SetActive(false);
        }

        // このフレームで既に置いたバッジの矩形（重なりを避けるために積む）。
        private readonly List<Rect> placed = new List<Rect>();

        private void Place(FleetCluster c, Camera cam, System.Func<Faction, Color> colorOf, Rect viewport, float font)
        {
            // ★画面外の星系はバッジを出さない（実機レビュー指摘）。
            // 端へクランプすると、視界の外にある星系のバッジが画面の縁に山積みになって読めなくなる。
            Vector3 sp0 = cam.WorldToScreenPoint(c.center);
            float bx0 = viewport.xMin * Screen.width, bx1 = viewport.xMax * Screen.width;
            float by0 = viewport.yMin * Screen.height, by1 = viewport.yMax * Screen.height;
            if (sp0.z < 0f || sp0.x < bx0 || sp0.x > bx1 || sp0.y < by0 || sp0.y > by1) return;

            Badge b = Rent();
            b.cluster = c;

            string text = FleetClusterRules.MarkerLabel(c);
            if (c.moving)
            {
                string dir = FleetClusterRules.HeadingLabel(c.heading);
                if (!string.IsNullOrEmpty(dir)) text += $" {dir}へ";
            }
            b.label.text = text;
            b.label.fontSize = font;

            Color fac = colorOf != null ? colorOf(c.faction) : Color.white;
            // 地は暗く、縁だけ陣営色＝濃紺の航宙図でも文字が読め、所属も判る。
            b.bg.color = new Color(0.05f, 0.07f, 0.11f, 0.92f);
            b.accent.color = new Color(fac.r, fac.g, fac.b, c.anySelected ? 1f : 0.85f);

            // 大きさは文字量から（実ピクセルの下限つき）。
            float uiScale = Screen.width > 0 ? Screen.width / 1920f : 1f;
            float padX = 10f, padY = 5f;
            b.label.ForceMeshUpdate();
            Vector2 pref = b.label.GetPreferredValues(text);
            float w = Mathf.Max(64f, pref.x + padX * 2f);
            float h = Mathf.Max(font + padY * 2f, 26f);
            b.rt.sizeDelta = new Vector2(w, h);

            // ★星系の<b>右下</b>へ置く（実機QA：右上だと星系名を隠した）。
            // 星系名はモデルの上に出るので、下側なら名前とも 3D モデルとも当たらない。
            // 縦のオフセットはモデルの見かけ半径ぶん下げる（要塞は普通の星より大きい）。
            Vector3 sp = sp0;
            float offX = 16f * uiScale;
            float offY = (starScreenRadius + 10f) * uiScale;
            float wPx = w * uiScale, hPx = h * uiScale;
            float x = sp.x + offX;
            float y = sp.y - offY - hPx;                      // 下へ

            float vx0 = bx0, vx1 = bx1, vy0 = by0, vy1 = by1;
            if (x + wPx > vx1) x = sp.x - offX - wPx;         // 右端で左へ回す
            if (y < vy0) y = sp.y + offY;                     // 下端で上へ回す（名前と当たらない位置まで離す）

            // ★同じ地点に別のまとまり（敵味方／停泊と航行中）が居ると、同じ offset では完全に重なる。
            // 既に置いたバッジと当たる間は下へ積む＝どちらも読める。順序は入力（id 昇順）に従うので安定。
            float step = (hPx + 3f * uiScale);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                var probe = new Rect(Mathf.Clamp(x, vx0, Mathf.Max(vx0, vx1 - wPx)),
                                     Mathf.Clamp(y, vy0, Mathf.Max(vy0, vy1 - hPx)), wPx, hPx);
                bool hit = false;
                for (int i = 0; i < placed.Count; i++)
                    if (placed[i].Overlaps(probe)) { hit = true; break; }
                if (!hit) break;
                y -= step;                                    // 下へ積む
                if (y < vy0) { y = sp.y + offY + step * (attempt + 1); } // 下端まで来たら上へ回す
            }

            x = Mathf.Clamp(x, vx0, Mathf.Max(vx0, vx1 - wPx));
            y = Mathf.Clamp(y, vy0, Mathf.Max(vy0, vy1 - hPx));

            b.rt.anchorMin = b.rt.anchorMax = Vector2.zero;
            b.rt.pivot = Vector2.zero;
            b.rt.anchoredPosition = new Vector2(x / uiScale, y / uiScale);
            b.screenRect = new Rect(x, y, wPx, hPx);          // 描いた矩形＝当たり判定
            placed.Add(b.screenRect);
            b.rt.gameObject.SetActive(true);
        }

        /// <summary>スクリーン座標がどのバッジの上か（描画矩形と完全に一致した判定）。</summary>
        public bool TryHit(Vector2 screenPos, out FleetCluster cluster)
        {
            cluster = null;
            for (int i = 0; i < usedCount; i++)
            {
                if (pool[i].rt == null || !pool[i].rt.gameObject.activeSelf) continue;
                if (pool[i].screenRect.Contains(screenPos)) { cluster = pool[i].cluster; return true; }
            }
            return false;
        }

        private Badge Rent()
        {
            if (usedCount < pool.Count) { usedCount++; return pool[usedCount - 1]; }

            var go = new GameObject("Badge", typeof(RectTransform));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform;
            var bg = go.AddComponent<Image>();
            bg.raycastTarget = false;

            var accentGo = new GameObject("Accent", typeof(RectTransform));
            accentGo.transform.SetParent(go.transform, false);
            var art = (RectTransform)accentGo.transform;
            art.anchorMin = new Vector2(0f, 0f); art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0f, 0.5f);
            art.sizeDelta = new Vector2(5f, 0f);
            art.anchoredPosition = Vector2.zero;
            var accent = accentGo.AddComponent<Image>();
            accent.raycastTarget = false;

            var label = new GameObject("Label", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            var lrt = label.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.color = new Color(0.96f, 0.97f, 1f);
            label.margin = new Vector4(10f, 0f, 6f, 0f);
            label.raycastTarget = false;
            if (jpFont != null) label.font = jpFont;

            var b = new Badge { rt = rt, bg = bg, accent = accent, label = label };
            pool.Add(b);
            usedCount = pool.Count;
            return b;
        }
    }
}
