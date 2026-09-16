using System.Collections.Generic;
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
    /// 官僚機構オブザーバ（観測層・read-only）。<b>Alt+K</b>で開閉し、勢力ごとに<b>官僚の組織構造</b>＝省庁の
    /// 編制ツリー（太政官 ⊃ 式部省/民部省/大蔵省/兵部省 …＝<see cref="Ministry"/> #158）を<b>入れ子のまま</b>
    /// 全段ダンプする。各省庁ごとに 所掌／配属定員と充足率／省益（縦割り抵抗 <see cref="Ministry.institutionalInterest"/>）／
    /// 行政効率（<see cref="MinistryAdminRules.StaffingEfficiency"/>）と、<b>配属官僚（文民）を名前と文才つきで一覧</b>。
    /// 朝廷の権威（<see cref="CourtAuthority"/>）と名実の乖離（<see cref="RitsuryoFormalizationRules"/>＝官職が
    /// 実効を持つ度合い）も併記する。政府オブザーバ（Alt+G＝要職任命・首班）が「政治任用の人事」を映すのに対し、
    /// こちらは「常勤の官僚機構そのもの」に特化＝省 ⊃ 庁/局の入れ子と配属官僚を最後まで降りて見せる。
    /// <para><b>省内職位（#141）</b>：人事台帳（<see cref="FactionState.civilService"/>）に在任記録があれば、配属官僚の行へ
    /// 職位名（<see cref="CivilServicePostRules.GradeTitle"/>）・在任年・官位・考課を添える。記録の無い旧配属者は
    /// 「台帳未登録」とその理由（政治家・軍人・未移行など）を明示して隠さない。省ごとに段別の在任/定員
    /// （<see cref="CivilServicePostRules.SlotsFor"/>）と承認権者（事務次官級＝首相／局長級以下＝所管大臣・委任された副大臣＝
    /// <see cref="CivilServicePostRules.ApprovalAuthority"/>）の在否を示し、勢力ごとに直近の退任履歴を並べる。</para>
    /// 観測専用ゆえ既存フィールドのみ読む＝<b>状態は変えない</b>（台帳・省庁・内閣・人物のいずれも書き換えない）。
    /// `GovernmentObserverOverlay` と同型の自動生成（Strategy/Battle）。
    /// </summary>
    public class BureaucracyObserverOverlay : MonoBehaviour
    {
        [Header("外観")]
        public int canvasSortingOrder = 1105;
        public float dimAlpha = 0.55f;
        public float panelWidth = 1020f;
        public float panelMaxHeight = 900f;
        public Color panelColor = new Color(0.05f, 0.05f, 0.04f, 0.96f);
        public float bodyFontSize = 20f;

        [Header("省内職位（#141）")]
        [Tooltip("勢力ごとに表示する人事履歴（退職/異動/昇任/降任/解任）の最大件数。超過は件数で示す。")]
        public int historyRows = 5;

        /// <summary>省内職位の調整値（段別定員・在職年・考第）。観測層は読むだけ＝Core の既定値をそのまま使う。</summary>
        private static readonly CivilServicePostParams PostParams = CivilServicePostParams.Default;

        /// <summary>段を並べる順（下から上へ）と短い見出し。</summary>
        private static readonly BureaucratGrade[] GradeOrder =
        {
            BureaucratGrade.一般官僚, BureaucratGrade.課長級, BureaucratGrade.局長級, BureaucratGrade.事務次官級
        };
        private static readonly string[] GradeShort = { "一般", "課長", "局長", "次官" };

        private GameObject overlayRoot;
        private GameObject panel;
        private TextMeshProUGUI bodyLabel;
        private object escWindowToken;

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
            if (Object.FindAnyObjectByType<BureaucracyObserverOverlay>() != null) return;
            new GameObject("BureaucracyObserverOverlay").AddComponent<BureaucracyObserverOverlay>();
        }

        private void Awake()
        {
            BuildUI();
            SetVisible(false);
            escWindowToken = UIWindowStack.Register(() => panel != null && panel.activeSelf, () => SetVisible(false), canvasSortingOrder, "官僚機構");
        }

        private void OnDestroy() => UIWindowStack.Unregister(escWindowToken);

        private void Update()
        {
            if (GameInput.WasPressed(GameAction.官僚機構観測切替))
                Toggle();

            if (panel != null && panel.activeSelf && bodyLabel != null)
                bodyLabel.text = BuildDump();
        }

        public void Toggle() => SetVisible(panel != null && !panel.activeSelf);

        public void SetVisible(bool visible)
        {
            if (panel != null) panel.SetActive(visible);
        }

        // ===== ダンプ本体 =====

        /// <summary>試験用：表示本文をそのまま取り出す（本番と同じ経路・read-only＝状態は変えない）。</summary>
        public string BuildDumpForQa() => BuildDump();

        /// <summary>
        /// 1勢力ぶんの人事の見方（台帳・名簿・暦年）。読むための束＝ここに状態を持たず、Core の窓口へ渡すだけ。
        /// </summary>
        private sealed class CareerContext
        {
            public Faction faction;
            public PoliticsState politics;
            public CivilServiceState ledger;   // null＝人事台帳 未初期化（新規/旧セーブ）
            public List<Person> roster;
            public int year;
        }

        private string BuildDump()
        {
            var sb = new StringBuilder(4096);
            CampaignState c = StrategySession.Campaign;
            GalaxyView gv = GalaxyView.Active;

            sb.Append("<b>官僚機構オブザーバ</b>　省庁ツリー（省⊃庁/局）・配属官僚・省内職位（#141）　(Alt+K で閉じる)\n");
            sb.Append("<color=#5b6b7a>──────────────────────────────────────────────</color>\n");

            // 朝廷の権威（名実の乖離＝官職が実効を持つ度合い）。盤面で唯一の中央権威。
            CourtAuthority court = gv != null ? gv.Court : null;
            if (court != null)
            {
                float a = court.authority;
                RitsuryoPhase phase = RitsuryoFormalizationRules.PhaseOf(a);
                float factor = RitsuryoFormalizationRules.OfficeAuthorityFactor(a);
                sb.Append("<color=#9fb0c0>朝廷の権威</color> ").Append(Bar(a, 12)).Append(' ')
                  .Append((a * 100f).ToString("0")).Append("%　（")
                  .Append(phase).Append("・官職の実効 ").Append((factor * 100f).ToString("0")).Append("%）\n");
            }

            if (c == null || c.states == null || c.states.Count == 0)
            {
                sb.Append("\n<color=#ffcc66>戦役データ（StrategySession.Campaign）がありません。</color>\n");
                sb.Append("戦略マップ（GalaxyView）を起動すると、各勢力の省庁編制と配属官僚がライブ表示されます。");
                return sb.ToString();
            }

            if (gv == null)
            {
                sb.Append("\n<color=#ffcc66>省庁データは戦略マップ（GalaxyView）でのみ表示されます。</color>");
                return sb.ToString();
            }

            List<Person> roster = BuildRoster(gv);
            int year = gv.ElectionYearForQa; // 年次人事が就任年を書くのと同じ暦年（統一クロックの宇宙暦）

            for (int i = 0; i < c.states.Count; i++)
            {
                FactionState s = c.states[i];
                if (s == null) continue;
                AppendFaction(sb, gv, s, roster, year);
            }

            sb.Append("\n<color=#6f8a9a>※ 常勤の官僚機構＝省庁ツリー（二官八省・#158）と配属官僚。行政効率＝配下官僚の平均文才×定員充足率。\n");
            sb.Append("　 職位＝省内職位（#141）の段別 在任/定員。承認＝内閣人事局（事務次官級は首相・局長級以下は所管大臣／委任された副大臣）。\n");
            sb.Append("　 省益が高い省庁ほど横断政策に抵抗する（縦割り）。要職任命・首班は政府オブザーバ（Alt+G）。</color>");
            return sb.ToString();
        }

        private void AppendFaction(StringBuilder sb, GalaxyView gv, FactionState s, List<Person> roster, int year)
        {
            sb.Append('\n').Append("<color=#e7e0b0>◤ ").Append(s.faction).Append("</color>\n");

            var ctx = new CareerContext
            {
                faction = s.faction,
                politics = s.politics,
                ledger = s.civilService,
                roster = roster,
                year = year
            };

            if (ctx.ledger == null)
                sb.Append("  <color=#ffcc66>人事台帳 未初期化</color> <color=#7f8a96>（#141 の年次人事が未実行／旧セーブ）＝省庁の配属だけを表示</color>\n");
            else
                sb.Append("  <color=#9aa7b2>人事台帳</color> 在任 ").Append(ServingTotal(ctx.ledger)).Append("名　履歴 ")
                  .Append(ctx.ledger.history != null ? ctx.ledger.history.Count : 0).Append("件　<color=#7f8a96>暦年 ")
                  .Append(year).Append("年</color>\n");

            IReadOnlyList<Ministry> mins = gv.MinistriesOf(s.faction);
            if (mins == null || mins.Count == 0)
            {
                sb.Append("  <color=#9aa7b2>省庁 未配線</color>\n");
                AppendHistory(sb, gv, ctx);
                return;
            }

            var tree = mins as List<Ministry> ?? new List<Ministry>(mins);

            // 最上位（省＝親なし）から再帰で全段降りる。
            bool anyTop = false;
            for (int i = 0; i < tree.Count; i++)
            {
                Ministry m = tree[i];
                if (m == null || !m.IsTopLevel) continue;
                anyTop = true;
                AppendMinistry(sb, gv, tree, m, 1, ctx);
            }
            if (!anyTop)
                sb.Append("  <color=#9aa7b2>最上位の省が見つかりません</color>\n");

            AppendHistory(sb, gv, ctx);
        }

        /// <summary>省庁を1つ描画し、子（庁/局）を再帰で降りる。depth＝インデント段（1＝省）。</summary>
        private void AppendMinistry(StringBuilder sb, GalaxyView gv, List<Ministry> tree, Ministry m, int depth, CareerContext ctx)
        {
            string indent = new string('　', depth); // 全角スペースで段付け
            string branch = depth <= 1 ? "■ " : "└ ";

            // ヘッダ行：省庁名（所掌）・臨時フラグ
            sb.Append(indent).Append("<color=#cfe0f0>").Append(branch).Append(m.ministryName).Append("</color>")
              .Append(" <color=#7f8a96>(").Append(m.domain).Append(')').Append("</color>");
            if (m.isTemporary) sb.Append(" <color=#e8a060>［臨時］</color>");
            sb.Append('\n');

            // 大臣／長官（headOfficeId は Office.id。在任者は GovernmentRegistry から引く）
            string headName = HeadOfficeHolderName(m);

            // 充足率（この省庁単体）・行政効率（配下含む）・省益
            int assigned = m.staffIds != null ? m.staffIds.Count : 0;
            int slots = Mathf.Max(0, m.staffSlots);
            float fill = slots > 0 ? Mathf.Clamp01((float)assigned / slots) : 0f;
            float eff = MinistryAdminRules.StaffingEfficiency(m, tree, FindPerson(gv));

            sb.Append(indent).Append("　<color=#9aa7b2>配属</color> ").Append(assigned).Append('/').Append(slots)
              .Append(' ').Append(Bar(fill, 8))
              .Append("　<color=#9aa7b2>行政効率</color> ").Append((eff * 100f).ToString("0")).Append('%')
              .Append("　<color=#9aa7b2>省益</color> ").Append((m.institutionalInterest * 100f).ToString("0")).Append('%');
            if (!string.IsNullOrEmpty(headName))
                sb.Append("　<color=#9aa7b2>長</color> ").Append(headName);
            sb.Append('\n');

            // 段別の在任/定員（#141・定員は CivilServicePostRules.SlotsFor が唯一の出所＝ここで持たない）
            if (ctx.ledger != null)
            {
                sb.Append(indent).Append("　<color=#9aa7b2>職位</color> ");
                for (int g = 0; g < GradeOrder.Length; g++)
                {
                    if (g > 0) sb.Append('・');
                    sb.Append(GradeShort[g]).Append(' ')
                      .Append(CivilServicePostRules.ServingCount(ctx.ledger, m.id, GradeOrder[g])).Append('/')
                      .Append(CivilServicePostRules.SlotsFor(m, GradeOrder[g], PostParams));
                }
                sb.Append('\n');
            }

            // 承認権者（内閣人事局）の在否＝任命可能／承認者空席／上申先。判定は Core に委ね状態は変えない
            sb.Append(indent).Append("　<color=#9aa7b2>承認</color> 次官級▸ ")
              .Append(ApprovalStatus(gv, ctx, m.id, BureaucratGrade.事務次官級))
              .Append("　局長級以下▸ ")
              .Append(ApprovalStatus(gv, ctx, m.id, BureaucratGrade.局長級))
              .Append('\n');

            // 配属官僚（文民）を名前と文才つきで列挙＋省内職位（在任記録があれば職位名・在任年・官位・考課）
            if (m.staffIds != null && m.staffIds.Count > 0)
            {
                for (int i = 0; i < m.staffIds.Count; i++)
                {
                    Person p = FindPersonById(gv, m.staffIds[i]);
                    sb.Append(indent).Append("　・");
                    if (p != null)
                        sb.Append(p.name).Append(" <color=#7f8a96>(文才 ").Append(p.CivilAptitude.ToString("0")).Append(')').Append("</color>");
                    else
                        sb.Append("<color=#9aa7b2>id ").Append(m.staffIds[i]).Append("</color>");
                    sb.Append("　").Append(CareerNote(ctx, p, m.staffIds[i]));
                    sb.Append('\n');
                }
            }

            // 子（庁/局）を再帰
            if (m.childIds != null)
            {
                for (int i = 0; i < m.childIds.Count; i++)
                {
                    Ministry child = MinistryRules.Get(tree, m.childIds[i]);
                    if (child != null) AppendMinistry(sb, gv, tree, child, depth + 1, ctx);
                }
            }
        }

        // ===== 省内職位（#141・すべて read-only） =====

        /// <summary>その配属者の省内職位（台帳の在任記録）。記録が無ければ「台帳未登録」＋就けない理由（隠さない）。</summary>
        private static string CareerNote(CareerContext ctx, Person p, int personId)
        {
            CivilServiceRecord r = CivilServicePostRules.FindServing(ctx.ledger, personId);
            if (r == null)
            {
                string why = ctx.ledger == null
                    ? "人事台帳 未初期化"
                    : (CivilServicePostRules.PersonProblem(p, ctx.faction) ?? "台帳へ未移行（年次人事で移行される）");
                return "<color=#9aa7b2>台帳未登録</color><color=#7f8a96>（" + why + "）</color>";
            }

            int tenure = Mathf.Max(0, ctx.year - r.appointedYear);
            var sb = new StringBuilder(112);
            sb.Append("<color=#cfe0b0>").Append(CivilServicePostRules.GradeTitle(r.ministryName, r.grade)).Append("</color>")
              .Append("<color=#7f8a96>・在任 ").Append(tenure).Append("年・官位 ")
              .Append(p != null ? p.courtRank.ToString() : "不明")
              .Append("・考課 ").Append(MeritText(p)).Append("</color>");
            return sb.ToString();
        }

        /// <summary>考課（考第の平均と回数）。未評定は明示する。</summary>
        private static string MeritText(Person p)
            => p != null && p.merit != null && p.merit.HasRecord
                ? p.merit.AverageScore.ToString("0.0") + "（" + p.merit.evaluations + "回）"
                : "未評定";

        /// <summary>
        /// その省のその段の承認権者の状態（任命可能／委任承認／承認者空席／上申先）。<see cref="CivilServicePostRules.ApprovalAuthority"/>
        /// を「誰でもない操作者（-1）」で引いて上申先＝本来の承認権者（事務次官級＝首相／局長級以下＝所管大臣）を割り出し、
        /// その本人で引き直して現に承認できるかを見る＝判定を二重実装せず、状態も変えない。
        /// <para>局長級以下は大臣の明示の委任（CabinetAppointmentRules.Delegate＝所管決裁・期限つき）を受けた副大臣も承認しうる。
        /// 副大臣は内閣の職（<see cref="CabinetState.posts"/>＝<see cref="CabinetAppointmentRules.FindPost"/>）から引き、
        /// 可否は必ず本人IDの <see cref="CivilServicePostRules.ApprovalAuthority"/> に委ねる＝-1 の probe だけで
        /// 在席・委任の有効を決めない（委任の失効・大臣の空席は Core が拒み、ここでは可能と書かない）。</para>
        /// </summary>
        private static string ApprovalStatus(GalaxyView gv, CareerContext ctx, int ministryId, BureaucratGrade grade)
        {
            AppointmentResult probe = CivilServicePostRules.ApprovalAuthority(ctx.politics, ctx.faction, -1, ministryId,
                grade, ctx.roster, ctx.year);
            if (!probe.canPetition || probe.petitionToId < 0)
                return "<color=#e08a8a>承認者空席</color><color=#7f8a96>（" + probe.reason + "）</color>";

            int approverId = probe.petitionToId;
            string nm = PersonLabel(gv, approverId);
            bool approverOk = CanApprove(ctx, approverId, ministryId, grade, out string approverWhy);
            string delegated = DelegatedApproverText(gv, ctx, ministryId, grade, approverId);

            if (approverOk)
                return "<color=#a9d6a9>任命可能</color> <color=#7f8a96>上申先</color> " + nm + delegated;
            // 大臣が承認できなくても、有効な委任を持つ副大臣が承認できるなら隠さない（理由は併記する）
            if (delegated.Length > 0)
                return "<color=#a9d6a9>任命可能</color>" + delegated
                     + "　<color=#e0c080>大臣は承認不可</color> <color=#7f8a96>上申先</color> " + nm
                     + "<color=#7f8a96>（" + approverWhy + "）</color>";
            return "<color=#e0c080>承認不可</color> <color=#7f8a96>上申先</color> " + nm + "<color=#7f8a96>（" + approverWhy + "）</color>";
        }

        /// <summary>
        /// 局長級以下で、有効な委任を持つ副大臣が現に承認できるときの表示（できない・居ない・事務次官級なら空文字）。
        /// 事務次官級は内閣人事局＝首相だけ＝委任の対象外。
        /// </summary>
        private static string DelegatedApproverText(GalaxyView gv, CareerContext ctx, int ministryId, BureaucratGrade grade, int approverId)
        {
            if (grade == BureaucratGrade.事務次官級) return "";
            CabinetState cab = ctx.politics != null ? ctx.politics.cabinet : null;
            CabinetPost vice = CabinetAppointmentRules.FindPost(cab, ministryId, CabinetPostKind.副大臣);
            int viceId = vice != null ? vice.holderId : -1;
            if (viceId < 0 || viceId == approverId) return "";
            if (!CanApprove(ctx, viceId, ministryId, grade, out _)) return "";
            return "　<color=#a9d6a9>委任承認</color> <color=#7f8a96>副大臣</color> " + PersonLabel(gv, viceId)
                 + "<color=#7f8a96>（" + vice.delegation + "・SE" + vice.delegationEndYear + "まで）</color>";
        }

        /// <summary>その人物が現にその段の人事を承認できるか（判定は Core＝ApprovalAuthority に委ねる・状態は変えない）。</summary>
        private static bool CanApprove(CareerContext ctx, int personId, int ministryId, BureaucratGrade grade, out string reason)
        {
            reason = "";
            if (personId < 0) return false;
            AppointmentResult r = CivilServicePostRules.ApprovalAuthority(ctx.politics, ctx.faction, personId, ministryId,
                grade, ctx.roster, ctx.year);
            reason = r.reason;
            return r.ok;
        }

        /// <summary>名簿で引けた人物名（引けなければ id を隠さず出す）。</summary>
        private static string PersonLabel(GalaxyView gv, int personId)
        {
            Person p = FindPersonById(gv, personId);
            return p != null ? p.name : "人物#" + personId;
        }

        /// <summary>直近の退任履歴（新しい順・上限つき）。超過は件数で示し黙って捨てない。</summary>
        private void AppendHistory(StringBuilder sb, GalaxyView gv, CareerContext ctx)
        {
            if (ctx.ledger == null) return;
            List<CivilServiceRecord> h = ctx.ledger.history;
            int total = h != null ? h.Count : 0;
            if (total == 0)
            {
                sb.Append("  <color=#9aa7b2>人事履歴</color> <color=#7f8a96>なし</color>\n");
                return;
            }

            int rows = Mathf.Max(0, historyRows);
            int shown = Mathf.Min(rows, total);
            sb.Append("  <color=#9aa7b2>人事履歴</color> <color=#7f8a96>（新しい順・最大 ").Append(rows).Append("件）</color>\n");
            for (int i = 0; i < shown; i++)
            {
                CivilServiceRecord r = h[total - 1 - i];
                if (r == null) continue;
                Person p = FindPersonById(gv, r.personId);
                sb.Append("  ・<color=#7f8a96>").Append(r.vacatedYear).Append("年</color> ")
                  .Append(StatusTag(r.status)).Append(' ')
                  .Append(CivilServicePostRules.GradeTitle(r.ministryName, r.grade)).Append(' ')
                  .Append(p != null ? p.name : "人物#" + r.personId);
                if (!string.IsNullOrEmpty(r.reason))
                    sb.Append(" <color=#7f8a96>（").Append(r.reason).Append("）</color>");
                sb.Append('\n');
            }

            int rest = total - shown;
            if (rest > 0 || ctx.ledger.historyDropped > 0)
            {
                sb.Append("  <color=#9aa7b2>…ほか ").Append(rest).Append("件");
                if (ctx.ledger.historyDropped > 0)
                    sb.Append("（上限で捨てた ").Append(ctx.ledger.historyDropped).Append("件を除く）");
                sb.Append("</color>\n");
            }
        }

        /// <summary>退任の種類を色つきの短い札に（昇任＝緑／降任・解任＝赤／異動＝青／退職＝灰）。</summary>
        private static string StatusTag(CivilServiceStatus status)
        {
            switch (status)
            {
                case CivilServiceStatus.昇任: return "<color=#a9d6a9>昇任</color>";
                case CivilServiceStatus.降任: return "<color=#e0c080>降任</color>";
                case CivilServiceStatus.解任: return "<color=#e08a8a>解任</color>";
                case CivilServiceStatus.異動: return "<color=#9fc0e0>異動</color>";
                case CivilServiceStatus.退職: return "<color=#9aa7b2>退職</color>";
                default: return "<color=#9aa7b2>" + status + "</color>";
            }
        }

        /// <summary>台帳の在任者の総数（全省・全段）。</summary>
        private static int ServingTotal(CivilServiceState st)
        {
            if (st == null || st.records == null) return 0;
            int n = 0;
            for (int i = 0; i < st.records.Count; i++)
                if (st.records[i] != null && st.records[i].IsServing) n++;
            return n;
        }

        private static string HeadOfficeHolderName(Ministry m)
        {
            if (m.headOfficeId < 0) return "";
            var apps = GovernmentRegistry.Appointments;
            if (apps == null) return "";
            for (int i = 0; i < apps.Count; i++)
            {
                GovernmentRegistry.Appointment ap = apps[i];
                if (ap.office != null && ap.office.id == m.headOfficeId && ap.holder != null)
                    return ap.holder.CharacterName;
            }
            return "";
        }

        // ===== Person 解決 =====

        private System.Func<int, Person> FindPerson(GalaxyView gv) => id => FindPersonById(gv, id);

        /// <summary>
        /// 承認権限・資格の判定（Core の窓口）が読む名簿＝文民＋軍人。観測のたびに組み直すだけで名簿は書き換えない。
        /// </summary>
        private static List<Person> BuildRoster(GalaxyView gv)
        {
            var list = new List<Person>();
            if (gv == null) return list;
            IReadOnlyList<Person> civilians = gv.CivilianRoster;
            if (civilians != null)
                for (int i = 0; i < civilians.Count; i++)
                    if (civilians[i] != null) list.Add(civilians[i]);
            IReadOnlyList<Person> commanders = gv.CommanderRoster;
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                    if (commanders[i] != null) list.Add(commanders[i]);
            return list;
        }

        private static Person FindPersonById(GalaxyView gv, int personId)
        {
            if (gv == null) return null;
            IReadOnlyList<Person> civilians = gv.CivilianRoster;
            if (civilians != null)
                for (int i = 0; i < civilians.Count; i++)
                    if (civilians[i] != null && civilians[i].id == personId) return civilians[i];
            IReadOnlyList<Person> commanders = gv.CommanderRoster;
            if (commanders != null)
                for (int i = 0; i < commanders.Count; i++)
                    if (commanders[i] != null && commanders[i].id == personId) return commanders[i];
            return null;
        }

        /// <summary>0..1 を文字バーに（観測層の手仕上げ表示）。</summary>
        private static string Bar(float v, int width)
        {
            v = Mathf.Clamp01(v);
            int filled = Mathf.RoundToInt(v * width);
            var sb = new StringBuilder(width + 16);
            sb.Append("<color=#6fae6f>");
            for (int i = 0; i < filled; i++) sb.Append('▮');
            sb.Append("</color><color=#3a3f44>");
            for (int i = filled; i < width; i++) sb.Append('▯');
            sb.Append("</color>");
            return sb.ToString();
        }

        // ===== UI 構築（GovernmentObserverOverlay と同型・単一スクロールラベル版） =====

        private void BuildUI()
        {
            EnsureEventSystem();

            overlayRoot = new GameObject("BureaucracyObserverCanvas");
            overlayRoot.transform.SetParent(transform);
            Canvas canvas = overlayRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = canvasSortingOrder;
            CanvasScaler scaler = overlayRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            overlayRoot.AddComponent<GraphicRaycaster>();

            panel = new GameObject("ObserverPanel");
            panel.transform.SetParent(overlayRoot.transform, false);
            RectTransform panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.sizeDelta = Vector2.zero;
            panelRT.anchoredPosition = Vector2.zero;
            Image dimImage = panel.AddComponent<Image>();
            dimImage.color = new Color(0f, 0f, 0f, dimAlpha);
            WindowChrome.MakeNonModal(dimImage);

            BuildContentPanel(panel.transform);
        }

        private void BuildContentPanel(Transform parent)
        {
            GameObject frame = new GameObject("ObserverFrame");
            frame.transform.SetParent(parent, false);
            RectTransform frameRT = frame.AddComponent<RectTransform>();
            frameRT.anchorMin = new Vector2(0f, 0.5f);
            frameRT.anchorMax = new Vector2(0f, 0.5f);
            frameRT.pivot = new Vector2(0f, 0.5f);
            frameRT.anchoredPosition = new Vector2(24f, 0f);
            frameRT.sizeDelta = new Vector2(panelWidth, panelMaxHeight);

            Image frameImg = frame.AddComponent<Image>();
            frameImg.color = panelColor;

            VerticalLayoutGroup vlg = frame.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(16, 16, 12, 12);
            vlg.spacing = 8f;
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            WindowChrome.AddTitleBarLayout(frameRT, "官僚機構", () => SetVisible(false));
            BuildScrollBody(frame.transform);
        }

        private void BuildScrollBody(Transform parent)
        {
            GameObject scrollObj = new GameObject("ObserverScrollRect");
            scrollObj.transform.SetParent(parent, false);
            scrollObj.AddComponent<RectTransform>();
            LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 30f;

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
            UiScrollbars.Attach(scrollRect);   // #H スクロールできることを画面で示す（見えて掴めるバー）

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
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            GameObject esObj = new GameObject("EventSystem");
            esObj.AddComponent<EventSystem>();
            esObj.AddComponent<InputSystemUIInputModule>();
        }
    }
}
