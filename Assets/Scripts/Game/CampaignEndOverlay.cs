using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// キャンペーン（戦役）の終了画面（遊べる縦スライスの締め）。<see cref="GalaxyView.RunCampaignVictoryCheck"/> が
    /// プレイヤー勢力の勝敗を判定したとき全画面モーダルで結果（勝利/敗北・支配率・決着日時）を提示する。
    /// 「タイトルへ戻る」で <see cref="StrategySession"/> を破棄してタイトルへ／「観戦を続ける」で閉じて
    /// （停止中の）盤面を眺められる。UIは実行時コード生成（<see cref="SystemDetailPanel"/> と同作法）。
    /// 統一クロックは既に <see cref="GameClock.Pause"/> 済み＝本オーバーレイは入力を塞ぐだけ（<c>IsOpen</c>）。
    /// </summary>
    public class CampaignEndOverlay : MonoBehaviour
    {
        private static CampaignEndOverlay instance;

        /// <summary>終了画面が開いているか（GalaxyView が入力を譲るために参照）。</summary>
        public static bool IsOpen => instance != null && instance.isOpen;

        private bool isOpen;
        private GameObject root;
        private TextMeshProUGUI titleText;
        private TextMeshProUGUI bodyText;

        /// <summary>戦役の決着を表示する（必要なら生成）。win=勝利か、ownedFraction=支配率(0..1)。</summary>
        public static void Show(bool win, Faction player, float ownedFraction)
        {
            if (instance == null)
            {
                GameObject go = new GameObject("CampaignEndOverlay");
                instance = go.AddComponent<CampaignEndOverlay>();
                instance.Build();
            }
            instance.Display(win, player, ownedFraction);
        }

        private void Build()
        {
            EnsureEventSystem();

            GameObject canvasObj = new GameObject("CampaignEndCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120; // 観測オーバーレイ(90台)より前面
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();

            root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(canvasObj.transform, false);
            StretchFull(root.GetComponent<RectTransform>());

            // 背景ディマー（暗転＝終局感。クリックでは閉じない＝決断を促す）
            GameObject dim = new GameObject("Dimmer", typeof(RectTransform));
            dim.transform.SetParent(root.transform, false);
            StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.82f);

            // パネル（中央）
            GameObject panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(root.transform, false);
            RectTransform pRT = panel.GetComponent<RectTransform>();
            pRT.anchorMin = pRT.anchorMax = pRT.pivot = new Vector2(0.5f, 0.5f);
            pRT.sizeDelta = new Vector2(640f, 420f);
            pRT.anchoredPosition = Vector2.zero;
            Image pImg = panel.AddComponent<Image>();
            pImg.color = new Color(0.06f, 0.07f, 0.12f, 0.98f);

            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(32, 32, 28, 28);
            vlg.spacing = 16f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;

            titleText = CreateText(panel.transform, "", 44f, FontStyles.Bold, TextAlignmentOptions.Center);

            bodyText = CreateText(panel.transform, "", 24f, FontStyles.Normal, TextAlignmentOptions.Center);
            LayoutElement bodyLE = bodyText.gameObject.AddComponent<LayoutElement>();
            bodyLE.flexibleHeight = 1f;

            CreateButton(panel.transform, "タイトルへ戻る", BackToTitle, new Color(0.5f, 0.18f, 0.22f, 1f));
            CreateButton(panel.transform, "観戦を続ける", Close, new Color(0.2f, 0.25f, 0.4f, 1f));

            root.SetActive(false);
        }

        private void Display(bool win, Faction player, float ownedFraction)
        {
            if (titleText != null)
            {
                titleText.text = win ? "勝　利" : "敗　北";
                titleText.color = win ? new Color(1f, 0.85f, 0.35f) : new Color(0.95f, 0.4f, 0.35f);
            }

            int pct = Mathf.RoundToInt(Mathf.Clamp01(ownedFraction) * 100f);
            string dateLine = TimeDisplay.TryFormatNow(out string dateText, out _)
                ? dateText.Replace("\n", "　") : "";
            if (bodyText != null)
            {
                bodyText.text = win
                    ? $"{player} は銀河を制覇した。\n支配 {pct}%\n\n{dateLine}"
                    : $"{player} は星系をすべて失った。\n支配 {pct}%\n\n{dateLine}";
            }

            isOpen = true;
            if (root != null) root.SetActive(true);
        }

        /// <summary>終了画面を閉じる（停止中の盤面を眺められる＝観戦）。</summary>
        public void Close()
        {
            isOpen = false;
            if (root != null) root.SetActive(false);
        }

        /// <summary>戦役を畳んでタイトルへ戻る（次の戦役を新規に始められるよう状態をクリア）。</summary>
        public void BackToTitle()
        {
            StrategySession.Clear();
            BattleHandoff.Clear();
            GalaxyView.ResetCampaignStatics();
            isOpen = false;
            SceneManager.LoadScene("Title");
        }

        // ===== UI生成ヘルパ（SystemDetailPanel と同作法） =====

        private TextMeshProUGUI CreateText(Transform parent, string text, float size, FontStyles style, TextAlignmentOptions align)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.fontStyle = style; t.alignment = align;
            t.color = Color.white; t.raycastTarget = false;
            ApplyJapaneseFont(t);
            return t;
        }

        private void CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick, Color color)
        {
            GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Image img = go.AddComponent<Image>();
            img.color = color;
            Button btn = go.AddComponent<Button>();
            btn.transition = UnityEngine.UI.Selectable.Transition.None; // Ginei.Selectable と衝突回避
            btn.onClick.AddListener(onClick);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 56f;
            TextMeshProUGUI txt = CreateText(go.transform, label, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
            StretchFull(txt.rectTransform);
        }

        private void ApplyJapaneseFont(TextMeshProUGUI tmp)
        {
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) tmp.font = jaFont;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
