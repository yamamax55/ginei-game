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

        /// <summary>進行中の稟議在庫（建白→伝播→決裁→執行）。観測/UI から読めるよう公開。</summary>
        public static readonly PetitionLedger Ledger = new PetitionLedger();

        private struct Pending
        {
            public Petition pet;
            public float friction;
            public bool isGovernancePolicy;
            public int targetSystemId;
            public GovernancePolicy targetPolicy;
        }
        private readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();

        private float accum;
        private int nextDecisionId = 80000; // デモ決裁(9001+)と衝突させない番号帯

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

            if (pending.Count >= maxConcurrent) return;
            if (Random.value > raiseChance) return;
            TryRaisePetition(forced: false, sampleIndex: -1);
        }

        /// <summary>サンプル建白を1件起こす（同時上限を無視＝F7/スクリプト/テスト用）。決裁デスクへ載った決裁id（&lt;0=官僚機構で死んだ）を返す。
        /// sampleIndex&lt;0 はランダム、0以上は <see cref="RingiSampleData"/> の指定サンプル。</summary>
        public int ForceRaise(int sampleIndex = -1) => TryRaisePetition(forced: true, sampleIndex: sampleIndex);

        /// <summary>
        /// 星系別統治政策の変更を地方箱へ上申する（#67/#109/#141）。直接変更せず、
        /// 官僚機構の伝播→決裁デスク→執行を通過した場合だけ対象 Province の政策を更新する。
        /// </summary>
        public int SubmitGovernancePolicy(int systemId, string systemName, Faction faction, GovernancePolicy targetPolicy)
        {
            FactionState fs = PlayerState();
            if (fs == null || fs.faction != faction || pending.Count >= maxConcurrent) return -1;
            foreach (Pending existing in pending.Values)
                if (existing.isGovernancePolicy && existing.targetSystemId == systemId)
                    return -1; // 同じ星系への重複上申を積まない

            string regionKey = systemId.ToString();
            string title = $"{systemName} 統治政策「{targetPolicy}」への変更";
            string effectKey = $"governance.policy.{systemId}.{(int)targetPolicy}";
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
            var decision = new PendingDecision(nextDecisionId++, $"{title}（地方箱）", DecisionSeverity.通常,
                DecisionSource.建白結果, effectKey, defaultChoiceIndex: 1,
                body: $"{systemName} の統治政策を「{targetPolicy}」へ改める上申。安定・統合・産出・反乱圧に波及する。所管官僚の抵抗により執行が遅れる場合がある。");
            decision.choices.Add("裁可する");
            decision.choices.Add("見送る（現状維持）");
            DecisionDeck.Enqueue(decision);
            pending[decision.id] = new Pending
            {
                pet = pet,
                friction = friction,
                isGovernancePolicy = true,
                targetSystemId = systemId,
                targetPolicy = targetPolicy
            };
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［上申］{title} が決裁待ち（右下の決裁デスクへ）");
            return decision.id;
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
            var pd = new PendingDecision(nextDecisionId++, $"{sample.title}（{sample.box}箱）", DecisionSeverity.通常,
                DecisionSource.建白結果, pet.effectKey, defaultChoiceIndex: 1, body: sample.body);
            pd.choices.Add("裁可する");
            pd.choices.Add("見送る（現状維持）");
            DecisionDeck.Enqueue(pd);
            pending[pd.id] = new Pending { pet = pet, friction = friction };

            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.注意,
                $"［建白］{sample.title} が決裁待ち（右下の決裁デスクへ）");
            return pd.id;
        }

        // ----- 決裁の確定（人 or 自動）→ 執行で世界が動く -----

        private void OnResolved(PendingDecision d, int choiceIndex)
        {
            if (d == null || !pending.TryGetValue(d.id, out var e)) return; // 自分の稟議でなければ無視
            pending.Remove(d.id);

            bool approve = choiceIndex == 0; // 0=裁可する / 1=見送る
            RingiPipeline.Decide(e.pet, approve);

            if (!approve)
            {
                NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                    $"［見送り］{e.pet.title}（現状維持）");
                return;
            }

            // 統治政策はカテゴリ値なので、承認後の執行成立時に切り替える。
            // 実効率は「命令が現地へどこまで届いたか」として通知し、数値効果は既存 GovernanceRules が時間で反映する。
            float applied;
            if (e.isGovernancePolicy)
            {
                float fidelity = PetitionFlowRules.ExecutionFidelity(e.friction);
                applied = WorkflowRules.Execute(e.pet, fidelity);
                if (applied > 0f && StrategySession.Provinces != null &&
                    StrategySession.Provinces.TryGetValue(e.targetSystemId, out Province province) && province != null)
                    province.governancePolicy = e.targetPolicy;
            }
            else
            {
                // 通常効果：官僚の執行忠実度で骨抜き＝通っても満額は効かない
                applied = RingiPipeline.ExecuteAndApply(e.pet, StrategySession.Campaign, e.friction);
            }
            NotificationCenter.Push(NotificationCategory.政治, NotificationSeverity.情報,
                $"［執行］{e.pet.title}：実効 {applied * 100f:0}%（官僚に骨抜きされた）");
        }

        /// <summary>稟議の効果分野→所管 <see cref="OfficeDomain"/>（税は財政＝大蔵省の省益が抵抗する）。</summary>
        private static OfficeDomain DomainOf(string effectKey)
        {
            if (!string.IsNullOrEmpty(effectKey) && effectKey.StartsWith("tax.")) return OfficeDomain.財政;
            return OfficeDomain.内政;
        }

        /// <summary>所管省庁の省益から伝播/執行の摩擦を引く（#158 配線）。省庁ツリーが無ければ既定 0.4 へフォールバック。</summary>
        private static float MinistryFriction(Faction faction, OfficeDomain domain)
        {
            const float fallback = 0.4f;
            var gv = UnityEngine.Object.FindAnyObjectByType<GalaxyView>();
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
