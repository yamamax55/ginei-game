using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 稟議の最小ループ配線（DESK-6 合流・MEYASU #1296）。戦略マップで<b>稟議を実際に回す</b>橋渡し：
    /// 統一クロックの game-時間で時おり建白（減税/増税）を起こし、官僚機構の生存ロール（<see cref="PetitionFlowRules"/>）で
    /// 大半を間引き、浮上したものだけ <see cref="DecisionDeck"/>（右下の決裁デスク）へ積む。プレイヤーが裁可すると
    /// <see cref="RingiPipeline.ExecuteAndApply"/> で官僚の執行忠実度ぶん骨抜きにされた実効量だけ世界（税率）が動く。
    /// 数式・状態遷移は Core 窓口へ委譲（並行新設しない）。Strategy シーンへ自動生成（手配線不要）。
    /// </summary>
    public class RingiDirector : MonoBehaviour
    {
        [Header("生起ペース（game-秒）")]
        [Tooltip("この game-秒ごとに建白の発生判定を行う（ポーズ中は進まない・倍速で速まる）")]
        public float raiseInterval = 50f;
        [Tooltip("判定ごとに建白が起きる確率")]
        [Range(0f, 1f)] public float raiseChance = 0.6f;
        [Tooltip("同時に決裁待ちにできる稟議の上限（積みすぎ防止）")]
        public int maxConcurrent = 3;

        /// <summary>
        /// 進行中の稟議在庫（建白→伝播→決裁→執行）。観測/UI から読めるよう公開。
        /// ★実体は <see cref="StrategySession.Petitions"/>（保存・ロード・新規戦役のリセットと同じ入れ物）。
        /// 以前は別の static を持っていたため、保存される台帳が常に空で、ロード後のカードを裁可しても何も起きなかった。
        /// </summary>
        public static PetitionLedger Ledger
            => StrategySession.Petitions ?? (StrategySession.Petitions = new PetitionLedger());

        private struct Pending { public Petition pet; public float friction; }
        private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

        private float accum;
        /// <summary>
        /// この Director が使う決裁idの番号帯（デモ決裁 9001+ と衝突させない）。
        /// 実際の採番は <see cref="DecisionDeck.NextDecisionId"/>＝シーン往復で巻き戻らない。
        /// </summary>
        private const int DecisionIdBand = 80000;

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
            if (scene.name != "Strategy") return; // 戦役（戦略マップ）でのみ稟議を回す
            if (UnityEngine.Object.FindAnyObjectByType<RingiDirector>() != null) return;
            new GameObject("RingiDirector").AddComponent<RingiDirector>();
        }

        private void Awake() => DecisionDeck.Resolved += OnResolved;
        private void OnDestroy() => DecisionDeck.Resolved -= OnResolved;

        private void Update()
        {
            if (StrategySession.Campaign == null) return; // 戦役が走っていなければ何もしない

            // テスト用：F7 で即サンプル建白を1件起こす（決裁フローを Unity で即確認するため）
            if (Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame)
                ForceRaise();

            GameClock clock = StrategySession.Clock;
            float gdt = clock != null ? (float)clock.EffectiveDt(Time.unscaledDeltaTime) : Time.deltaTime;
            accum += gdt;
            if (accum < raiseInterval) return;
            accum = 0f;

            // ★状況起案（作業票④）：実状態を見て意味のある建白だけを出す。
            // 出すものが無ければ<b>何も出さない</b>（間を持たせるための定型案を混ぜない）。
            TryRaiseFromSituation();
        }

        /// <summary>サンプル建白を1件起こす（同時上限を無視＝F7/スクリプト/テスト用）。決裁デスクへ載った決裁id（&lt;0=官僚機構で死んだ）を返す。
        /// sampleIndex&lt;0 はランダム、0以上は <see cref="RingiSampleData"/> の指定サンプル。</summary>
        public int ForceRaise(int sampleIndex = -1) => TryRaisePetition(forced: true, sampleIndex: sampleIndex);

        /// <summary>
        /// 最高権力者が決めきれない重大案件を目安箱へ諮問する（MEYASU-4 #1300）。
        /// 建白の逆向きなので官僚伝播を通さず、同じ台帳と決裁デスクへ直接積む。
        /// </summary>
        public int SubmitConsultation(string title, string body, string effectKey, float cabinetAgreement,
            BoxKind box = BoxKind.政治家, string regionKey = "")
        {
            FactionState fs = PlayerState();
            if (fs == null || ActivePendingCount() >= maxConcurrent) return -1;
            var pet = new Petition(0, title, fs.faction, box, PetitionOrigin.諮問, effectKey, regionKey)
            {
                severity = DecisionSeverity.重大,
                status = PetitionStatus.起案,
            };
            if (!WorkflowRules.ShouldDeferToBox(pet, fs, cabinetAgreement)) return -1;

            pet.status = PetitionStatus.決裁待ち;
            if (!Ledger.Add(pet)) return -1;
            var decision = new PendingDecision(DecisionDeck.NextDecisionId(DecisionIdBand), title,
                DecisionSeverity.重大, DecisionSource.諮問, effectKey, defaultChoiceIndex: 0, body: body);
            decision.choices.Add("裁可する");
            decision.choices.Add("却下する");
            decision.petitionId = pet.id;
            decision.friction = MinistryFriction(fs.faction, DomainOf(effectKey));
            DecisionDeck.Enqueue(decision);
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.警告,
                $"［諮問］{title}（重大案件・裁可が必要）");
            return decision.id;
        }

        // ----- 状況起案（作業票④）-----

        /// <summary>その状況で最後に建白した game-秒（クールダウンの判定に使う）。</summary>
        private readonly Dictionary<PetitionTrigger, float> lastRaisedAt = new Dictionary<PetitionTrigger, float>();

        /// <summary>状況起案の調整値。</summary>
        public PetitionAgendaParams AgendaParams => new PetitionAgendaParams(
            120f, 0.35f, 0.35f, 0.5f, agendaCooldownSeconds, maxConcurrent);

        [Tooltip("同じ状況の建白を再び上げるまでの間隔（game-秒）")]
        public float agendaCooldownSeconds = 180f;

        /// <summary>
        /// 実状態から建白を1件起こす。上げるものが無ければ何もしない。
        /// 重複抑止＝①同じ状況の未解決案件があれば出さない ②クールダウン ③同時件数の上限。
        /// </summary>
        private int TryRaiseFromSituation()
        {
            FactionState fs = PlayerState();
            GalaxyView gv = GalaxyView.Active;
            if (fs == null || gv == null) return -1;

            PetitionSituation sit = gv.MeasurePetitionSituation(fs.faction);
            float now = StrategySession.Clock != null ? (float)StrategySession.Clock.ElapsedSeconds : 0f;

            PetitionAgendaItem item = PetitionAgendaRules.Next(
                sit, AgendaParams, ActivePendingCount(),
                trigger => HasPendingFor(trigger),
                trigger => lastRaisedAt.TryGetValue(trigger, out float t) ? now - t : float.MaxValue);

            if (!item.IsValid) return -1;
            int id = RaiseAgendaItem(fs, item);
            if (id >= 0) lastRaisedAt[item.trigger] = now;
            return id;
        }

        /// <summary>
        /// 試験用：生起の間隔を待たずに、本番と同じ状況起案（盤面の計測→重複/クールダウン判定→官僚機構の伝播→起票）を1回だけ行う。
        /// 権限・カード・結果は渡さない。決裁id（&lt;0＝出すものが無い／官僚機構で止まった）。
        /// </summary>
        public int RaiseFromSituationForQa() => TryRaiseFromSituation();

        /// <summary>いま決裁待ちで残っている（この Director が出した）案件の数。</summary>
        private int ActivePendingCount()
        {
            DecisionQueue q = DecisionDeck.Queue;
            if (q == null) return 0;
            int n = 0;
            for (int i = 0; i < q.items.Count; i++)
            {
                PendingDecision d = q.items[i];
                if (d == null || d.petitionId <= 0) continue;
                if (DecisionResolutionRules.IsSettled(d)) continue;
                if (OwnPetition(d) != null) n++;
            }
            return n;
        }

        /// <summary>
        /// カードに対応する、この Director の稟議（無ければ null）。
        /// ★税と編制の台帳は id を別々に採番するので、id だけで引くと他方の案件を掴みうる＝効果キーの一致も確かめる。
        /// </summary>
        private static Petition OwnPetition(PendingDecision d)
        {
            if (d == null || d.petitionId <= 0 || FleetRingiDirector.IsFleetEffectKey(d.effectKey)) return null;
            Petition pet = Ledger.Get(d.petitionId);
            return pet != null && FleetRingiDirector.SameEffectKey(pet.effectKey, d.effectKey) ? pet : null;
        }

        /// <summary>その状況の建白が未解決で残っているか（同じ対象を二重に出さない）。</summary>
        private bool HasPendingFor(PetitionTrigger trigger)
        {
            DecisionQueue q = DecisionDeck.Queue;
            if (q == null) return false;
            for (int i = 0; i < q.items.Count; i++)
            {
                PendingDecision d = q.items[i];
                if (d == null || DecisionResolutionRules.IsSettled(d)) continue;
                if (PetitionAgendaRules.TriggerOf(d.effectKey) == trigger) return true;
            }
            return false;
        }

        /// <summary>状況から起きた建白を官僚機構へ通し、抜けたら決裁デスクへ積む。決裁id（&lt;0＝不発）。</summary>
        private int RaiseAgendaItem(FactionState fs, in PetitionAgendaItem item)
        {
            string title = TitleFor(item.trigger);
            var pet = new Petition(0, title, fs.faction, BoxKind.政治家, PetitionOrigin.建白, item.effectKey);
            if (!RingiPipeline.Submit(Ledger, pet)) return -1;

            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.政治家);
            float friction = MinistryFriction(fs.faction, DomainOf(item.effectKey));
            float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
            var step = RingiPipeline.Propagate(pet, heed, friction, legitimacy, Random.value);
            if (step != PetitionStep.通過)
            {
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"［{(step == PetitionStep.握り潰し ? "握り潰し" : "黙殺")}］{title}（官僚機構で止まった）");
                return -1;
            }

            RingiPipeline.SendToDecision(pet);
            // ★決裁前に判断材料を出す（作業票⑤）：なぜ上がったか・費用・対象・期待効果・実行時期。
            string body = item.reason + "\n" + PetitionBriefingRules.Brief(item.effectKey, BriefingContext());
            var pd = new PendingDecision(DecisionDeck.NextDecisionId(DecisionIdBand), title, DecisionSeverity.通常,
                DecisionSource.建白結果, pet.effectKey, defaultChoiceIndex: 1, body: body);
            pd.choices.Add("裁可する");
            pd.choices.Add("見送る（現状維持）");
            pd.petitionId = pet.id;
            pd.friction = friction;

            // ★提案の時点で対象を固定する（承認後に別の対象へ勝手に振り替えないため）。
            pd.SetTarget(PetitionActionRules.PlanTarget(item.effectKey, BriefingContext()));

            // ★提案者・決裁権者・権限の根拠をカードへ載せる（#67・実在の人物だけ）。
            StampAttribution(pd, item.effectKey);

            DecisionDeck.Enqueue(pd);

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［建白］{title} が決裁待ち（右下の決裁デスクへ）");
            return pd.id;
        }

        /// <summary>
        /// 提案者・決裁権者・権限の根拠をカードへ記録する（#67）。
        /// <b>実在の人物からしか名前を取らない</b>＝分からなければ空のままにする（架空の名前を作らない）。
        /// </summary>
        private static void StampAttribution(PendingDecision pd, string effectKey)
        {
            GalaxyView gv = GalaxyView.Active;
            if (gv == null || pd == null) return;

            Person actor = gv.PlayerCharacter();
            if (actor != null) { pd.proposerId = actor.id; pd.proposerName = actor.name; }

            OfficeDomain domain = DecisionAuthorityRules.DomainOf(effectKey);

            if (actor != null)
            {
                // 裁可時・見込み表示と同じ判定（役職＋閣僚職・所管大臣への上申先）で決裁権者を載せる。
                DecisionAuthorityResult auth = DecisionAuthorityDirector.EvaluateFor(actor, effectKey);
                pd.authorityBasis = auth.basis;
                if (auth.CanDecide) { pd.deciderId = actor.id; pd.deciderName = actor.name; }
                else if (auth.addresseeId > 0) { pd.deciderId = auth.addresseeId; pd.deciderName = auth.addresseeName; }
            }
            else
            {
                Person holder = gv.FindOfficeHolder(
                    GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟, domain);
                if (holder != null) { pd.deciderId = holder.id; pd.deciderName = holder.name; }
            }
        }

        /// <summary>判断材料を組み立てるための盤面（無ければ null＝見込みは出せる範囲で出す）。</summary>
        private static PetitionActionContext BriefingContext()
        {
            GalaxyView gv = GalaxyView.Active;
            return gv != null ? gv.BuildPetitionActionContext() : null;
        }

        /// <summary>状況に対応する建白の題目。</summary>
        private static string TitleFor(PetitionTrigger trigger)
        {
            switch (trigger)
            {
                case PetitionTrigger.財政難: return "増税の建白（国庫窮迫）";
                case PetitionTrigger.重税の不満: return "減税の建白（重税の不満）";
                case PetitionTrigger.敵の接近: return "動員令の建白（敵が接近）";
                case PetitionTrigger.守りの綻び: return "防衛強化の建白（守りの綻び）";
                case PetitionTrigger.戦争の長期化: return "講和の建白（戦の長期化）";
                case PetitionTrigger.余剰の艦艇: return "攻勢の建白（艦艇に余剰）";
                default: return "建白";
            }
        }

        // ----- 星系別統治政策の上申（#67/#109/#141）-----

        /// <summary>
        /// 星系別統治政策の変更を地方箱へ上申する（#67/#109/#141）。直接変更せず、
        /// 官僚機構の伝播→決裁デスク→執行を通過した場合だけ対象 Province の政策を更新する。
        /// ★対象（星系・政策）は効果キーに固定し、稟議との対応・摩擦はカード自身に持たせる
        /// ＝シーン往復で Director が作り直されても裁可が黙って無視されない。
        /// </summary>
        public int SubmitGovernancePolicy(int systemId, string systemName, Faction faction, GovernancePolicy targetPolicy)
        {
            FactionState fs = PlayerState();
            if (fs == null || fs.faction != faction || ActivePendingCount() >= maxConcurrent) return -1;
            if (HasPendingGovernanceFor(systemId)) return -1; // 同じ星系への重複上申を積まない

            string regionKey = systemId.ToString();
            string title = $"{systemName} 統治政策「{targetPolicy}」への変更";
            string effectKey = GovernanceRules.PolicyPetitionKey(systemId, targetPolicy);
            var pet = new Petition(0, title, faction, BoxKind.地方, PetitionOrigin.建白, effectKey, regionKey);
            if (!RingiPipeline.Submit(Ledger, pet)) return -1;

            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.地方, regionKey);
            float friction = MinistryFriction(faction, OfficeDomain.内政);
            float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
            PetitionStep step = RingiPipeline.Propagate(pet, heed, friction, legitimacy, Random.value);
            if (step != PetitionStep.通過)
            {
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"［{(step == PetitionStep.握り潰し ? "握り潰し" : "黙殺")}］{title}（地方官僚機構で止まった）");
                return -1;
            }

            RingiPipeline.SendToDecision(pet);
            var decision = new PendingDecision(DecisionDeck.NextDecisionId(DecisionIdBand), $"{title}（地方箱）", DecisionSeverity.通常,
                DecisionSource.建白結果, effectKey, defaultChoiceIndex: 1,
                body: $"{systemName} の統治政策を「{targetPolicy}」へ改める上申。安定・統合・産出・反乱圧に波及する。所管官僚の抵抗により執行が遅れる場合がある。");
            decision.choices.Add("裁可する");
            decision.choices.Add("見送る（現状維持）");
            decision.petitionId = pet.id;
            decision.friction = friction;

            // ★提案者・決裁権者・権限の根拠をカードへ載せる（#67・実在の人物だけ）。
            StampAttribution(decision, effectKey);

            DecisionDeck.Enqueue(decision);
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［上申］{title} が決裁待ち（右下の決裁デスクへ）");
            return decision.id;
        }

        /// <summary>
        /// 統治政策の上申の見込み（対象・現在・次の政策・受け付けない理由）。<b>キー（Alt+T）も星系情報パネルのボタンも
        /// ここを通す</b>＝表示と実行で判定がずれない。星系と所有は執行時と同じ <see cref="StrategySession.Map"/>／
        /// <see cref="StrategySession.Provinces"/> を見る。政策は変えない（読み取りのみ）。
        /// </summary>
        public static GovernanceProposalPreview PreviewGovernanceProposal(int systemId)
        {
            GalaxyMap map = StrategySession.Map;
            StarSystem system = map != null ? map.GetSystem(systemId) : null;
            Province province = null;
            bool hasProvince = system != null && StrategySession.Provinces != null
                               && StrategySession.Provinces.TryGetValue(systemId, out province) && province != null;
            GovernancePolicy current = hasProvince ? province.governancePolicy : GovernancePolicy.民生;
            GovernancePolicy next = GovernanceProposalRules.NextPolicy(current);

            RingiDirector director = FindAnyObjectByType<RingiDirector>();
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            FactionState fs = PlayerState();

            GovernanceProposalRejection r = GovernanceProposalRules.Evaluate(
                systemFound: system != null,
                owner: system != null ? system.owner : player,
                player: player,
                hasProvince: hasProvince,
                directorAvailable: director != null,
                hasPlayerState: fs != null && fs.faction == player,
                pendingCount: director != null ? director.ActivePendingCount() : 0,
                maxConcurrent: director != null ? director.maxConcurrent : 0,
                duplicatePending: HasPendingGovernanceFor(systemId));
            return new GovernanceProposalPreview(systemId, system != null ? system.systemName : "", current, next, r);
        }

        /// <summary>
        /// 次の統治政策を上申する<b>唯一の入口</b>（Alt+T・星系情報パネルのボタン共通）。
        /// <see cref="PreviewGovernanceProposal"/> で受け付けを確かめてから <see cref="SubmitGovernancePolicy"/> へ渡す。
        /// 受け付けない理由・官僚機構で止まったことは通知に出す（黙って何も起きない、にしない）。
        /// 戻り値＝決裁id（&lt;0＝上申されなかった）。政策そのものは決裁→執行まで変わらない。
        /// </summary>
        public static int ProposeNextGovernancePolicy(int systemId, out GovernanceProposalPreview preview,
                                                      out string message)
        {
            preview = PreviewGovernanceProposal(systemId);
            if (!preview.CanSubmit)
            {
                message = GovernanceProposalRules.RejectionText(preview.rejection, preview.systemName);
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, message);
                return -1;
            }

            RingiDirector director = FindAnyObjectByType<RingiDirector>();
            Faction player = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            int id = director != null
                ? director.SubmitGovernancePolicy(systemId, preview.systemName, player, preview.next)
                : -1;
            // 受付判定は通ったのに -1＝ほぼ官僚機構で止まった場合（SubmitGovernancePolicy が握り潰し/黙殺を通知済み）。
            if (id >= 0)
                message = $"{preview.systemName} 統治政策「{preview.current}」→「{preview.next}」を上申しました（右下の決裁デスクへ）";
            else
            {
                message = $"{preview.systemName} の上申は決裁デスクまで届きませんでした（地方官僚機構で止まった等・もう一度上申できます）";
                NotificationCenter.Push(NotificationCategory.内政, NotificationSeverity.注意, message);
            }
            return id;
        }

        /// <summary>その星系への統治政策の上申が未解決で残っているか（決裁デスクのカードから判定＝シーン往復でも失わない）。</summary>
        private static bool HasPendingGovernanceFor(int systemId)
        {
            DecisionQueue q = DecisionDeck.Queue;
            if (q == null) return false;
            for (int i = 0; i < q.items.Count; i++)
            {
                PendingDecision d = q.items[i];
                if (d == null || DecisionResolutionRules.IsSettled(d)) continue;
                if (GovernanceRules.TryParsePolicyPetitionKey(d.effectKey, out int id, out _) && id == systemId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 承認済みの統治政策の上申を執行する。政策はカテゴリ値なので、執行が成立したときに切り替える
        /// （実効率は「命令が現地へどこまで届いたか」として通知し、数値効果は既存 GovernanceRules が時間で反映する）。
        /// ★提案時の星系にだけ効かせる。所有が変わった・星系が無いなら<b>失敗として記録</b>し振り替えない。
        /// </summary>
        private static PetitionActionResult ExecuteGovernancePolicy(Petition pet, float friction, int systemId,
                                                                    GovernancePolicy policy, out float applied)
        {
            applied = 0f;
            Province province = null;
            // ★所有の確認は盤面（GalaxyView）の有無に依らず、保存・ロードされる StrategySession.Map で行う。
            //   星系が地図に無い場合も「確認できないので通す」にしない（ロード後の古い所有で執行しない）。
            GalaxyMap map = StrategySession.Map;
            StarSystem system = map != null ? map.GetSystem(systemId) : null;
            if (system == null || StrategySession.Provinces == null ||
                !StrategySession.Provinces.TryGetValue(systemId, out province) || province == null)
            {
                WorkflowRules.Execute(pet, 0f); // 稟議は閉じる（在庫を占有させない）
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし, "対象の星系が見つかりません");
            }

            if (system.owner != pet.faction)
            {
                WorkflowRules.Execute(pet, 0f);
                return PetitionActionResult.Fail(PetitionActionOutcome.対象なし,
                    $"{system.systemName} はすでに管轄外のため統治政策を変更できません");
            }

            applied = WorkflowRules.Execute(pet, PetitionFlowRules.ExecutionFidelity(friction));
            if (applied <= 0f)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "執行されませんでした");

            province.governancePolicy = policy;
            return new PetitionActionResult(PetitionActionOutcome.実行, $"統治政策を「{policy}」へ変更", applied);
        }

        // ----- 建白の起案＋官僚機構の伝播 -----

        /// <summary>サンプル建白を1件起こす。forced=true は同時上限を無視。決裁待ちへ載った決裁id（&lt;0=不発/死亡）を返す。</summary>
        private int TryRaisePetition(bool forced, int sampleIndex)
        {
            FactionState fs = PlayerState();
            if (fs == null || RingiSampleData.Count == 0) return -1;
            if (!forced && pending.Count >= maxConcurrent) return -1;

            RingiSample sample = sampleIndex >= 0
                ? RingiSampleData.At(sampleIndex)
                : RingiSampleData.At(Random.Range(0, RingiSampleData.Count));

            var pet = new Petition(0, sample.title, fs.faction, sample.box, PetitionOrigin.建白, sample.effectKey);
            if (!RingiPipeline.Submit(Ledger, pet)) return -1; // 越階受理＋在庫投入

            // 官僚機構を1階：箱の信認 × 省益摩擦 × 正統性 で生存ロール（大半はここで死ぬ）
            float heed = CredibilityRules.Heed(fs.credibility, sample.box);
            float friction = MinistryFriction(fs.faction, DomainOf(sample.effectKey)); // 所管省庁の省益＝縦割り抵抗（#158 配線）
            float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
            var step = RingiPipeline.Propagate(pet, heed, friction, legitimacy, Random.value);

            if (step != PetitionStep.通過)
            {
                // 握り潰し（却下）/黙殺＝上に行かず勝手に死ぬ（内生スロットル）
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"［{(step == PetitionStep.握り潰し ? "握り潰し" : "黙殺")}］{sample.title}（官僚機構で止まった）");
                return -1;
            }

            // 浮上＝権力者の決裁待ちへ。決裁デスク（右下）へカードを積む
            RingiPipeline.SendToDecision(pet);
            var pd = new PendingDecision(DecisionDeck.NextDecisionId(DecisionIdBand), $"{sample.title}（{sample.box}箱）", DecisionSeverity.通常,
                DecisionSource.建白結果, pet.effectKey, defaultChoiceIndex: 1, body: sample.body);
            pd.choices.Add("裁可する");
            pd.choices.Add("見送る（現状維持）");
            // ★稟議との対応と摩擦を<b>カード自身に持たせる</b>＝シーン往復で Director の
            //   インスタンス状態が消えても対応を失わない（幽霊カードを作らない・保存もできる）。
            pd.petitionId = pet.id;
            pd.friction = friction;
            DecisionDeck.Enqueue(pd);

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［建白］{sample.title} が決裁待ち（右下の決裁デスクへ）");
            return pd.id;
        }

        // ----- 決裁の確定（人 or 自動）→ 執行で世界が動く -----

        /// <summary>
        /// 決裁の確定を受けて稟議を執行する。
        ///
        /// ★対応する稟議は<b>カードが持つ <see cref="PendingDecision.petitionId"/></b> から静的台帳を引く
        /// （以前は Director のインスタンス辞書を引いていたため、Strategy→Battle→Strategy の往復で
        /// 辞書が消え、裁可が黙って無視されて稟議が「決裁待ち」のまま台帳を永久占有していた）。
        /// ★効果の適用は <see cref="DecisionResolutionRules.ClaimForApply"/> を勝ち取った1回だけ。
        /// </summary>
        private void OnResolved(PendingDecision d, int choiceIndex)
        {
            if (d == null || d.petitionId <= 0) return;      // 稟議に紐づかない決裁は対象外
            if (FleetRingiDirector.IsFleetEffectKey(d.effectKey)) return; // 編制は FleetRingiDirector の担当
            Petition pet = OwnPetition(d);
            if (pet == null)
            {
                // 対応する稟議が台帳に無い（旧セーブ等）＝効果は出さず、黙って終わらせずに失敗として記録する。
                FleetRingiDirector.RecordMissingPetition(d, NotificationCategory.政治);
                return;
            }

            if (!DecisionResolutionRules.ClaimForApply(d)) return; // 二重適用を防ぐ（どの経路から来ても1回）
            pending.Remove(d.id);                             // 旧経路の在庫も掃除（枠を空ける）

            bool approve = choiceIndex == 0; // 0=裁可する / 1=見送る・却下
            RatificationResult ratification = default;
            if (pet.origin == PetitionOrigin.諮問)
            {
                ratification = WorkflowRules.ApplyRatification(pet, approve, PlayerState());
                if (!ratification.applied)
                {
                    DecisionResolutionRules.RecordResult(d,
                        PetitionActionResult.Fail(PetitionActionOutcome.対象外, ratification.summary));
                    return;
                }
            }
            else
                RingiPipeline.Decide(pet, approve);

            if (!approve)
            {
                DecisionResolutionRules.RecordResult(d,
                    new PetitionActionResult(PetitionActionOutcome.対象外,
                        pet.origin == PetitionOrigin.諮問 ? ratification.summary : "見送り（現状維持）"));
                NotificationCenter.Push(NotificationCategory.政治,
                    pet.origin == PetitionOrigin.諮問 ? NotificationSeverity.警告 : NotificationSeverity.情報,
                    pet.origin == PetitionOrigin.諮問
                        ? $"［諮問却下］{pet.title}：{ratification.summary}"
                        : $"［見送り］{pet.title}（現状維持）");
                return;
            }

            // ★省内職位の人事（#141）は国家値・盤面の効果ではない＝通常の効果レジストリ（PetitionEffects/PetitionActionRules）へ
            // 流さず専用の執行へ分ける。官僚の省益で人事の内容を値切らない（friction を実効量に使わない）。
            // 一度きりの適用（ClaimForApply）・稟議の決裁（RingiPipeline.Decide）・結果の記録は上の共通経路のまま。
            if (CivilServiceRingiRules.IsCivilServiceKey(pet.effectKey))
            {
                PetitionActionResult personnel = ExecuteCivilServicePersonnel(pet, d);
                DecisionResolutionRules.RecordResult(d, personnel);
                NotificationCenter.Push(NotificationCategory.人事,
                    personnel.ok ? NotificationSeverity.情報 : NotificationSeverity.注意,
                    $"［{(personnel.ok ? "執行" : "実行不可")}］{pet.title}：{personnel.detail}");
                return;
            }

            float applied;
            PetitionActionResult action;
            if (GovernanceRules.TryParsePolicyPetitionKey(pet.effectKey, out int policySystemId, out GovernancePolicy policy))
            {
                // 統治政策の上申：対象はキーに固定済み（提案時の星系にだけ効かせる）
                action = ExecuteGovernancePolicy(pet, d.friction, policySystemId, policy, out applied);
            }
            else
            {
                // 執行：官僚の執行忠実度（friction）で骨抜き＝通っても満額は効かない
                applied = RingiPipeline.ExecuteAndApply(pet, StrategySession.Campaign, d.friction);

                // ★盤面まで届く効果（動員・攻勢・防衛・講和）は、ここで実際にゲームを動かす。
                // 国庫と民心だけ動かして終わり、にしない（作業票③）。
                action = ExecuteBoardAction(pet.effectKey, applied, d.Target);
            }
            DecisionResolutionRules.RecordResult(d, action);

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                $"［執行］{pet.title}：実効 {applied * 100f:0}%（官僚に骨抜きされた）" +
                (string.IsNullOrEmpty(action.detail) ? "" : $" ／ {action.detail}"));
        }

        /// <summary>
        /// 承認された人事（省内職位・#141）の執行。判定と台帳の更新は <see cref="CivilServicePostRules.Execute"/> が
        /// 唯一の窓口（<see cref="GalaxyView.ExecuteApprovedCivilServicePost"/> が決裁の時点の状態で通す）。
        /// 理由はカード本文の最終行から取り出して人事履歴へ残す。執行できたら稟議を執行済みへ（実効は骨抜きにせず1.0）、
        /// 執行できなければ実効0で閉じる＝「承認」のまま台帳に残さない。
        /// </summary>
        private static PetitionActionResult ExecuteCivilServicePersonnel(Petition pet, PendingDecision d)
        {
            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
            {
                WorkflowRules.Execute(pet, 0f); // 稟議は閉じる（在庫を占有させない）
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "盤面がありません");
            }

            string reason = CivilServiceRingiRules.ExtractReason(d != null ? d.body : "");
            PetitionActionResult r = gv.ExecuteApprovedCivilServicePost(pet.faction,
                d != null ? d.deciderId : -1, pet.effectKey, reason);
            WorkflowRules.Execute(pet, r.ok ? 1f : 0f);
            return r;
        }

        /// <summary>
        /// 盤面まで届く執行（<see cref="PetitionActionRules"/>）。対象外のキーなら何もしない。
        /// 盤面の参照は <see cref="GalaxyView"/> から集める＝Core は MonoBehaviour を知らない。
        /// </summary>
        private static PetitionActionResult ExecuteBoardAction(string effectKey, float magnitude,
                                                              PetitionTarget target)
        {
            if (!PetitionActionRules.IsActionKey(effectKey))
            {
                // ★盤面へ届かない効果キーを「成功」に見せない。
                // 未登録キー（例：treaty.sign）は<b>未対応と明示</b>して失敗として返す。
                if (!PetitionEffects.Has(effectKey))
                    return PetitionActionResult.Fail(PetitionActionOutcome.対象外,
                        $"この決裁（{effectKey}）に対応する効果がまだ実装されていません");
                // ★国家状態へ効く効果（税など）は ExecuteAndApply で適用済み＝実効量があれば「実行」と記録する
                //   （以前は適用しても 対象外 と記録され、結果行が「執行されませんでした」になっていた）。
                return magnitude > 0f
                    ? new PetitionActionResult(PetitionActionOutcome.実行, $"実効 {magnitude * 100f:0}% で執行", magnitude)
                    : PetitionActionResult.Fail(PetitionActionOutcome.対象外, "官僚機構で執行されませんでした");
            }

            GalaxyView gv = GalaxyView.Active;
            PetitionActionContext ctx = gv != null ? gv.BuildPetitionActionContext() : null;
            if (ctx == null)
                return PetitionActionResult.Fail(PetitionActionOutcome.対象外, "盤面がありません");

            // ★提案時に固定した対象で執行する（別の対象へ振り替えない）。
            return PetitionActionRules.Execute(effectKey, ctx, magnitude, target);
        }

        /// <summary>稟議の効果分野→所管 <see cref="OfficeDomain"/>（税は財政＝大蔵省の省益が抵抗する）。</summary>
        private static OfficeDomain DomainOf(string effectKey)
        {
            if (!string.IsNullOrEmpty(effectKey) && effectKey.StartsWith("tax.")) return OfficeDomain.財政;
            return OfficeDomain.内政;
        }

        /// <summary>上申で使う摩擦の読み取り（QA の見込み表示用・<see cref="MinistryFriction"/> と同じ値）。</summary>
        public static float PreviewMinistryFriction(Faction faction, OfficeDomain domain) => MinistryFriction(faction, domain);

        /// <summary>所管省庁の省益から伝播/執行の摩擦を引く（#158 配線）。省庁ツリーが無ければ既定 0.4 へフォールバック。</summary>
        private static float MinistryFriction(Faction faction, OfficeDomain domain)
        {
            const float fallback = 0.4f;
            // 起票の帰属・判断材料と同じ盤面（Active）を優先する。
            GalaxyView gv = GalaxyView.Active != null ? GalaxyView.Active : UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null) return fallback;
            var ministries = gv.MinistriesOf(faction);
            if (ministries == null || ministries.Count == 0) return fallback;
            // 該当分野の省庁が無ければ（省益0）＝抵抗なしと区別するため fallback
            float f = MinistryRules.DomainFriction(ministries, domain);
            return f > 0f ? f : fallback;
        }

        private static FactionState PlayerState()
        {
            var camp = StrategySession.Campaign;
            if (camp?.states == null) return null;
            Faction pf = GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;
            for (int i = 0; i < camp.states.Count; i++)
                if (camp.states[i] != null && camp.states[i].faction == pf) return camp.states[i];
            return null;
        }
    }
}
