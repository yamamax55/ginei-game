using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Ginei
{
    /// <summary>
    /// 箱の受信箱（MEYASU-7 #1303）。Alt+I で開く UI Toolkit モーダル。
    /// 建白・諮問を箱/信認/状態つきで一覧し、伝播履歴を確認して共通 DecisionDeck 経路から裁可・却下する。
    /// 下段の建白フォームは登録済みの WHAT を選び、任意の WHY を添える。結果確率は数値で確約しない。
    /// </summary>
    public class RingiObserverOverlay : MonoBehaviour
    {
        private sealed class InboxRow
        {
            public Petition petition;
            public PendingDecision decision;
        }

        private static RingiObserverOverlay instance;
        public static bool IsOpen => instance != null && instance.isOpen;
        public static RingiObserverOverlay InstanceForTest => instance;

        public int canvasSortingOrder = 1114;
        private bool isOpen;
        private float savedTimeScale = 1f;
        private bool pausedClock;
        private VisualElement root;
        private ScrollView rows;
        private Label hint;
        private Label detail;
        private DropdownField proposalChoice;
        private TextField proposalWhy;
        private object escWindowToken;
        private string lastSignature = "";

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
            if (UnityEngine.Object.FindAnyObjectByType<RingiObserverOverlay>() != null) return;
            new GameObject("RingiInboxPanel").AddComponent<RingiObserverOverlay>();
        }

        private void Awake()
        {
            instance = this;
            Build();
            escWindowToken = UIWindowStack.Register(() => isOpen, Close, canvasSortingOrder, "箱の受信箱");
        }

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.稟議観測切替)) Toggle();
            if (!isOpen) return;
            Time.timeScale = 0f;
            string sig = Signature();
            if (sig != lastSignature) { Rebuild(); lastSignature = sig; }
        }

        public void Toggle() { if (isOpen) Close(); else Open(); }
        public void SetVisible(bool visible) { if (visible) Open(); else Close(); }

        private void Open()
        {
            if (root == null || isOpen) return;
            savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            GameClock clock = StrategySession.Clock;
            if (clock != null && !clock.paused) { clock.Pause(); pausedClock = true; }
            isOpen = true;
            root.style.display = DisplayStyle.Flex;
            lastSignature = "";
            Rebuild();
        }

        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;
            if (root != null) root.style.display = DisplayStyle.None;
            Time.timeScale = savedTimeScale;
            GameClock clock = StrategySession.Clock;
            if (clock != null && pausedClock) { clock.Resume(); pausedClock = false; }
        }

        private void Build()
        {
            GineiUITK.Attach(gameObject, canvasSortingOrder, out root);
            if (root == null) return;

            var dim = new VisualElement();
            dim.AddToClassList("dim");
            dim.RegisterCallback<ClickEvent>(evt => { if (evt.target == dim) Close(); });
            root.Add(dim);

            var panel = new VisualElement();
            panel.AddToClassList("panel");
            dim.Add(panel);
            var title = new Label("箱の受信箱 ― 建白と諮問");
            title.AddToClassList("title");
            panel.Add(title);

            hint = new Label("箱への信認は通りやすさの目安です。結果と代償は裁可後に確定します。");
            hint.AddToClassList("hint");
            panel.Add(hint);

            rows = new ScrollView(ScrollViewMode.Vertical);
            rows.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            rows.AddToClassList("scroll");
            rows.style.flexGrow = 1f;
            panel.Add(rows);

            detail = new Label("行の［詳細］で、誰の手を経たかを確認できます。");
            detail.style.whiteSpace = WhiteSpace.Normal;
            detail.style.minHeight = 72f;
            panel.Add(detail);

            var formTitle = new Label("建白フォーム（宛先の箱・WHATを選び、WHYを添える）");
            formTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            panel.Add(formTitle);
            var choices = new List<string>();
            for (int i = 0; i < RingiSampleData.Count; i++)
            {
                RingiSample s = RingiSampleData.At(i);
                choices.Add($"{s.box}箱｜{s.title}");
            }
            proposalChoice = new DropdownField("誰の箱に・何を", choices, choices.Count > 0 ? 0 : -1);
            panel.Add(proposalChoice);
            proposalWhy = new TextField("どう書くか（任意）") { multiline = true };
            proposalWhy.style.minHeight = 54f;
            panel.Add(proposalWhy);
            panel.Add(new Button(SubmitProposal) { text = "この内容で建白する" });
            panel.Add(new Button(Close) { text = "閉じる (Alt+I / Esc)" });

            root.style.display = DisplayStyle.None;
        }

        private void Rebuild()
        {
            if (rows == null) return;
            rows.Clear();
            List<InboxRow> inbox = GatherRows();
            if (inbox.Count == 0)
            {
                rows.Add(new Label("現在、進行中の建白・諮問はありません。"));
                return;
            }

            inbox.Sort((a, b) =>
            {
                int c = ((int)b.petition.severity).CompareTo((int)a.petition.severity);
                return c != 0 ? c : a.petition.id.CompareTo(b.petition.id);
            });
            for (int i = 0; i < inbox.Count; i++) AddInboxRow(inbox[i]);
        }

        private void AddInboxRow(InboxRow row)
        {
            Petition p = row.petition;
            var line = new VisualElement();
            line.AddToClassList("row");
            line.style.flexDirection = FlexDirection.Row;
            string direction = p.origin == PetitionOrigin.諮問 ? "↓諮問" : "↑建白";
            var label = new Label($"{direction}　{p.box}箱　{CredibilityText(p)}　【{p.status}】 {p.title}");
            label.AddToClassList("row-label");
            line.Add(label);
            line.Add(new Button(() => ShowDetail(row)) { text = "詳細" });

            if (row.decision != null && !DecisionResolutionRules.IsSettled(row.decision))
            {
                string yes = p.origin == PetitionOrigin.諮問 ? "裁可" : "汲む";
                string no = p.origin == PetitionOrigin.諮問 ? "却下" : "黙殺";
                line.Add(new Button(() => Resolve(row, 0, yes)) { text = yes });
                line.Add(new Button(() => Resolve(row, 1, no)) { text = no });
            }
            rows.Add(line);
        }

        private void Resolve(InboxRow row, int choice, string verb)
        {
            if (row == null || row.decision == null) return;
            bool ok = DecisionDeck.Resolve(row.decision.id, choice);
            hint.text = ok ? $"{row.petition.title}：{verb}を決裁経路へ渡しました。" :
                $"{row.petition.title}：決裁できません。権限・状態・効果登録を確認してください。";
            lastSignature = "";
            Rebuild();
        }

        private void ShowDetail(InboxRow row)
        {
            Petition p = row.petition;
            var sb = new StringBuilder();
            sb.Append(p.title).Append("\n");
            sb.Append("出自: ").Append(p.origin).Append("　宛先: ").Append(p.box).Append("箱")
              .Append("　信認: ").Append(CredibilityText(p)).Append("\n");
            sb.Append("起案: ").Append(ResolveNameOrId(p.drafterId)).Append("　決裁: ")
              .Append(p.addresseeId == 0 ? p.box + "箱" : ResolveNameOrId(p.addresseeId)).Append("\n");
            sb.Append("伝播履歴: ");
            if (p.hops == null || p.hops.Count == 0) sb.Append("（中継なし）");
            else for (int i = 0; i < p.hops.Count; i++)
            {
                if (i > 0) sb.Append(" → ");
                sb.Append(ResolveNameOrId(p.hops[i]));
            }
            if (p.distorted) sb.Append("\n※ 伝播中に内容が歪められています");
            if (row.decision != null) sb.Append("\n\n").Append(RingiNarrativeRuntime.TextFor(row.decision));
            detail.text = sb.ToString();
        }

        private void SubmitProposal()
        {
            RingiDirector director = UnityEngine.Object.FindAnyObjectByType<RingiDirector>();
            if (director == null || proposalChoice == null || proposalChoice.index < 0)
            {
                hint.text = "稟議機構が利用できません。";
                return;
            }
            int id = director.SubmitPlayerPetition(proposalChoice.index, proposalWhy != null ? proposalWhy.value : "");
            hint.text = id >= 0 ? "建白が箱へ届き、決裁待ちになりました。" :
                "建白は官僚機構で止まりました。結果は信認・摩擦・正統性で変わります。";
            if (id >= 0 && proposalWhy != null) proposalWhy.value = "";
            lastSignature = "";
            Rebuild();
        }

        private static List<InboxRow> GatherRows()
        {
            var result = new List<InboxRow>();
            AppendLedger(result, RingiDirector.Ledger);
            AppendLedger(result, FleetRingiDirector.Ledger);
            return result;
        }

        private static void AppendLedger(List<InboxRow> result, PetitionLedger ledger)
        {
            if (ledger == null) return;
            List<Petition> active = ledger.Active();
            for (int i = 0; i < active.Count; i++)
            {
                Petition p = active[i];
                if (p != null) result.Add(new InboxRow { petition = p, decision = FindDecision(p) });
            }
        }

        private static PendingDecision FindDecision(Petition p)
        {
            DecisionQueue q = DecisionDeck.Queue;
            if (p == null || q == null) return null;
            for (int i = 0; i < q.items.Count; i++)
            {
                PendingDecision d = q.items[i];
                if (d != null && d.petitionId == p.id && string.Equals(d.effectKey, p.effectKey, StringComparison.Ordinal))
                    return d;
            }
            return null;
        }

        private static string CredibilityText(Petition p)
        {
            FactionState fs = CampaignRules.GetState(StrategySession.Campaign, p.faction);
            float value = CredibilityRules.Heed(fs != null ? fs.credibility : null, p.box, p.regionKey);
            string word = value >= 0.65f ? "顔が利く" : value >= 0.35f ? "中立" : "警戒";
            return $"{word} {Mathf.RoundToInt(value * 100f)}";
        }

        private static string ResolveNameOrId(int id)
        {
            if (id == 0) return "匿名/制度発議";
            GalaxyView gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            string name = Find(id, gv != null ? gv.CommanderRoster : null) ?? Find(id, gv != null ? gv.CivilianRoster : null);
            return name ?? $"人物#{id}";
        }

        private static string Find(int id, IReadOnlyList<Person> roster)
        {
            if (roster == null) return null;
            for (int i = 0; i < roster.Count; i++)
                if (roster[i] != null && roster[i].id == id) return roster[i].name;
            return null;
        }

        private static string Signature()
        {
            var sb = new StringBuilder();
            AppendSignature(sb, RingiDirector.Ledger);
            AppendSignature(sb, FleetRingiDirector.Ledger);
            return sb.ToString();
        }

        private static void AppendSignature(StringBuilder sb, PetitionLedger ledger)
        {
            if (ledger == null) return;
            for (int i = 0; i < ledger.items.Count; i++)
            {
                Petition p = ledger.items[i];
                if (p != null) sb.Append(p.id).Append(':').Append((int)p.status).Append(':').Append(p.hops.Count).Append('|');
            }
        }

        private void OnDestroy()
        {
            UIWindowStack.Unregister(escWindowToken);
            if (isOpen) Close();
            if (instance == this) instance = null;
        }

        // PlayMode QA: UI Toolkit の入力配送を再実装せず、実画面の構築・停止・一覧・フォーム・Esc 配線を確認する。
        public void OpenForTest() => Open();
        public int InboxRowCountForTest => rows != null ? rows.childCount : 0;
        public bool ScrollbarVisibleForTest => rows != null && rows.verticalScrollerVisibility == ScrollerVisibility.AlwaysVisible;
        public bool ProposalFormExistsForTest => proposalChoice != null && proposalWhy != null;
        public bool EscRegisteredForTest => escWindowToken != null;
    }
}
