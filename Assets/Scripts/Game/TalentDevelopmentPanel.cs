using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Ginei
{
    /// <summary>
    /// 教育・人材育成の操作画面。評価、成長曲線、投資、現役教育、艦隊訓練、軍団演習を
    /// <see cref="GalaxyView"/> の共通入口へ接続し、画面自身は状態を直接変更しない。
    /// </summary>
    public class TalentDevelopmentPanel : MonoBehaviour
    {
        public int canvasSortingOrder = 1093;
        public float panelWidth = 1120f;
        public float panelHeight = 920f;
        public float refreshInterval = 1.2f;

        private static TalentDevelopmentPanel instance;
        private Canvas canvas;
        private RectTransform content;
        private TextMeshProUGUI statusLabel;
        private TMP_FontAsset jpFont;
        private object escWindowToken;
        private float refreshTimer;
        private string message = "";
        private readonly List<GameObject> rows = new List<GameObject>();

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
            if (scene.name != "Strategy") return;
            if (FindAnyObjectByType<TalentDevelopmentPanel>() != null) return;
            new GameObject("TalentDevelopmentPanel").AddComponent<TalentDevelopmentPanel>();
        }

        public static bool IsOpen => instance != null && instance.canvas != null && instance.canvas.gameObject.activeSelf;

        public static void Show()
        {
            TalentDevelopmentPanel panel = EnsureInstance();
            if (panel != null) panel.Open();
        }

        public static void Hide()
        {
            if (instance != null) instance.Close();
        }

        public static void Toggle()
        {
            if (IsOpen) Hide(); else Show();
        }

        private static TalentDevelopmentPanel EnsureInstance()
        {
            if (instance != null) return instance;
            instance = FindAnyObjectByType<TalentDevelopmentPanel>();
            if (instance == null) instance = new GameObject("TalentDevelopmentPanel").AddComponent<TalentDevelopmentPanel>();
            return instance;
        }

        private static GalaxyView View => GalaxyView.Active;
        private static Faction PlayerFaction => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

        private void Awake()
        {
            instance = this;
            jpFont = Resources.Load<TMP_FontAsset>("JapaneseFont_TMP");
            BuildUI();
            canvas.gameObject.SetActive(false);
            escWindowToken = UIWindowStack.Register(() => IsOpen, Close, canvasSortingOrder, "教育・人材育成");
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (!IsOpen) return;
            refreshTimer += Time.unscaledDeltaTime;
            if (refreshTimer < Mathf.Max(0.1f, refreshInterval)) return;
            refreshTimer = 0f;
            Rebuild();
        }

        private void Open()
        {
            canvas.gameObject.SetActive(true);
            message = "候補者の評価と訓練状況を表示しています";
            Rebuild();
        }

        private void Close() => canvas.gameObject.SetActive(false);

        private void Rebuild()
        {
            for (int i = 0; i < rows.Count; i++) if (rows[i] != null) Destroy(rows[i]);
            rows.Clear();
            statusLabel.text = message;

            GalaxyView view = View;
            if (view == null)
            {
                AddNotice("戦略マップでのみ育成を操作できます。");
                return;
            }

            Faction faction = PlayerFaction;
            TalentDevelopmentState state = view.RefreshTalentDevelopmentProfiles(faction);
            if (state == null)
            {
                AddNotice("勢力の育成台帳がありません。");
                return;
            }

            AddHeader("個人育成　— 評価・成長曲線・育成投資");
            int people = 0;
            for (int i = 0; i < state.people.Count; i++)
            {
                TalentDevelopmentProfile profile = state.people[i];
                Person person = profile != null ? view.FindDevelopmentPerson(profile.personId) : null;
                if (person == null || person.faction != faction || person.deathYear > 0) continue;
                people++;
                AddPersonRow(view, person, profile);
            }
            if (people == 0) AddNotice("評価対象の人物がいません。");

            AddHeader("艦隊訓練　— 出撃すると中断し、即応性が回復します");
            List<StrategicFleet> fleets = PlayerFleets(view, faction);
            if (fleets.Count == 0) AddNotice("訓練できる艦隊がありません。");
            for (int i = 0; i < fleets.Count; i++) AddFleetRow(view, state, fleets[i]);

            AddHeader("軍団共同演習　— 隷下艦隊が揃っている軍団が対象です");
            List<CorpsAssignmentRules.CorpsInfo> corps = CorpsAssignmentRules.CorpsOf(fleets, faction);
            if (corps.Count == 0) AddNotice("編成済みの軍団がありません。");
            for (int i = 0; i < corps.Count; i++) AddCorpsRow(view, state, faction, corps[i]);

            AddHeader("実施中・中断中の課程");
            int programs = 0;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram program = state.programs[i];
                if (program == null || (program.status != DevelopmentProgramStatus.実施中 &&
                    program.status != DevelopmentProgramStatus.中断)) continue;
                programs++;
                AddProgramRow(view, faction, program);
            }
            if (programs == 0) AddNotice("進行中の育成・訓練はありません。");
        }

        private void AddPersonRow(GalaxyView view, Person person, TalentDevelopmentProfile profile)
        {
            string box = PerformanceReviewRules.Box(profile.performance, profile.potential).ToString();
            string growth = profile.growth != null
                ? $"{profile.growth.archetype} 経験{profile.growth.experience:0.0} 成長+{GrowthRules.EffectiveStatBonus(profile.growth, Mathf.RoundToInt(Mathf.Max(person.MilitaryAptitude, Mathf.Max(person.CivilAptitude, person.TechnicalAptitude)))):0.0}"
                : "成長記録なし";
            DevelopmentProgramKind recommended = TalentDevelopmentRules.RecommendedProgram(profile);
            if (recommended == DevelopmentProgramKind.現役士官教育 && person.role != PersonRole.軍人)
                recommended = DevelopmentProgramKind.抜擢配置;
            string text = $"{person.name}　{person.role}　{box}\n実績 {profile.performance:0.00}／潜在 {profile.potential:0.00}　{growth}　累計投資 {profile.totalInvestment:0.0}";
            bool eligible = TalentDevelopmentRules.IsInvestmentCandidate(profile) && !HasOpenPersonProgram(view.TalentDevelopmentOf(person.faction), person.id);
            AddActionRow(text, eligible ? $"{recommended}を開始" : "投資対象外・実施中", eligible, () =>
            {
                if (view.StartRecommendedDevelopment(person.id, out string reason)) message = $"{person.name}：{reason}";
                else message = $"{person.name}：{reason}";
                Rebuild();
            });
        }

        private void AddFleetRow(GalaxyView view, TalentDevelopmentState state, StrategicFleet fleet)
        {
            float readiness = TalentDevelopmentRules.EffectiveFleetReadiness(state, fleet.id, 1f);
            FleetTrainingProfile profile = FindFleetProfile(state, fleet.id);
            bool active = HasOpenFleetProgram(state, fleet.id);
            string text = $"第{fleet.id}艦隊　{FleetCommandLabelRules.CorpsLabel(fleet)}　即応性 {readiness * 100f:0}%　練度 {(profile != null ? profile.proficiency : 0f) * 100f:0}%";
            bool canStart = !active && !fleet.IsMoving && !fleet.engaged;
            AddActionRow(text, active ? "訓練中・中断中" : "艦隊訓練を開始", canStart, () =>
            {
                view.StartFleetTraining(fleet.id, out string reason);
                message = $"第{fleet.id}艦隊：{reason}";
                Rebuild();
            });
        }

        private void AddCorpsRow(GalaxyView view, TalentDevelopmentState state, Faction faction, CorpsAssignmentRules.CorpsInfo corps)
        {
            float coordination = TalentDevelopmentRules.EffectiveCorpsCoordination(state, corps.corpsId, 0f);
            bool active = HasOpenCorpsProgram(state, corps.corpsId);
            string name = string.IsNullOrEmpty(corps.corpsName) ? $"軍団#{corps.corpsId}" : corps.corpsName;
            string text = $"{name}　{corps.fleetCount}個艦隊　連携 {coordination * 100f:0}%";
            AddActionRow(text, active ? "演習中・中断中" : "共同演習を開始", !active, () =>
            {
                view.StartCorpsExercise(faction, corps.corpsId, out string reason);
                message = $"{name}：{reason}";
                Rebuild();
            });
        }

        private void AddProgramRow(GalaxyView view, Faction faction, DevelopmentProgram program)
        {
            string subject = SubjectName(view, program);
            string text = $"#{program.id} {program.kind}　{subject}　{program.status}　{program.progressYears}/{program.durationYears}年　即応性 -{program.readinessPenalty * 100f:0}%";
            bool interrupted = program.status == DevelopmentProgramStatus.中断;
            AddActionRow(text, interrupted ? "再開" : "中断", true, () =>
            {
                bool ok = interrupted
                    ? view.ResumeDevelopment(faction, program.id, out string reason)
                    : view.InterruptDevelopment(faction, program.id, "プレイヤー指示", out reason);
                message = ok ? reason : $"操作失敗：{reason}";
                Rebuild();
            });
        }

        private static string SubjectName(GalaxyView view, DevelopmentProgram program)
        {
            if (program.personId >= 0)
            {
                Person person = view.FindDevelopmentPerson(program.personId);
                return person != null ? person.name : $"人物#{program.personId}";
            }
            if (program.fleetId >= 0) return $"第{program.fleetId}艦隊";
            return $"軍団#{program.corpsId}";
        }

        private static List<StrategicFleet> PlayerFleets(GalaxyView view, Faction faction)
        {
            var result = new List<StrategicFleet>();
            StrategicFleetRegistry registry = view != null ? view.Registry : null;
            if (registry == null || registry.fleets == null) return result;
            for (int i = 0; i < registry.fleets.Count; i++)
            {
                StrategicFleet fleet = registry.fleets[i];
                if (fleet != null && fleet.faction == faction) result.Add(fleet);
            }
            result.Sort((a, b) => a.id.CompareTo(b.id));
            return result;
        }

        private static bool HasOpenPersonProgram(TalentDevelopmentState state, int id)
            => HasOpenProgram(state, p => p.personId == id);
        private static bool HasOpenFleetProgram(TalentDevelopmentState state, int id)
            => HasOpenProgram(state, p => p.fleetId == id);
        private static bool HasOpenCorpsProgram(TalentDevelopmentState state, int id)
            => HasOpenProgram(state, p => p.corpsId == id);

        private static bool HasOpenProgram(TalentDevelopmentState state, System.Predicate<DevelopmentProgram> match)
        {
            if (state == null || state.programs == null) return false;
            for (int i = 0; i < state.programs.Count; i++)
            {
                DevelopmentProgram p = state.programs[i];
                if (p != null && (p.status == DevelopmentProgramStatus.実施中 || p.status == DevelopmentProgramStatus.中断) && match(p)) return true;
            }
            return false;
        }

        private static FleetTrainingProfile FindFleetProfile(TalentDevelopmentState state, int fleetId)
        {
            if (state == null || state.fleets == null) return null;
            for (int i = 0; i < state.fleets.Count; i++)
                if (state.fleets[i] != null && state.fleets[i].fleetId == fleetId) return state.fleets[i];
            return null;
        }

        private void BuildUI()
        {
            EnsureEventSystem();
            GameObject root = new GameObject("TalentDevelopmentCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject frame = new GameObject("Frame", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            frame.transform.SetParent(root.transform, false);
            RectTransform frameRT = frame.GetComponent<RectTransform>();
            frameRT.anchorMin = frameRT.anchorMax = new Vector2(0.5f, 0.5f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelHeight);
            frame.GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.085f, 0.98f);
            VerticalLayoutGroup layout = frame.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            WindowChrome.AddTitleBarLayout(frameRT, "教育・人材育成", Close);

            statusLabel = AddText(frame.transform, "Status", "", 17f, new Color(0.95f, 0.82f, 0.36f));
            statusLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(LayoutElement));
            scrollObject.transform.SetParent(frame.transform, false);
            scrollObject.GetComponent<LayoutElement>().flexibleHeight = 1f;
            ScrollRect scroll = scrollObject.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 32f;

            GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scrollObject.transform, false);
            RectTransform viewportRT = viewport.GetComponent<RectTransform>();
            viewportRT.anchorMin = Vector2.zero; viewportRT.anchorMax = Vector2.one; viewportRT.sizeDelta = Vector2.zero;
            viewport.GetComponent<Image>().color = Color.clear;
            scroll.viewport = viewportRT;

            GameObject contentObject = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentObject.transform.SetParent(viewport.transform, false);
            content = contentObject.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f); content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero;
            VerticalLayoutGroup contentLayout = contentObject.GetComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 5f; contentLayout.childControlWidth = true; contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true; contentLayout.childForceExpandHeight = false;
            contentObject.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            UiScrollbars.Attach(scroll);
        }

        private void AddHeader(string text)
        {
            TextMeshProUGUI label = AddText(content, "Header", text, 20f, new Color(0.98f, 0.78f, 0.28f));
            label.fontStyle = FontStyles.Bold;
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
            rows.Add(label.gameObject);
        }

        private void AddNotice(string text)
        {
            TextMeshProUGUI label = AddText(content, "Notice", text, 16f, new Color(0.7f, 0.76f, 0.84f));
            label.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
            rows.Add(label.gameObject);
        }

        private void AddActionRow(string text, string action, bool interactable, UnityEngine.Events.UnityAction onClick)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            row.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.17f, 0.96f);
            row.GetComponent<LayoutElement>().preferredHeight = text.Contains("\n") ? 58f : 40f;
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(10, 8, 5, 5); layout.spacing = 8f;
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = true;
            TextMeshProUGUI label = AddText(row.transform, "Label", text, 15f, new Color(0.9f, 0.93f, 0.98f));
            label.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            GameObject buttonObject = new GameObject("Action", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            buttonObject.transform.SetParent(row.transform, false);
            buttonObject.GetComponent<LayoutElement>().preferredWidth = 190f;
            buttonObject.GetComponent<Image>().color = interactable ? new Color(0.17f, 0.3f, 0.48f) : new Color(0.15f, 0.16f, 0.18f);
            Button button = buttonObject.GetComponent<Button>();
            button.interactable = interactable;
            if (onClick != null) button.onClick.AddListener(onClick);
            TextMeshProUGUI caption = AddText(buttonObject.transform, "Caption", action, 15f, interactable ? Color.white : new Color(0.55f, 0.58f, 0.62f));
            RectTransform captionRT = caption.rectTransform;
            captionRT.anchorMin = Vector2.zero; captionRT.anchorMax = Vector2.one; captionRT.sizeDelta = Vector2.zero;
            caption.alignment = TextAlignmentOptions.Center;
            rows.Add(row);
        }

        private TextMeshProUGUI AddText(Transform parent, string name, string text, float size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = color; label.richText = true;
            label.alignment = TextAlignmentOptions.MidlineLeft; label.textWrappingMode = TextWrappingModes.NoWrap;
            if (jpFont != null) label.font = jpFont;
            return label;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>();
        }
    }
}
