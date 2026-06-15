using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 艦隊の設立・解散を<b>稟議で回す</b>配線（艦隊編制の建議・<see cref="FleetEstablishmentRules"/> の戦略層）。
    /// 建艦で総プール（<see cref="FleetPool"/>）に余剰が出ると参謀本部が「新艦隊の設立」を、兵力0の空艦隊が
    /// 残ると「解散」を建白し、官僚機構の生存ロール（<see cref="PetitionFlowRules"/>）で間引いてから——
    /// <b>プレイヤー勢力は右下の決裁デスク（<see cref="DecisionDeck"/>）へ載せて裁可を仰ぎ</b>、AI勢力は自動裁可する。
    /// 承認されると官僚の執行忠実度ぶん値切られた規模で <see cref="FleetEstablishmentRules.ExecuteFromPetition"/> が
    /// 実際に台帳（<see cref="FleetRoster"/>）を動かす。数式・状態遷移は Core 窓口へ委譲（並行新設しない）。
    /// Strategy シーンへ自動生成（手配線不要）。<see cref="RingiDirector"/>（税の稟議）の艦隊版。
    /// </summary>
    public class FleetRingiDirector : MonoBehaviour
    {
        [Header("生起ペース（game-秒）")]
        [Tooltip("この game-秒ごとに各勢力の編制建議を検討する（ポーズ中は進まない・倍速で速まる）")]
        public float reviewInterval = 90f;
        [Tooltip("同時にプレイヤーへ積める編制稟議の上限（積みすぎ防止）")]
        public int maxConcurrent = 2;

        /// <summary>進行中の編制稟議在庫（建白→伝播→決裁→執行）。観測/UI から読めるよう公開。</summary>
        public static readonly PetitionLedger Ledger = new PetitionLedger();

        private struct Pending { public Petition pet; public float friction; }
        private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

        private float accum;
        private int nextDecisionId = 85000; // 税の稟議(80000台)・デモ決裁(9001+)と衝突させない番号帯

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
            if (UnityEngine.Object.FindAnyObjectByType<FleetRingiDirector>() != null) return;
            new GameObject("FleetRingiDirector").AddComponent<FleetRingiDirector>();
        }

        private void Awake() => DecisionDeck.Resolved += OnResolved;
        private void OnDestroy() => DecisionDeck.Resolved -= OnResolved;

        private void Update()
        {
            if (StrategySession.Campaign?.states == null) return; // 戦役が走っていなければ何もしない

            // テスト用：F8 で即プレイヤーの編制建議を1件起こす（決裁フローを Unity で即確認するため）
            if (Keyboard.current != null && Keyboard.current.f8Key.wasPressedThisFrame)
                ForcePlayerReview();

            GameClock clock = StrategySession.Clock;
            float gdt = clock != null ? (float)clock.EffectiveDt(Time.unscaledDeltaTime) : Time.deltaTime;
            accum += gdt;
            if (accum < reviewInterval) return;
            accum = 0f;

            ReviewAllFactions();
        }

        /// <summary>プレイヤー勢力の編制建議を1件起こす（同時上限を無視＝F8/テスト用）。</summary>
        public void ForcePlayerReview()
        {
            var fs = PlayerState();
            if (fs != null) RaisePlayerProposal(fs, forced: true);
        }

        // ----- 各勢力の建議検討（プレイヤー＝稟議へ／AI＝自動執行） -----

        private void ReviewAllFactions()
        {
            var states = StrategySession.Campaign.states;
            Faction pf = PlayerFaction();
            for (int i = 0; i < states.Count; i++)
            {
                var fs = states[i];
                if (fs == null) continue;
                if (fs.faction == pf) RaisePlayerProposal(fs, forced: false);
                else AutoResolveForAi(fs);
            }
        }

        /// <summary>AI勢力＝建議があれば官僚の生存ロールを経て自動裁可・執行（決裁デスクに載せない）。</summary>
        private void AutoResolveForAi(FactionState fs)
        {
            var p = FleetEstablishmentRules.RecommendProposal(fs.faction);
            if (p == null || !FleetEstablishmentRules.CanExecute(p)) return;

            float friction = FleetFriction(fs.faction);
            float fidelity = PetitionFlowRules.ExecutionFidelity(friction);
            if (FleetEstablishmentRules.Execute(p, fidelity))
                Notify(p, executed: true);
        }

        /// <summary>プレイヤー勢力＝建議を稟議へ載せ、官僚を抜けたら決裁デスクへ積む。</summary>
        private void RaisePlayerProposal(FactionState fs, bool forced)
        {
            if (!forced && pending.Count >= maxConcurrent) return;
            var p = FleetEstablishmentRules.RecommendProposal(fs.faction);
            if (p == null || !FleetEstablishmentRules.CanExecute(p)) return;

            var pet = FleetEstablishmentRules.ToPetition(p, 0, BoxKind.国王);
            if (!RingiPipeline.Submit(Ledger, pet)) return; // 越階受理＋在庫投入

            // 官僚機構を1階：箱の信認 × 軍政の省益摩擦 × 正統性 で生存ロール（大半はここで死ぬ）
            float heed = CredibilityRules.Heed(fs.credibility, BoxKind.国王);
            float friction = FleetFriction(fs.faction);
            float legitimacy = FactionLoyaltyRules.BaselineLoyalty(fs);
            var step = RingiPipeline.Propagate(pet, heed, friction, legitimacy, Random.value);
            if (step != PetitionStep.通過)
            {
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"［{(step == PetitionStep.握り潰し ? "握り潰し" : "黙殺")}］{p.Summary}（軍政機構で止まった）");
                return;
            }

            // 浮上＝決裁待ちへ。決裁デスク（右下）へカードを積む
            RingiPipeline.SendToDecision(pet);
            string body = p.kind == FleetProposalKind.設立
                ? $"参謀本部が新艦隊の設立を上申。総艦艇プールから{p.strength}隻を割く（承認すれば官僚の執行忠実度ぶん値切られる）。"
                : $"参謀本部が第{p.fleetNumber}艦隊の解散を上申。兵力を総プールへ戻す。";
            var pd = new PendingDecision(nextDecisionId++, $"［編制］{p.Summary}", DecisionSeverity.通常,
                DecisionSource.建白結果, pet.effectKey, defaultChoiceIndex: 1, body: body);
            pd.choices.Add("裁可する");
            pd.choices.Add("見送る（現状維持）");
            DecisionDeck.Enqueue(pd);
            pending[pd.id] = new Pending { pet = pet, friction = friction };

            NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                $"［編制建議］{p.Summary} が決裁待ち（右下の決裁デスクへ）");
        }

        // ----- 決裁の確定（人 or 自動）→ 執行で台帳が動く -----

        private void OnResolved(PendingDecision d, int choiceIndex)
        {
            if (d == null || !pending.TryGetValue(d.id, out var e)) return; // 自分の稟議でなければ無視
            pending.Remove(d.id);

            bool approve = choiceIndex == 0; // 0=裁可する / 1=見送る
            RingiPipeline.Decide(e.pet, approve);
            if (!approve)
            {
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"［見送り］{e.pet.title}（現状維持）");
                return;
            }

            // 執行：官僚の執行忠実度（friction）で骨抜き＝通っても満額の艦隊は揃わない
            float fidelity = PetitionFlowRules.ExecutionFidelity(e.friction);
            var p = FleetEstablishmentRules.Decode(e.pet.faction, e.pet.effectKey);
            if (p != null && FleetEstablishmentRules.Execute(p, fidelity))
            {
                WorkflowRules.Execute(e.pet, fidelity); // 陳情を執行済みへ（在庫の状態を締める）
                Notify(p, executed: true);
            }
            else
            {
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"［不発］{e.pet.title}（プールが足りず編制できなかった）");
            }
        }

        // ----- 補助 -----

        private static void Notify(FleetProposal p, bool executed)
        {
            if (p.kind == FleetProposalKind.設立)
            {
                var unit = FleetRoster.GetFleet(p.faction, p.fleetNumber);
                int got = unit != null ? unit.baseStrength : p.strength;
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"［{p.faction}］第{p.fleetNumber}艦隊を新編（{got}隻）");
            }
            else
            {
                NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                    $"［{p.faction}］第{p.fleetNumber}艦隊を解散（兵力をプールへ）");
            }
        }

        /// <summary>軍政（軍事省）の省益から伝播/執行の摩擦を引く（#158 配線）。省庁ツリーが無ければ既定 0.4。</summary>
        private static float FleetFriction(Faction faction)
        {
            const float fallback = 0.4f;
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
            if (gv == null) return fallback;
            var ministries = gv.MinistriesOf(faction);
            if (ministries == null || ministries.Count == 0) return fallback;
            float f = MinistryRules.DomainFriction(ministries, OfficeDomain.軍事);
            return f > 0f ? f : fallback;
        }

        private static Faction PlayerFaction()
            => GameSettings.Instance != null ? GameSettings.Instance.playerFaction : Faction.同盟;

        private static FactionState PlayerState()
        {
            var camp = StrategySession.Campaign;
            if (camp?.states == null) return null;
            Faction pf = PlayerFaction();
            for (int i = 0; i < camp.states.Count; i++)
                if (camp.states[i] != null && camp.states[i].faction == pf) return camp.states[i];
            return null;
        }
    }
}
