using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 戦略マップのイベント提示モーダル（S6・縦スライス）。支持低下などの政治的帰結を、タイトル＋本文＋
    /// 選択肢ボタンで提示する。表示中は <c>Time.timeScale=0</c> でポーズし、選択で効果を適用して閉じる。
    /// イベントエンジン(#116)の発火を画面に出す唯一の窓口。`FleetDetailPanel`/`SystemDetailPanel` と同じ
    /// ランタイム uGUI 生成パターン（見切れ防止のためフルスクリーン・縦並び）。`static Show` で開く。
    /// </summary>
    public class StrategyEventPanel : MonoBehaviour
    {
        private static StrategyEventPanel instance;

        /// <summary>モーダルが開いているか（GalaxyView/PauseManager が入力を譲るのに使う）。</summary>
        public static bool IsOpen => instance != null && instance.root != null && instance.root.activeSelf;

        /// <summary>
        /// 旧イベントモーダルの ON/OFF（既定 ON）。false の間は <see cref="Show"/> が無効＝中央モーダルを出さない。
        /// 決裁デスク（DESK #1628）へ移行中は <see cref="DecisionDeck"/> が OFF にし、イベントを右下スタックへ集約する。
        /// DESK-6（イベント/目安箱の合流）完了後はこのパネル自体を廃止予定。
        /// </summary>
        public static bool Enabled = true;

        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;
        private Transform choiceContainer;
        private float savedTimeScale = 1f;

        /// <summary>
        /// イベントを提示する。<paramref name="choices"/> は (ラベル, 押下時の効果) のリスト。
        /// 押すと効果を実行しモーダルを閉じる（ポーズ解除）。
        /// </summary>
        public static void Show(string title, string body, IList<(string label, Action onClick)> choices)
        {
            if (!Enabled) return; // ON/OFF トグル：OFF の間は旧モーダルを出さない（決裁デスクへ集約）
            if (instance == null)
            {
                GameObject go = new GameObject("StrategyEventPanel");
                instance = go.AddComponent<StrategyEventPanel>();
                instance.Build();
            }
            instance.Populate(title, body, choices);
        }

        /// <summary>モーダルを閉じてポーズを解除する。</summary>
        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        private void Build()
        {
            EnsureEventSystem();

            // Canvas（最前面）
            GameObject canvasObj = new GameObject("StrategyEventCanvas");
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1200; // 他UIより前面
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            // 全画面ディマー（これを root として表示切替）
            root = new GameObject("Root");
            root.transform.SetParent(canvasObj.transform, false);
            RectTransform rootRT = root.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero; rootRT.anchorMax = Vector2.one;
            rootRT.sizeDelta = Vector2.zero; rootRT.anchoredPosition = Vector2.zero;
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            // 中央フレーム（縦並び・自動レイアウト＝見切れ防止）
            GameObject frame = new GameObject("Frame");
            frame.transform.SetParent(root.transform, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0.5f, 0.5f); frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.pivot = new Vector2(0.5f, 0.5f); frameRT.anchoredPosition = Vector2.zero;
            frameRT.sizeDelta = new Vector2(560f, 0f);
            frame.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.98f);
            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(24, 24, 20, 20);
            vlg.spacing = 14f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            ContentSizeFitter csf = frame.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            titleText = MakeText(frame.transform, 28f, new Color(1f, 0.85f, 0.35f), 40f);
            bodyText = MakeText(frame.transform, 20f, new Color(0.9f, 0.92f, 0.96f), 80f);

            // 選択肢コンテナ
            GameObject cc = new GameObject("Choices");
            cc.transform.SetParent(frame.transform, false);
            cc.AddComponent<RectTransform>();
            VerticalLayoutGroup cvlg = cc.AddComponent<VerticalLayoutGroup>();
            cvlg.spacing = 8f; cvlg.childControlWidth = true; cvlg.childControlHeight = true;
            cvlg.childForceExpandWidth = true; cvlg.childForceExpandHeight = false;
            choiceContainer = cc.transform;

            root.SetActive(false);
        }

        private void Populate(string title, string body, IList<(string label, Action onClick)> choices)
        {
            titleText.text = title;
            bodyText.text = body;

            // 既存の選択肢ボタンをクリア
            for (int i = choiceContainer.childCount - 1; i >= 0; i--)
                Destroy(choiceContainer.GetChild(i).gameObject);

            if (choices != null)
            {
                foreach (var (label, onClick) in choices)
                {
                    Action captured = onClick;
                    CreateChoiceButton(label, () =>
                    {
                        captured?.Invoke();   // 効果を適用（帰結）
                        Close();              // モーダルを閉じてポーズ解除
                    });
                }
            }

            // ポーズして表示
            if (!root.activeSelf)
            {
                savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            root.SetActive(true);
        }

        private void Close()
        {
            if (root != null && root.activeSelf)
            {
                root.SetActive(false);
                Time.timeScale = savedTimeScale;
            }
        }

        private void CreateChoiceButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            GameObject btnObj = new GameObject("Choice_" + label);
            btnObj.transform.SetParent(choiceContainer, false);
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.24f, 0.36f, 1f);
            Button btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);
            LayoutElement le = btnObj.AddComponent<LayoutElement>();
            le.minHeight = 46f; le.preferredHeight = 46f;

            GameObject txtObj = new GameObject("Text");
            txtObj.transform.SetParent(btnObj.transform, false);
            RectTransform rt = txtObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero; rt.anchoredPosition = Vector2.zero;
            TextMeshProUGUI tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 20f; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white; ApplyFont(tmp);
        }

        private TextMeshProUGUI MakeText(Transform parent, float size, Color color, float minHeight)
        {
            GameObject obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size; tmp.color = color; tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            ApplyFont(tmp);
            LayoutElement le = obj.AddComponent<LayoutElement>();
            le.minHeight = minHeight;
            return tmp;
        }

        private static void ApplyFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset ja = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (ja != null) tmp.font = ja;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
