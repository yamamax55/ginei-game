using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦中の旗幟（#817 関ヶ原型「戦う前に決まる戦い」）を定期評価し、寝返り・静観・静観退きを
    /// 実艦隊へ発火させる配線。解決は <see cref="BattleAllegianceRules"/>／<see cref="LoyaltyRules"/>
    /// （純ロジック）に委譲し、ここは Unity 側の適用（陣営フリップ・発砲停止・通知）のみを行う。
    /// 全艦隊が完全忠誠（loyalty=1, intrigue=0）なら評価ごと休止＝従来動作（後方互換）。
    /// 旗幟は legacy 2 陣営（帝国/同盟）の会戦でのみ働く（3勢力以上は対象外＝最小実装）。
    /// Battle シーンに RuntimeInitializeOnLoadMethod で自動生成（手置き不要）。
    /// </summary>
    public class BattleAllegianceManager : MonoBehaviour
    {
        [Header("旗幟評価（#817）")]
        [Tooltip("旗幟を再評価する間隔（秒・timeScale 追従）")]
        public float evaluateInterval = 2f;

        [Tooltip("開戦から最初の評価までの猶予（秒）。趨勢が出る前に即断しない")]
        public float initialDelay = 3f;

        [Tooltip("純忠誠(loyalty-intrigue)がこれ以上なら「戦う」（LoyaltyParams.fightThreshold）")]
        [Range(0f, 1f)]
        public float fightThreshold = 0.5f;

        [Tooltip("調略(intrigue)がこれ以上＋自軍劣勢なら「寝返り」（LoyaltyParams.defectThreshold）")]
        [Range(0f, 1f)]
        public float defectThreshold = 0.5f;

        private bool built;            // 台帳構築済みか（全 Start 完了後の最初の Update で構築）
        private bool active;           // 旗幟が揺らぐ艦隊が居るか（居なければ毎フレーム何もしない）
        private float timer;
        private readonly List<Allegiance> allegiances = new List<Allegiance>();
        private readonly Dictionary<int, FleetStrength> fleetById = new Dictionary<int, FleetStrength>();
        private readonly List<StanceChange> changes = new List<StanceChange>();
        private Faction sideA, sideB;          // 会戦の2陣営（legacy enum）
        private FactionData dataA, dataB;      // 各陣営の代表 FactionData（寝返り先の色・敵対判定用。無ければ null）
        private FleetHUDManager hud;

        // ===== 自動生成エントリーポイント（HelpOverlay と同型）=====

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // 二重購読防止
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle") return;
            if (Object.FindAnyObjectByType<BattleAllegianceManager>() != null) return;
            new GameObject("BattleAllegianceManager").AddComponent<BattleAllegianceManager>();
        }

        // ===== 本体 =====

        private void Update()
        {
            if (BattleHandoff.IsSystemView) return; // 非戦闘のシステムビューでは何もしない

            if (!built)
            {
                // 全 Start 完了後（＝レジストリに旗艦が載ってから）一度だけ台帳を構築
                if (FleetRegistry.AllFlagships.Count == 0) return;
                Build();
                built = true;
                timer = -Mathf.Max(0f, initialDelay); // 最初の評価まで猶予
                return;
            }
            if (!active) return;

            timer += Time.deltaTime; // timeScale 追従（ポーズ中は進まない）
            if (timer < evaluateInterval) return;
            timer = 0f;
            Evaluate();
        }

        /// <summary>
        /// 旗幟台帳を構築する。揺らぐ艦隊（loyalty&lt;1 か intrigue&gt;0）が居て、
        /// かつ会戦が legacy 2 陣営の対戦である場合のみ有効化する。
        /// </summary>
        private void Build()
        {
            allegiances.Clear();
            fleetById.Clear();
            bool anyWavering = false;
            var sides = new List<Faction>(2);

            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || !fs.IsAlive || !fs.IsCombatant) continue;

                if (!sides.Contains(fs.faction)) sides.Add(fs.faction);
                if (fs.loyalty < 1f || fs.intrigue > 0f) anyWavering = true;

                int id = fs.GetInstanceID();
                allegiances.Add(new Allegiance(id, fs.faction, fs.strength, fs.loyalty, fs.intrigue));
                fleetById[id] = fs;

                // 各陣営の代表 FactionData（寝返り先の所属に使う）
                if (fs.factionData != null)
                {
                    if (sides.Count >= 1 && fs.faction == sides[0] && dataA == null) dataA = fs.factionData;
                    if (sides.Count >= 2 && fs.faction == sides[1] && dataB == null) dataB = fs.factionData;
                }
            }

            active = anyWavering && sides.Count == 2;
            if (!active) return;

            sideA = sides[0];
            sideB = sides[1];
            hud = Object.FindAnyObjectByType<FleetHUDManager>();
            Debug.Log($"BattleAllegianceManager: 旗幟評価を開始（{sideA} vs {sideB}・対象 {allegiances.Count} 艦隊）。");
        }

        /// <summary>現在戦力を同期して旗幟を再解決し、遷移と静観退きを実艦隊へ適用する。</summary>
        private void Evaluate()
        {
            // 現在戦力の同期（退却・撃沈は 0＝以後の趨勢に数えない。旗幟も確定扱い）
            for (int i = 0; i < allegiances.Count; i++)
            {
                Allegiance a = allegiances[i];
                FleetStrength fs = fleetById[a.id];
                bool alive = fs != null && fs.IsAlive;
                a.strength = alive ? Mathf.Max(0, fs.strength) : 0;
                if (!alive) a.locked = true;
            }

            // 旗幟の再解決（純ロジック）→ 遷移を適用
            changes.Clear();
            var p = new LoyaltyParams(fightThreshold, defectThreshold);
            BattleAllegianceRules.ResolveTransitions(allegiances, sideA, sideB, p, changes);
            for (int i = 0; i < changes.Count; i++) Apply(changes[i]);

            // 静観退き：戦う者が尽きた側の静観組は戦わずして戦場を去る（決着を停滞させない）
            WithdrawBystandersIfDecided(sideA, sideB);
            WithdrawBystandersIfDecided(sideB, sideA);
        }

        /// <summary>旗幟遷移1件を実艦隊へ適用する。</summary>
        private void Apply(StanceChange c)
        {
            if (!fleetById.TryGetValue(c.id, out FleetStrength fs) || fs == null || !fs.IsAlive) return;

            fs.battleStance = c.to;
            switch (c.to)
            {
                case Stance.寝返り:
                    ApplyDefection(fs);
                    break;
                case Stance.静観:
                    ShowMessage($"{LabelOf(fs)}は動かない……（静観）");
                    break;
                case Stance.戦う:
                    if (c.from == Stance.静観) ShowMessage($"{LabelOf(fs)}が参戦した！");
                    break;
            }
        }

        /// <summary>寝返りの実適用：陣営フリップ・AI/選択の整理・手動目標の解除・通知。</summary>
        private void ApplyDefection(FleetStrength fs)
        {
            bool wasSideA = fs.faction == sideA;
            Faction newLegacy = wasSideA ? sideB : sideA;
            FactionData newData = wasSideA ? dataB : dataA;

            fs.Defect(newData, newLegacy);

            // プレイヤー側へ来たなら手動指揮へ、離れたなら AI 指揮へ（主人公は AI に乗っ取らせない・GON-6）
            bool nowPlayerSide = IsPlayerSide(fs);
            FleetAI ai = fs.GetComponent<FleetAI>();
            if (ai != null) ai.enabled = ProtagonistRules.ShouldEnableAI(fs.admiralData, nowPlayerSide);

            // プレイヤーの選択から外す（敵になった艦隊を選択し続けない）
            if (!nowPlayerSide)
            {
                FleetCommander commander = Object.FindAnyObjectByType<FleetCommander>();
                Selectable sel = fs.GetComponent<Selectable>();
                if (commander != null && sel != null && commander.SelectedFleets.Contains(sel))
                {
                    sel.SetSelected(false);
                    commander.SelectedFleets.Remove(sel);
                }
            }

            // 旧敵（今は味方）が寝返り艦隊を手動目標にし続けないよう解除
            Squadron squadron = fs.GetComponent<Squadron>();
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength other = flagships[i];
                if (other == null || other == fs) continue;
                FleetWeapon w = other.GetComponent<FleetWeapon>();
                if (w != null && !FactionRelations.IsHostile(other, fs)) w.ClearManualTargetIfFleet(squadron);
            }

            AudioManager.Instance.PlayUIClick();
            ShowMessage($"{LabelOf(fs)}が寝返った！（→ {newLegacy}）", 4f);
        }

        /// <summary>side の戦う者が尽きていたら、その側の静観艦隊を戦わずして退かせる。</summary>
        private void WithdrawBystandersIfDecided(Faction side, Faction enemySide)
        {
            if (!BattleAllegianceRules.ShouldWithdraw(allegiances, side, enemySide)) return;
            for (int i = 0; i < allegiances.Count; i++)
            {
                Allegiance a = allegiances[i];
                if (a.locked || a.stance != Stance.静観 || a.side != side) continue;
                if (!fleetById.TryGetValue(a.id, out FleetStrength fs) || fs == null || !fs.IsAlive) continue;
                a.strength = 0;
                a.locked = true;
                fs.BeginRetreat(withEffects: false); // 撃たれて沈んだのではない＝爆発演出なし
                ShowMessage($"{LabelOf(fs)}は戦わずして戦場を去った……");
            }
        }

        /// <summary>艦隊の表示名（艦隊番号があれば「第N艦隊」、無ければ提督名）。</summary>
        private static string LabelOf(FleetStrength fs)
            => fs.HasFleetNumber ? fs.FleetLabel : $"{fs.admiralName}艦隊";

        /// <summary>この艦隊が現在プレイヤー陣営か（FactionData 優先・無ければ legacy enum）。</summary>
        private static bool IsPlayerSide(FleetStrength fs)
        {
            FactionData playerData = GameSettings.Instance.playerFactionData;
            if (playerData != null && fs.factionData != null) return fs.factionData == playerData;
            return fs.faction == GameSettings.Instance.playerFaction;
        }

        private void ShowMessage(string text, float duration = 3f)
        {
            if (hud == null) hud = Object.FindAnyObjectByType<FleetHUDManager>();
            if (hud != null) hud.ShowMessage(text, duration);
        }
    }
}
