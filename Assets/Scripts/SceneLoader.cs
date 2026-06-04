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
            canvasObj.transform.SetParent(transform);
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999; // 最前面
            
            canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasObj.AddComponent<GraphicRaycaster>();

            // 2. Overlay Background
            overlayRoot = new GameObject("Overlay");
            overlayRoot.transform.SetParent(canvasObj.transform);
            RectTransform overlayRT = overlayRoot.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            
            UnityEngine.UI.Image bg = overlayRoot.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.8f);

            // 3. Loading Text
            GameObject textObj = new GameObject("LoadingText");
            textObj.transform.SetParent(overlayRoot.transform);
            loadingText = textObj.AddComponent<TextMeshProUGUI>();
            loadingText.text = "ロード中...";
            loadingText.fontSize = 32;
            loadingText.alignment = TextAlignmentOptions.Center;
            
            // Conventions.md の TMP フォント適用
            TMP_FontAsset jaFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            if (jaFont != null) loadingText.font = jaFont;

            RectTransform textRT = textObj.GetComponent<RectTransform>();
            textRT.anchoredPosition = new Vector2(0, 50);
            textRT.sizeDelta = new Vector2(400, 50);

            // 4. Progress Bar (Slider)
            GameObject sliderObj = new GameObject("ProgressBar");
            sliderObj.transform.SetParent(overlayRoot.transform);
            progressBar = sliderObj.AddComponent<UnityEngine.UI.Slider>();
            progressBar.interactable = false;

            RectTransform sliderRT = sliderObj.GetComponent<RectTransform>();
            sliderRT.sizeDelta = new Vector2(400, 20);
            sliderRT.anchoredPosition = new Vector2(0, -20);

            // Slider Background
            GameObject sliderBG = new GameObject("Background");
            sliderBG.transform.SetParent(sliderObj.transform);
            UnityEngine.UI.Image bgImg = sliderBG.AddComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            RectTransform bgRT = sliderBG.GetComponent<RectTransform>();
            bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one; bgRT.sizeDelta = Vector2.zero;

            // Slider Area & Fill
            GameObject area = new GameObject("Fill Area");
            area.transform.SetParent(sliderObj.transform);
            RectTransform areaRT = area.AddComponent<RectTransform>();
            areaRT.anchorMin = Vector2.zero; areaRT.anchorMax = Vector2.one; areaRT.sizeDelta = new Vector2(-10, -10);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(area.transform);
            UnityEngine.UI.Image fillImg = fill.AddComponent<UnityEngine.UI.Image>();
            fillImg.color = Color.cyan;
            RectTransform fillRT = fill.GetComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;

            progressBar.fillRect = fillRT;
            progressBar.targetGraphic = fillImg;

            overlayRoot.SetActive(false);
        }
    }
}
