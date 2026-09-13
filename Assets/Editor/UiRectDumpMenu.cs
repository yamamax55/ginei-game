using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// UI の矩形を<b>実画面座標で</b>ダンプする（#UIの見切れ）。「切れている気がする」を推測で直すのをやめ、
    /// どの矩形がどこにあり、親のビューポートに収まっているかを数値で確かめるための道具。
    ///
    /// 主な用途：艦隊一覧の行の名前ラベルが左へはみ出していないか（RectMask2D に切られていないか）。
    /// Play 中に対象のパネルを開いた状態で実行する。
    /// </summary>
    public static class UiRectDumpMenu
    {
        [MenuItem("Ginei/QA: 艦隊一覧の矩形をダンプ", false, 320)]
        public static void DumpFleetList()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog("矩形ダンプ", "Play 中に、艦隊一覧を開いた状態で実行してください。", "OK");
                return;
            }

            var panel = GameObject.Find("FleetClusterListPanel");
            if (panel == null)
            {
                EditorUtility.DisplayDialog("矩形ダンプ", "FleetClusterListPanel が見つかりません（一覧を開いてください）。", "OK");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"画面 {Screen.width}x{Screen.height}　UIスケール(幅基準) {Screen.width / 1920f:F3}");

            // ビューポート（RectMask2D）を先に出す＝この矩形の外は切られる。
            var mask = panel.GetComponentInChildren<RectMask2D>(true);
            Rect maskRect = default;
            if (mask != null)
            {
                maskRect = ScreenRect((RectTransform)mask.transform);
                sb.AppendLine($"[マスク] Viewport  x {maskRect.xMin:F0}..{maskRect.xMax:F0}  y {maskRect.yMin:F0}..{maskRect.yMax:F0}");
            }

            var contentRT = FindChild(panel.transform, "Content") as RectTransform;
            if (contentRT != null)
            {
                Rect c = ScreenRect(contentRT);
                sb.AppendLine($"[中身] Content   x {c.xMin:F0}..{c.xMax:F0}  y {c.yMin:F0}..{c.yMax:F0}" +
                              $"  sizeDelta={contentRT.sizeDelta}  anchoredPos={contentRT.anchoredPosition}");
                if (mask != null && (c.xMin < maskRect.xMin - 0.5f || c.xMax > maskRect.xMax + 0.5f))
                    sb.AppendLine("  ✗ Content がマスクより横に広い＝行の左右が切られる（sizeDelta.x を 0 に）");
            }

            var texts = panel.GetComponentsInChildren<TextMeshProUGUI>(true);
            int clipped = 0;
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (!t.gameObject.activeInHierarchy) continue;
                Rect r = ScreenRect(t.rectTransform);
                bool bad = mask != null && (r.xMin < maskRect.xMin - 0.5f);
                if (bad) clipped++;
                sb.AppendLine($"  {(bad ? "✗" : "・")} \"{t.text}\"  x {r.xMin:F0}..{r.xMax:F0}  y {r.yMin:F0}..{r.yMax:F0}" +
                              $"  実文字 {t.fontSize * (Screen.width / 1920f):F1}px");
            }

            sb.AppendLine(clipped == 0
                ? "判定：左端の見切れは検出されませんでした。"
                : $"判定：{clipped} 件の文字がマスク左端より外にあります＝見切れます。");

            Debug.Log($"[UiRectDump] 艦隊一覧\n{sb}");
            EditorUtility.DisplayDialog("矩形ダンプ",
                clipped == 0 ? "見切れは検出されませんでした。詳細は Console。" :
                               $"{clipped} 件の見切れを検出。詳細は Console。", "OK");
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform r = FindChild(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }

        /// <summary>RectTransform の4隅から実画面座標の矩形を作る（Overlay Canvas なので corners がそのまま画面座標）。</summary>
        private static Rect ScreenRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = Mathf.Min(c[0].x, c[2].x), xMax = Mathf.Max(c[0].x, c[2].x);
            float yMin = Mathf.Min(c[0].y, c[2].y), yMax = Mathf.Max(c[0].y, c[2].y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }
    }
}
