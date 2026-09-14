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

            NotificationCenter.Push(NotificationCategory.政治,
                outcome == EscalationOutcome.承認 ? NotificationSeverity.情報 : NotificationSeverity.注意,
                PetitionEscalationRules.ResultText(outcome, who, d.title));

            if (outcome != EscalationOutcome.承認)
            {
                // 却下・差し戻し＝確定させて閉じる（効果は出さない）。以後この札は動かない。
                DecisionResolutionRules.Settle(d, d.defaultChoiceIndex, auto: true, out _);
                DecisionResolutionRules.ClaimForApply(d);   // 誰も効果を出さないよう権利を消費する
                DecisionResolutionRules.RecordResult(d, PetitionActionResult.Fail(
                    PetitionActionOutcome.対象外,
                    outcome == EscalationOutcome.却下 ? $"{who} が却下しました" : "決裁権者が不在のため差し戻し"));
                return;
            }

            // ★承認＝決裁権者の権限で確定させる。権限判定を一時的に外し、確定と執行を通す
            //   （プレイヤーの権限では通らないが、決裁権者の権限では通る、を表現する）。
            var saved = DecisionDeck.AuthorityCheck;
            DecisionDeck.AuthorityCheck = null;
            try { DecisionDeck.Resolve(d.id, 0); }
            finally { DecisionDeck.AuthorityCheck = saved; }
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

            if (actor.IsDeceased)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁者が不在です（死亡・離脱）");

            List<Office> offices = GovernmentRegistry.GetOffices(actor);
            CivilianControlType control = gv.CivilianControlOf(actor.faction);

            return DecisionAuthorityRules.Evaluate(
                actor, effectKey ?? "", OfficeScope.国家, offices, control,
                domain => gv.FindOfficeHolder(actor.faction, domain, actor));
        }
    }
}
