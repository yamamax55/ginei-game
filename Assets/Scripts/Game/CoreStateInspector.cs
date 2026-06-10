using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 汎用 状態インスペクタ（観測オーバーレイ・A案）。J キーで開閉し、<b>登録ルート</b>をリフレクションで
    /// 再帰的に舐めて、Core の純データ（社会・政治シミュ層 等）を**型に依存せず丸ごとダンプ**する。
    /// float の 0..1 はバー、数値/真偽/列挙/リスト/入れ子オブジェクトを自動整形。手仕上げの
    /// <see cref="CampaignObserverOverlay"/>（ヒーロー表示）の汎用版＝**新しい Core 型が増えても登録1行で覗ける**。
    ///
    /// 既定で <see cref="StrategySession"/> 配下（Campaign/Provinces/Clock）を登録。自動コード化ルーチンは
    /// 新モジュールの state 型を <see cref="Register"/> で足すだけ＝生成と観測が歩調を合わせる。観測専用＝状態は変えない。
    /// </summary>
    public class CoreStateInspector : MonoBehaviour
    {
        // ===== 調整可能なパラメーター =====

        [Header("外観")]
        public int canvasSortingOrder = 1095;
        public float dimAlpha = 0.6f;
        [Tooltip("本文のフォントサイズ（大きめ＝読みやすさ優先）")]
        public float bodyFontSize = 20f;
        [Tooltip("0..1 のバー桁数")]
        public int barWidth = 14;

        [Header("再帰の上限（暴走・循環ガード）")]
        [Tooltip("入れ子オブジェクトを展開する最大深さ")]
        public int maxDepth = 6;
        [Tooltip("リスト/辞書を展開する最大要素数")]
        public int maxItems = 40;

        // ===== 登録ルート（静的＝シーンを跨いで保持） =====

        /// <summary>ダンプ対象の根（ラベル＋遅延解決子）。毎フレーム解決するので null でも安全。</summary>
        private static readonly List<(string label, Func<object> resolve)> roots
            = new List<(string, Func<object>)>();
        private static bool defaultsRegistered;

        /// <summary>
        /// インスペクタにダンプ対象のルートを登録する（重複ラベルは上書き）。自動コード化ルーチンや
        /// 各モジュールの初期化から呼ぶ＝「Core を1個生やしたら登録1行」で観測が追従する。
        /// </summary>
        public static void Register(string label, Func<object> resolve)
        {
            if (string.IsNullOrEmpty(label) || resolve == null) return;
            for (int i = 0; i < roots.Count; i++)
                if (roots[i].label == label) { roots[i] = (label, resolve); return; }
            roots.Add((label, resolve));
        }

        /// <summary>既定ルート（戦役世界状態）を1度だけ登録する。</summary>
        private static void EnsureDefaultRoots()
        {
            if (defaultsRegistered) return;
            defaultsRegistered = true;
            Register("Campaign (CampaignState)", () => StrategySession.Campaign);
            Register("Provinces (星系内政)",      () => StrategySession.Provinces);
            Register("Clock (GameClock)",         () => StrategySession.Clock);
        }

        // ===== 内部状態 =====

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;

        // ===== 自動生成（HelpOverlay と同型） =====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Strategy" && scene.name != "Battle") return;
            if (UnityEngine.Object.FindAnyObjectByType<CoreStateInspector>() != null) return;
            GameObject go = new GameObject("CoreStateInspector");
            go.AddComponent<CoreStateInspector>();
        }

        // ===== ライフサイクル =====

        private void Awake()
        {
            EnsureDefaultRoots();
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.状態インスペクタ切替))
                Toggle();
            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle()
        {
            bool next = panel != null && !panel.activeSelf;
            SetVisible(next);
        }

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体（リフレクション） =====

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            sb.Append("<b>汎用 状態インスペクタ</b>　登録ルート: ").Append(roots.Count).Append("　(J で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            for (int i = 0; i < roots.Count; i++)
            {
                object obj = null;
                try { obj = roots[i].resolve(); } catch { obj = null; }
                var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
                sb.Append('\n');
                DumpValue(sb, roots[i].label, obj, 0, visited);
            }
            return sb.ToString();
        }

        /// <summary>1つの値を name 付きでダンプする（型に応じて整形・再帰）。</summary>
        private void DumpValue(StringBuilder sb, string name, object obj, int depth, HashSet<object> visited)
        {
            string indent = Indent(depth);

            if (obj == null) { sb.Append(indent).Append(Field(name)).Append("<color=#7a8694>null</color>\n"); return; }

            Type t = obj.GetType();

            // --- 数値（float/double は 0..1 ならバー、それ以外は数値） ---
            if (t == typeof(float) || t == typeof(double))
            {
                double v = Convert.ToDouble(obj);
                if (v >= 0d && v <= 1d) { sb.Append(indent).Append(Field(name)); AppendBar(sb, (float)v); }
                else sb.Append(indent).Append(Field(name)).Append("<color=#ffd28a>").Append(v.ToString("0.###")).Append("</color>\n");
                return;
            }
            if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) || t == typeof(uint) || t == typeof(ulong))
            {
                sb.Append(indent).Append(Field(name)).Append("<color=#ffd28a>").Append(Convert.ToInt64(obj).ToString("#,0")).Append("</color>\n");
                return;
            }
            if (t == typeof(bool))
            {
                bool b = (bool)obj;
                sb.Append(indent).Append(Field(name))
                  .Append(b ? "<color=#ff8a8a>✔ true</color>" : "<color=#6f7d8a>✘ false</color>").Append('\n');
                return;
            }
            if (t.IsEnum)
            {
                sb.Append(indent).Append(Field(name)).Append("<color=#c9b3ff>").Append(obj).Append("</color>\n");
                return;
            }
            if (t == typeof(string))
            {
                sb.Append(indent).Append(Field(name)).Append("<color=#a0e0a0>\"").Append(obj).Append("\"</color>\n");
                return;
            }
            if (t == typeof(Vector2)) { var v = (Vector2)obj; sb.Append(indent).Append(Field(name)).Append($"<color=#8ad0ff>({v.x:0.##}, {v.y:0.##})</color>\n"); return; }
            if (t == typeof(Vector3)) { var v = (Vector3)obj; sb.Append(indent).Append(Field(name)).Append($"<color=#8ad0ff>({v.x:0.##}, {v.y:0.##}, {v.z:0.##})</color>\n"); return; }

            // --- 辞書 ---
            if (obj is IDictionary dict)
            {
                sb.Append(indent).Append(Field(name)).Append("<color=#9aa7b3>{ ").Append(dict.Count).Append(" 件 }</color>\n");
                if (depth >= maxDepth) { sb.Append(Indent(depth + 1)).Append("<color=#7a8694>…(深さ上限)</color>\n"); return; }
                int n = 0;
                foreach (DictionaryEntry e in dict)
                {
                    if (n++ >= maxItems) { sb.Append(Indent(depth + 1)).Append("<color=#7a8694>…(").Append(dict.Count - maxItems).Append(" 件省略)</color>\n"); break; }
                    DumpValue(sb, "[" + e.Key + "]", e.Value, depth + 1, visited);
                }
                return;
            }

            // --- リスト/配列など（string は上で処理済み） ---
            if (obj is IEnumerable en)
            {
                var items = new List<object>();
                foreach (object o in en) items.Add(o);
                sb.Append(indent).Append(Field(name)).Append("<color=#9aa7b3>[ ").Append(items.Count).Append(" 件 ]</color>\n");
                if (depth >= maxDepth) { sb.Append(Indent(depth + 1)).Append("<color=#7a8694>…(深さ上限)</color>\n"); return; }
                for (int i = 0; i < items.Count; i++)
                {
                    if (i >= maxItems) { sb.Append(Indent(depth + 1)).Append("<color=#7a8694>…(").Append(items.Count - maxItems).Append(" 件省略)</color>\n"); break; }
                    DumpValue(sb, "[" + i + "]", items[i], depth + 1, visited);
                }
                return;
            }

            // --- Ginei 名前空間のオブジェクト/構造体だけ再帰展開 ---
            if (t.Namespace != null && t.Namespace.StartsWith("Ginei"))
            {
                // 循環ガード（参照型のみ）
                if (!t.IsValueType)
                {
                    if (visited.Contains(obj)) { sb.Append(indent).Append(Field(name)).Append("<color=#7a8694>↺ (循環参照)</color>\n"); return; }
                    visited.Add(obj);
                }
                sb.Append(indent).Append("<color=#bfe9c0>").Append(name).Append("</color> <color=#5b6b7a>(").Append(t.Name).Append(")</color>\n");
                if (depth >= maxDepth) { sb.Append(Indent(depth + 1)).Append("<color=#7a8694>…(深さ上限)</color>\n"); return; }
                DumpMembers(sb, obj, t, depth + 1, visited);
                return;
            }

            // --- それ以外（System 型等）は ToString() のリーフ ---
            sb.Append(indent).Append(Field(name)).Append("<color=#9aa7b3>").Append(obj).Append("</color>\n");
        }

        /// <summary>Ginei オブジェクトの public フィールド＋（取得可能な）public プロパティを順にダンプする。</summary>
        private void DumpMembers(StringBuilder sb, object obj, Type t, int depth, HashSet<object> visited)
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < fields.Length; i++)
            {
                object val;
                try { val = fields[i].GetValue(obj); } catch { continue; }
                DumpValue(sb, fields[i].Name, val, depth, visited);
            }

            // プロパティ（引数なし・読み取り可能のみ。getter の副作用/例外は握りつぶしてスキップ）
            PropertyInfo[] props = t.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < props.Length; i++)
            {
                if (!props[i].CanRead || props[i].GetIndexParameters().Length > 0) continue;
                object val;
                try { val = props[i].GetValue(obj); } catch { continue; }
                DumpValue(sb, props[i].Name + " ▸", val, depth, visited);
            }
        }

        // ===== 整形ヘルパー =====

        private static string Indent(int depth)
        {
            // 全角寄りの見やすいインデント（リッチテキストの色は付けない）
            return depth <= 0 ? "" : new string('　', depth);
        }

        private static string Field(string name) => "<color=#cdd6df>" + name + "</color> ＝ ";

        /// <summary>0..1 を「████░░░░ 0.42」のバーで追加する。</summary>
        private void AppendBar(StringBuilder sb, float v01)
        {
            v01 = Mathf.Clamp01(v01);
            int filled = Mathf.RoundToInt(v01 * barWidth);
            sb.Append("<color=#7fd4ff>");
            for (int i = 0; i < barWidth; i++) sb.Append(i < filled ? '█' : '░');
            sb.Append("</color> ").Append(v01.ToString("0.00")).Append('\n');
        }

        /// <summary>参照等価でハッシュする比較子（循環参照ガード用）。</summary>
        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new ReferenceEqualityComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        // ===== UI 構築（大きめ・ほぼ全画面） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("CoreStateInspectorCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("InspectorPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            // 大きめフレーム＝画面の大半（左右4%・上下5%余白）を使う＝読みやすさ優先
            GameObject frame = new GameObject("InspectorFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.04f, 0.05f);
            frameRT.anchorMax = new Vector2(0.96f, 0.95f);
            frameRT.offsetMin = Vector2.zero;
            frameRT.offsetMax = Vector2.zero;

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = new Color(0.03f, 0.05f, 0.09f, 0.97f);

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 16, 16);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            BuildScrollBody(frame.transform);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("InspectorScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 36f;

            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollObj.transform, false);
            RectTransform viewportRT = viewport.AddComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero;
            viewportRT.anchorMax = Vector2.one;
            viewportRT.sizeDelta = Vector2.zero;
            viewportRT.anchoredPosition = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            viewport.AddComponent<RectMask2D>();
            scrollRect.viewport = viewportRT;

            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.anchoredPosition = Vector2.zero;
            contentRT.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(8, 8, 4, 4);
            contentVlg.childAlignment = TextAnchor.UpperLeft;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = true;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;

            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.content = contentRT;

            GameObject bodyObj = new GameObject("Body");
            bodyObj.transform.SetParent(content.transform, false);
            bodyLabel = bodyObj.AddComponent<TextMeshProUGUI>();
            bodyLabel.text = "";
            bodyLabel.fontSize = bodyFontSize;
            bodyLabel.color = new Color(0.9f, 0.93f, 0.96f);
            bodyLabel.alignment = TextAlignmentOptions.TopLeft;
            bodyLabel.richText = true;
            bodyLabel.raycastTarget = false;
            ApplyJapaneseFont(bodyLabel);
        }

        private static void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
