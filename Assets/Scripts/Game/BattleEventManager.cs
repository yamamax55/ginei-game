using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 会戦イベントの配線（#2179）。既存の `EventEngine`/`GameEventDef`/`EventRules`（#116）を会戦に駆動する。
    /// 一定間隔で会戦用イベントを抽選発火し、選択モーダル（ポーズ）で提示、選んだ効果を実艦隊へ適用する。
    /// Battle シーンに自動生成。効果はプレイヤー勢力の旗艦の士気へ反映（毎回違う事件でリプレイ性）。
    /// </summary>
    public class BattleEventManager : MonoBehaviour
    {
        [Tooltip("イベント抽選の間隔（秒・game-time）")]
        public float tickInterval = 25f;

        private readonly EventEngine engine = new EventEngine();
        private float nextTick;
        private bool modalOpen;
        private float savedTimeScale = 1f;

        // モーダルUI
        private GameObject modalRoot;
        private TextMeshProUGUI titleText, bodyText;
        private Transform buttonRow;
        private GameObject buttonTemplate;

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
            if (scene.name != "Battle") return;
            // 攻城/システムビューなど特殊モードでは出さない。
            if (BattleHandoff.IsPlanetSiege || BattleHandoff.IsSystemView) return;
            if (FindAnyObjectByType<BattleEventManager>() != null) return;
            new GameObject("BattleEventManager").AddComponent<BattleEventManager>();
        }

        private void Start()
        {
            RegisterEvents();
            nextTick = Time.time + tickInterval;
        }

        private Faction Player => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

        private void RegisterEvents()
        {
            // 義勇兵の志願（好機）：受ければ士気↑。
            engine.Register(new GameEventDef("battle_volunteers", "義勇兵の志願",
                    "近隣の義勇兵が前線への参加を志願している。")
                .AddChoice("受け入れる（士気↑）", ctx => AdjustPlayerMorale(8f))
                .AddChoice("断る", null));

            // 補給線の不安（ジレンマ）：慎重＝安全だが士気↓、強攻＝活気づくが…。
            engine.Register(new GameEventDef("battle_supply", "補給線に不安",
                    "弾薬の補給に遅れが出ている。どう戦う？")
                .AddChoice("慎重に戦う（士気↓・安全）", ctx => AdjustPlayerMorale(-5f))
                .AddChoice("強攻して勢いをつける（士気↑）", ctx => AdjustPlayerMorale(7f)));

            // 英雄的奮戦（通知＝確認のみ）：自動で士気↑。
            engine.Register(new GameEventDef("battle_heroics", "英雄的奮戦",
                    "一隊の奮戦が全軍を奮い立たせた！")
                .AddChoice("士気高まる", ctx => AdjustPlayerMorale(6f)));
        }

        private void Update()
        {
            if (modalOpen) return;
            if (Time.time < nextTick) return;
            nextTick = Time.time + Mathf.Max(5f, tickInterval);

            var ctx = new EventContext(Player);
            GameEventDef fired = engine.Tick(ctx, Time.time, Random.value);
            if (fired != null) OpenModal(fired, ctx);
        }

        /// <summary>プレイヤー勢力の生存旗艦の士気を一律に増減する。</summary>
        private void AdjustPlayerMorale(float delta)
        {
            Faction player = Player;
            IReadOnlyList<FleetStrength> flags = FleetRegistry.AllFlagships;
            for (int i = 0; i < flags.Count; i++)
            {
                FleetStrength f = flags[i];
                if (f == null || !f.IsAlive || f.faction != player) continue;
                FleetMorale mo = f.GetComponent<FleetMorale>();
                if (mo != null) mo.ApplyMoraleDelta(delta);
            }
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                delta >= 0 ? $"会戦イベント：味方の士気が上がった（+{delta:0}）" : $"会戦イベント：味方の士気が下がった（{delta:0}）");
        }

        // ===== モーダルUI（uGUI・実行時生成） =====

        private void OpenModal(GameEventDef def, EventContext ctx)
        {
            if (modalRoot == null) BuildModal();
            modalOpen = true;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f; // ポーズ

            titleText.text = def.title;
            bodyText.text = def.body;

            // 既存の選択ボタンを除去してから生成。
            for (int i = buttonRow.childCount - 1; i >= 0; i--)
            {
                GameObject c = buttonRow.GetChild(i).gameObject;
                if (c != buttonTemplate) Destroy(c);
            }
            for (int i = 0; i < def.choices.Count; i++)
            {
                int idx = i;
                GameObject b = Instantiate(buttonTemplate, buttonRow);
                b.SetActive(true);
                var t = b.GetComponentInChildren<TextMeshProUGUI>();
                if (t != null) t.text = def.choices[idx].label;
                var btn = b.GetComponent<Button>();
                if (btn != null) btn.onClick.AddListener(() => Choose(idx, ctx));
            }

            modalRoot.SetActive(true);
        }

        private void Choose(int index, EventContext ctx)
        {
            engine.Resolve(index, ctx);
            CloseModal();
        }

        private void CloseModal()
        {
            modalOpen = false;
            Time.timeScale = savedTimeScale;
            if (modalRoot != null) modalRoot.SetActive(false);
        }

        private void BuildModal()
        {
            var canvasGo = new GameObject("BattleEventCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasGo.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            modalRoot = canvasGo;

            // 全画面ディマー。
            var dim = NewImage("Dimmer", canvasGo.transform, new Color(0f, 0f, 0f, 0.6f));
            Stretch(dim.rectTransform);

            // パネル。
            var panel = NewImage("Panel", canvasGo.transform, new Color(0.12f, 0.12f, 0.16f, 0.97f));
            var prt = panel.rectTransform;
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = new Vector2(640f, 320f);

            titleText = NewText("Title", panel.transform, 36, new Vector2(0f, 110f), new Vector2(600f, 50f));
            titleText.color = new Color(1f, 0.9f, 0.5f);
            bodyText = NewText("Body", panel.transform, 26, new Vector2(0f, 20f), new Vector2(580f, 120f));

            var rowGo = new GameObject("Buttons");
            rowGo.transform.SetParent(panel.transform, false);
            var rrt = rowGo.AddComponent<RectTransform>();
            rrt.anchorMin = rrt.anchorMax = new Vector2(0.5f, 0f);
            rrt.pivot = new Vector2(0.5f, 0f);
            rrt.anchoredPosition = new Vector2(0f, 24f);
            rrt.sizeDelta = new Vector2(600f, 60f);
            var hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
            buttonRow = rowGo.transform;

            buttonTemplate = BuildButtonTemplate(rowGo.transform);
            modalRoot.SetActive(false);
        }

        private GameObject BuildButtonTemplate(Transform parent)
        {
            var go = NewImage("ChoiceBtn", parent, new Color(0.25f, 0.3f, 0.4f, 1f)).gameObject;
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 180f; le.minHeight = 48f;
            go.AddComponent<Button>();
            var label = NewText("Label", go.transform, 22, Vector2.zero, new Vector2(180f, 48f));
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            go.SetActive(false);
            return go;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI NewText(string name, Transform parent, float size, Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            if (t.font == null) t.font = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = sizeDelta;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }
    }
}
