using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;

namespace Ginei
{
    /// <summary>
    /// 非同期でのシーン遷移とロード画面表示を管理するクラス。
    /// </summary>
    public class SceneLoader : MonoBehaviour
    {
        private static SceneLoader instance;
        public static SceneLoader Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Object.FindFirstObjectByType<SceneLoader>();
                    if (instance == null)
                    {
                        GameObject go = new GameObject("SceneLoader");
                        instance = go.AddComponent<SceneLoader>();
                        if (Application.isPlaying)
                        {
                            DontDestroyOnLoad(go);
                        }
                    }
                }
                return instance;
            }
        }

        [Header("設定")]
        [Tooltip("ロード画面の最低表示時間（秒）")]
        public float minDisplayTime = 0.8f;

        [Header("UI参照")]
        private GameObject overlayRoot;
        private UnityEngine.UI.Slider progressBar;
        private TextMeshProUGUI loadingText;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
                SetupLoadingUI();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// 指定されたシーンへ非同期で遷移します。
        /// </summary>
        public void LoadScene(string sceneName)
        {
            Debug.Log("SceneLoader: LoadScene called for: " + sceneName);
            StartCoroutine(LoadCoroutine(sceneName));
        }

        private IEnumerator LoadCoroutine(string sceneName)
        {
            Debug.Log("SceneLoader: LoadCoroutine started for: " + sceneName);
            if (overlayRoot == null)
            {
                Debug.Log("SceneLoader: SetupLoadingUI called from LoadCoroutine.");
                SetupLoadingUI();
            }
            
            overlayRoot.SetActive(true);
            progressBar.value = 0;
            
            float startTime = Time.unscaledTime;
            Debug.Log("SceneLoader: Starting LoadSceneAsync...");
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError("SceneLoader: LoadSceneAsync returned null for scene: " + sceneName);
                yield break;
            }
            op.allowSceneActivation = false;

            while (!op.isDone)
            {
                // progress は 0.9 で止まる（読み込み完了、アクティベーション待ち）
                float progress = Mathf.Clamp01(op.progress / 0.9f);
                progressBar.value = progress;

                float elapsedTime = Time.unscaledTime - startTime;
                
                // 読み込みが 0.9 に達し、かつ最低表示時間を超えたら遷移許可
                if (op.progress >= 0.9f && elapsedTime >= minDisplayTime)
                {
                    Debug.Log("SceneLoader: Loading complete. Activating scene...");
                    op.allowSceneActivation = true;
                }

                yield return null;
            }

            Debug.Log("SceneLoader: Scene transition complete.");
            // 完了したら少し待ってから消す（あるいは即座に）
            yield return new WaitForSecondsRealtime(0.1f);
            overlayRoot.SetActive(false);
        }

        private void SetupLoadingUI()
        {
            if (overlayRoot != null) return;

            // 1. Canvas
            GameObject canvasObj = new GameObject("LoadingCanvas");
            canvasObj.transform.SetParent(transform, false);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 最前面

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Overlay Background（不透明＝前のシーンを完全に隠す）
            // 注意: UI 子要素は SetParent(parent, false) でローカル基準にする（ズレ防止）
            overlayRoot = new GameObject("Overlay", typeof(RectTransform));
            overlayRoot.transform.SetParent(canvasObj.transform, false);
            RectTransform overlayRT = overlayRoot.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            UnityEngine.UI.Image bg = overlayRoot.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0.02f, 0.02f, 0.05f, 1f);

            // 3. Loading Text（中央やや上）
            GameObject textObj = new GameObject("LoadingText", typeof(RectTransform));
            textObj.transform.SetParent(overlayRoot.transform, false);
            loadingText = textObj.AddComponent<TextMeshProUGUI>();
            loadingText.text = "ロード中...";
            loadingText.fontSize = 40;
            loadingText.alignment = TextAlignmentOptions.Center;
            loadingText.color = Color.white;

            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) loadingText.font = jaFont;

            RectTransform textRT = loadingText.rectTransform;
            textRT.anchorMin = new Vector2(0.5f, 0.5f);
            textRT.anchorMax = new Vector2(0.5f, 0.5f);
            textRT.pivot = new Vector2(0.5f, 0.5f);
            textRT.anchoredPosition = new Vector2(0f, 40f);
            textRT.sizeDelta = new Vector2(800f, 80f);

            // 4. Progress Bar (Slider)（中央やや下）
            GameObject sliderObj = new GameObject("ProgressBar", typeof(RectTransform));
            sliderObj.transform.SetParent(overlayRoot.transform, false);
            progressBar = sliderObj.AddComponent<UnityEngine.UI.Slider>();
            progressBar.interactable = false;
            progressBar.transition = UnityEngine.UI.Selectable.Transition.None;
            progressBar.minValue = 0f;
            progressBar.maxValue = 1f;
            progressBar.value = 0f;

            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRT.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRT.pivot = new Vector2(0.5f, 0.5f);
            sliderRT.sizeDelta = new Vector2(600f, 24f);
            sliderRT.anchoredPosition = new Vector2(0f, -30f);

            // Slider Background
            GameObject sliderBG = new GameObject("Background", typeof(RectTransform));
            sliderBG.transform.SetParent(sliderObj.transform, false);
            UnityEngine.UI.Image bgImg = sliderBG.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform bgRT = sliderBG.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
            bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

            // Slider Fill Area & Fill
            GameObject area = new GameObject("Fill Area", typeof(RectTransform));
            area.transform.SetParent(sliderObj.transform, false);
            RectTransform areaRT = area.GetComponent<RectTransform>();
            areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one;
            areaRT.offsetMin = Vector2.zero; areaRT.offsetMax = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform));
            fill.transform.SetParent(area.transform, false);
            UnityEngine.UI.Image fillImg = fill.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = Color.cyan;
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = Vector2.one;
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;

            progressBar.fillRect = fillRT;
            progressBar.targetGraphic = fillImg;

            overlayRoot.SetActive(false);
        }
    }
}
