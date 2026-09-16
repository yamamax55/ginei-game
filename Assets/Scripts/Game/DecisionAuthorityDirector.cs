using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 「いま操作しているのは<b>誰か</b>」を決裁の権限判定へ橋渡しする（GitHub #67）。
    ///
    /// プレイヤーはいち人物であって全能の裁可者ではない。右下にカードが出ていても、
    /// その案件の所掌（軍事／内政／外交／財政）を決裁する役職に就いていなければ裁可できず、
    /// <b>上申</b>になる。判定そのものは Core の <see cref="DecisionAuthorityRules"/>＝ここは
    /// 「操作者・役職・政体」を集めて渡すだけ（権限の体系を二重に作らない）。
    ///
    /// <see cref="DecisionDeck.AuthorityCheck"/> に差し込むので、右下カード・決裁ボード・
    /// スクリプト（テスト）のどの経路から裁可しても<b>必ずこの判定を通る</b>＝迂回路ができない。
    /// Strategy シーンへ自動生成（手配線不要）。
    /// </summary>
    public class DecisionAuthorityDirector : MonoBehaviour
    {
        [Tooltip("権限の判定を有効にする（off＝従来どおり誰でも裁可できる。移行時の逃げ道）")]
        public bool enforceAuthority = true;

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
            if (FindAnyObjectByType<DecisionAuthorityDirector>() != null) return;
            new GameObject("DecisionAuthorityDirector").AddComponent<DecisionAuthorityDirector>();
        }

        [Tooltip("上申の返事が返るまでの game-秒")]
        public float reviewSeconds = 30f;

        /// <summary>上申した案件の経過（決裁id→上申してからの game-秒）。</summary>
        private readonly Dictionary<int, float> escalatedAt = new Dictionary<int, float>();

        private PetitionEscalationParams EscalationParams
            => new PetitionEscalationParams(reviewSeconds, PetitionEscalationParams.Default.approveThreshold);

        private void Awake()
        {
            if (enforceAuthority) DecisionDeck.AuthorityCheck = Evaluate;
        }

        private void Update()
        {
            if (escalatedAt.Count == 0) return;

            GameClock clock = StrategySession.Clock;
            float now = clock != null ? (float)clock.ElapsedSeconds : Time.time;
            GalaxyView gv = GalaxyView.Active;
            DecisionQueue q = DecisionDeck.Queue;
            if (gv == null || q == null) return;

            // 上申中の案件を1件ずつ見て、返事の時期が来たら決裁権者に判断させる。
            List<int> done = null;
            foreach (var kv in escalatedAt)
            {
                PendingDecision d = FindCard(q, kv.Key);
                if (d == null || !d.escalated) { (done ??= new List<int>()).Add(kv.Key); continue; }

                Person decider = d.deciderId > 0 ? gv.FindPersonById(d.deciderId) : null;
                bool present = decider != null && !decider.IsDeceased;
                float elapsed = now - kv.Value;

                EscalationOutcome outcome = PetitionEscalationRules.Review(
                    present, elapsed, Favor(decider), EscalationParams);
                if (outcome == EscalationOutcome.審査中) continue;

                (done ??= new List<int>()).Add(kv.Key);
                Conclude(d, decider, outcome);
            }
            if (done != null) for (int i = 0; i < done.Count; i++) escalatedAt.Remove(done[i]);
        }

        /// <summary>決裁権者の賛意（資質＋案件の切迫度）。決定論＝同じ状況なら同じ答え。QA の見込み表示も同じ式を読む。</summary>
        public static float Favor(Person decider)
        {
            if (decider == null) return 0f;
            return PetitionEscalationRules.Favor(decider.operation, decider.intelligence, 0.5f);
        }

        /// <summary>
        /// 上申の結論を出す。承認なら<b>決裁権者の権限で</b>実際に確定させる（効果は1回だけ）。
        /// 却下・差し戻しは効果を出さず、理由が起案者へ戻る。
        /// </summary>
        private void Conclude(PendingDecision d, Person decider, EscalationOutcome outcome)
        {
            d.escalated = false;
            string who = decider != null ? decider.name : d.deciderName;

            if (outcome == EscalationOutcome.承認)
            {
                // ★承認でも、確定の直前に実際の決裁者を同じ判定へ通す（審査中の失職・解任・首相交代・委任の撤回/期限切れで古い権限を使わない）。
                ConcludeApproval(d, who, EvaluateDecider(decider, d.effectKey));
                return;
            }

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                PetitionEscalationRules.ResultText(outcome, who, d.title));
            // 却下・差し戻し＝確定させて閉じる（効果は出さない）。以後この札は動かない。
            CloseWithoutEffect(d, outcome == EscalationOutcome.却下 ? $"{who} が却下しました" : "決裁権者が不在のため差し戻し");
        }

        /// <summary>
        /// 上申の承認を確定する共通の手順。<paramref name="deciderAuth"/>＝決裁の時点で実際の決裁者を
        /// <see cref="EvaluateFor"/> に通した結果。裁可できなければ効果を出さずに差し戻して閉じる。
        /// 裁可できれば権限判定を一時的に外し（決裁者の権限で通ったことを表す）、共通入口 <see cref="DecisionDeck.Resolve"/> で確定する＝効果は1回だけ。
        /// 確定したら true。
        /// </summary>
        public static bool ConcludeApproval(PendingDecision d, string deciderName, in DecisionAuthorityResult deciderAuth)
        {
            if (d == null || DecisionResolutionRules.IsSettled(d)) return false;
            d.escalated = false;
            string who = string.IsNullOrEmpty(deciderName) ? "決裁権者" : deciderName;

            if (!deciderAuth.CanDecide)
            {
                d.authorityBasis = deciderAuth.basis;
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                    $"［差し戻し］{d.title}：{who} は決裁の時点で権限がありません（{deciderAuth.basis}）");
                CloseWithoutEffect(d, $"{who} は決裁の時点で権限がないため差し戻し（{deciderAuth.basis}）");
                return false;
            }

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                PetitionEscalationRules.ResultText(EscalationOutcome.承認, who, d.title));
            d.authorityBasis = deciderAuth.basis;
            var saved = DecisionDeck.AuthorityCheck;
            DecisionDeck.AuthorityCheck = null;
            try { return DecisionDeck.Resolve(d.id, 0); }
            finally { DecisionDeck.AuthorityCheck = saved; }
        }

        /// <summary>効果を出さずに確定して閉じる（誰も効果を出さないよう適用の権利を消費する）。</summary>
        private static void CloseWithoutEffect(PendingDecision d, string reason)
        {
            DecisionResolutionRules.Settle(d, d.defaultChoiceIndex, auto: true, out _);
            DecisionResolutionRules.ClaimForApply(d);
            DecisionResolutionRules.RecordResult(d, PetitionActionResult.Fail(PetitionActionOutcome.対象外, reason));
            ClosePetitionWithoutEffect(d);
        }

        /// <summary>
        /// カードに紐づく稟議を却下で締める（効果なしで閉じた札の稟議を「決裁待ち」のまま台帳に残さない）。
        /// 税と編制は id を別採番するので、効果キーも一致するものだけを扱う。
        /// </summary>
        private static void ClosePetitionWithoutEffect(PendingDecision d)
        {
            if (d == null || d.petitionId <= 0) return;
            PetitionLedger ledger = FleetRingiDirector.IsFleetEffectKey(d.effectKey) ? FleetRingiDirector.Ledger : RingiDirector.Ledger;
            Petition pet = ledger != null ? ledger.Get(d.petitionId) : null;
            if (pet == null || !FleetRingiDirector.SameEffectKey(pet.effectKey, d.effectKey)) return;
            RingiPipeline.Decide(pet, approve: false); // 決裁待ちのときだけ却下へ
        }

        /// <summary>上申先の決裁者を、決裁の時点で改めて判定する（不在・他勢力への離反は権限外）。</summary>
        private static DecisionAuthorityResult EvaluateDecider(Person decider, string effectKey)
        {
            if (decider == null || decider.IsDeceased)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁権者が不在です（死亡・離脱）");
            GalaxyView gv = GalaxyView.Active;
            Person player = gv != null ? gv.PlayerCharacter() : null;
            if (player != null && decider.faction != player.faction)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, $"決裁権者が他勢力（{decider.faction}）の人物です");
            return EvaluateFor(decider, effectKey);
        }

        private static PendingDecision FindCard(DecisionQueue q, int id)
        {
            for (int i = 0; i < q.items.Count; i++)
                if (q.items[i] != null && q.items[i].id == id) return q.items[i];
            return null;
        }

        /// <summary>
        /// その案件を<b>上申する</b>（権限が無かったとき）。決裁権者と根拠をカードへ記録し、審査を始める。
        /// すでに上申中なら何もしない。
        /// </summary>
        public void Escalate(PendingDecision d, in DecisionAuthorityResult auth)
        {
            if (d == null || d.escalated || DecisionResolutionRules.IsSettled(d)) return;

            d.escalated = true;
            d.deciderId = auth.addresseeId;
            d.deciderName = auth.addresseeName;
            d.authorityBasis = auth.basis;

            GameClock clock = StrategySession.Clock;
            escalatedAt[d.id] = clock != null ? (float)clock.ElapsedSeconds : Time.time;

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［上申］{d.title} を {(!string.IsNullOrEmpty(auth.addresseeName) ? auth.addresseeName : "所管")} へ上げました");
        }

        /// <summary>共有の窓口（決裁デスク・決裁ボードから呼ぶ）。</summary>
        public static void EscalateShared(PendingDecision d, in DecisionAuthorityResult auth)
        {
            var self = FindAnyObjectByType<DecisionAuthorityDirector>();
            if (self != null) self.Escalate(d, auth);
        }

        private void OnDestroy()
        {
            // 自分が差し込んだフックだけ外す（他が差し替えていたら触らない）。
            if (DecisionDeck.AuthorityCheck == Evaluate) DecisionDeck.AuthorityCheck = null;
        }

        /// <summary>
        /// その決裁を、いまの操作者が裁可できるか。
        /// <b>命令発行の時点で毎回引き直す</b>ので、配属変更・指揮移譲・代行終了・死亡で自動的に更新される
        /// （権限を握ったまま持ち歩かない）。
        /// </summary>
        private DecisionAuthorityResult Evaluate(PendingDecision d) => EvaluateEffectKey(d != null ? d.effectKey : "");

        /// <summary>
        /// その効果キーの案件を、いまの操作者が裁可できるかの<b>見込み</b>（表示用・読み取りのみ）。
        /// 判定は裁可時と同じ <see cref="EvaluateEffectKey"/>。権限判定が差し込まれていなければ false
        /// （＝誰でも裁可できる状態）。上申も確定もしない。
        /// </summary>
        public static bool TryPreviewAuthority(string effectKey, out DecisionAuthorityResult result)
        {
            DecisionAuthorityDirector self = FindAnyObjectByType<DecisionAuthorityDirector>();
            if (self == null || DecisionDeck.AuthorityCheck == null)
            {
                result = new DecisionAuthorityResult(DecisionAuthority.裁可, "権限判定なし（誰でも裁可できます）");
                return false;
            }
            result = EvaluateEffectKey(effectKey);
            return true;
        }

        /// <summary>効果キーだけで権限を判定する（裁可時の判定と見込み表示で共通）。</summary>
        private static DecisionAuthorityResult EvaluateEffectKey(string effectKey)
        {
            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
                return new DecisionAuthorityResult(DecisionAuthority.裁可, "盤面がないため判定を省略");

            Person actor = gv.PlayerCharacter();
            if (actor == null)
                // 操作者を特定できない＝従来どおり通す（権限で遊べなくしない・後方互換）。
                return new DecisionAuthorityResult(DecisionAuthority.裁可, "操作者が特定できないため判定を省略");

            return EvaluateFor(actor, effectKey);
        }

        /// <summary>
        /// その人物がその効果キーの案件を裁可できるか（裁可時・上申の確定時・見込み表示・稟議の起票で共通の判定）。
        /// 役職（<see cref="GovernmentRegistry"/>）の判定に、内閣の閣僚職（所管大臣・委任を受けた副大臣）の権限と所管大臣への上申先を足す
        /// （<see cref="CabinetDecisionAuthorityRules.Evaluate"/>）。内閣は毎回その時点の状態から組み直す。
        /// </summary>
        public static DecisionAuthorityResult EvaluateFor(Person actor, string effectKey)
        {
            if (actor == null)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁する人物がいません");
            if (actor.IsDeceased)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁者が不在です（死亡・離脱）");

            GalaxyView gv = GalaxyView.Active;
            if (gv == null)
                return new DecisionAuthorityResult(DecisionAuthority.裁可, "盤面がないため判定を省略");

            // ★省内職位の人事（#141）は<b>一般の効果キーの分野推定（DomainOf→内政）で代用しない</b>。
            // 効果キーから省・段・人物を復号し、内閣人事局の承認権限（CivilServicePostRules.ApprovalAuthority）へ直接渡す
            // ＝事務次官級＝首相／局長級以下＝所管大臣（有効な委任を受けた副大臣を含む）という同じ1つの判定を、
            // 裁可・上申の確定・見込み表示・起票のすべてで通す。
            if (CivilServiceRingiRules.IsCivilServiceKey(effectKey))
                return gv.EvaluateCivilServiceAuthority(actor, effectKey);

            List<Office> offices = GovernmentRegistry.GetOffices(actor);
            CivilianControlType control = gv.CivilianControlOf(actor.faction);

            return CabinetDecisionAuthorityRules.Evaluate(
                actor, effectKey ?? "", offices, control,
                domain => gv.FindOfficeHolder(actor.faction, domain, actor),
                gv.CabinetDecisionContextOf(actor.faction),
                id => gv.FindPersonById(id));
        }
    }
}
