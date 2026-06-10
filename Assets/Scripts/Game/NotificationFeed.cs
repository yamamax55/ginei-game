using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 画面左下のメッセージフィード（#964 NOTIF-2）。<see cref="NotificationCenter"/> の新着をトーストで縦積み表示し、
    /// 実時間（unscaled）で自動フェード→破棄する。Strategy/Battle 両シーンへ自動生成（TimeDisplay と同様の uGUI HUD）。
    /// 生成時に <see cref="NotificationCenter.LastSeq"/> を既読にして履歴フラッドを防ぐ。クリックスルー（raycastTarget=false）。
    /// </summary>
    public class NotificationFeed : MonoBehaviour
    {
        [Tooltip("トーストの表示秒数（実時間）")]
        public float toastDuration = 6f;
        [Tooltip("末尾のフェード秒数")]
        public float fadeTime = 1.2f;
        [Tooltip("同時表示の最大数（超過は古いものから消す）")]
        public int maxToasts = 7;

        private long lastSeq;
        private RectTransform container;
        private TMP_FontAsset jpFont;

        private class Toast { public GameObject go; public CanvasGroup cg; public float age; }
        private readonly List<Toast> toasts = new List<Toast>();

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
            if (UnityEngine.Object.FindAnyObjectByType<NotificationFeed>() != null) return;
            new GameObject("NotificationFeed").AddComponent<NotificationFeed>();
        }

        private void Awake()
        {
            lastSeq = NotificationCenter.LastSeq; // 生成以降の新着だけ出す（履歴フラッド防止）
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            BuildUI();
        }

        private void Update()
        {
            // 新着をトースト化（古い順に Since で取得）
            var fresh = NotificationCenter.Since(lastSeq);
            for (int i = 0; i < fresh.Count; i++)
            {
                SpawnToast(fresh[i]);
                lastSeq = fresh[i].seq;
            }

            // 上限を超えたら古いものから除去
            while (toasts.Count > maxToasts) RemoveToast(0);

            // フェード進行（実時間）。破棄しながら逆順走査。
            float dt = Time.unscaledDeltaTime;
            for (int i = toasts.Count - 1; i >= 0; i--)
            {
                Toast t = toasts[i];
                t.age += dt;
                if (t.age >= toastDuration) { RemoveToast(i); continue; }
                float fadeStart = Mathf.Max(0f, toastDuration - fadeTime);
                t.cg.alpha = t.age <= fadeStart ? 1f : Mathf.InverseLerp(toastDuration, fadeStart, t.age);
            }
        }

        private void SpawnToast(Notification n)
        {
            var go = new GameObject("Toast");
            go.transform.SetParent(container, false);
            var cg = go.AddComponent<CanvasGroup>();
            cg.interactable = false; cg.blocksRaycasts = false;

            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = $"▸ {n.message}";
            label.fontSize = 20f;
            label.color = SeverityColor(n.severity);
            label.raycastTarget = false;
            label.alignment = TextAlignmentOptions.BottomLeft;
            label.enableWordWrapping = true;
            if (jpFont != null) label.font = jpFont;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 520f;

            go.transform.SetAsLastSibling(); // 新しいものが下
            toasts.Add(new Toast { go = go, cg = cg, age = 0f });
        }

        private void RemoveToast(int index)
        {
            if (index < 0 || index >= toasts.Count) return;
            if (toasts[index].go != null) Destroy(toasts[index].go);
            toasts.RemoveAt(index);
        }

        private static Color SeverityColor(NotificationSeverity s)
        {
            switch (s)
            {
                case NotificationSeverity.警告: return new Color(1f, 0.5f, 0.4f);
                case NotificationSeverity.注意: return new Color(1f, 0.85f, 0.4f);
                default: return new Color(0.9f, 0.93f, 0.96f);
            }
        }

        private void BuildUI()
        {
            var canvasObj = new GameObject("NotificationCanvas");
            canvasObj.transform.SetParent(transform);
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 880; // ゲームUIより前・モーダル(900+)より後ろ
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasObj.AddComponent<GraphicRaycaster>();

            var cont = new GameObject("FeedContainer");
            cont.transform.SetParent(canvasObj.transform, false);
            container = cont.AddComponent<RectTransform>();
            container.anchorMin = new Vector2(0f, 0f); // 左下
            container.anchorMax = new Vector2(0f, 0f);
            container.pivot = new Vector2(0f, 0f);
            container.anchoredPosition = new Vector2(16f, 16f);
            container.sizeDelta = new Vector2(540f, 0f);

            var vlg = cont.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4f;
            vlg.childAlignment = TextAnchor.LowerLeft;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = false; vlg.childForceExpandHeight = false;
            var fitter = cont.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize; // 下端アンカーから上へ伸びる
        }
    }
}
