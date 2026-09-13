using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 回廊要塞戦（#40）の<b>守備側AI</b>の配線（Battle シーンに自動生成・手置き不要）。
    /// <see cref="CorridorFortressArena"/> が在る戦場でだけ働き、守備側（要塞保持勢力）の艦隊に
    /// 「持ち場」と「追撃上限」を与える。数値判断は Core の <see cref="FortressDefenseAiRules"/> に委譲し、
    /// ここは<b>検出と配線だけ</b>を持つ（<see cref="BattlefieldCommandManager"/> が軍団スロットを配るのと同じ作法）。
    ///
    /// やること（ユーザー要求＝「要塞を活用した守備」）：
    /// 1. 要塞の砲火（支援範囲）の内側を基本守備位置にする。
    /// 2. 門前に横一線を張って突破ルートを塞ぐ（水路は迂回できないので抜けてくる線はここだけ）。
    /// 3. 深追いさせない（持ち場から離れすぎ／支援範囲外なら交戦より優先して復帰）。
    /// 4. 役割を分ける（迎撃／要塞直衛）。<b>最低1隊は直衛に残す＝要塞を空にしない</b>。
    /// 5. 攻撃側・通常会戦には一切触れない（アリーナが無ければ何もしない）。
    /// 6. 迂回不能の幾何（<see cref="CorridorArenaRules"/>）は読むだけで崩さない。
    ///
    /// 既存AIは改造しない：<see cref="FleetAI"/> の持ち場（corpsAnchor/corpsLeashRange）・
    /// 軍団スロット（corpsSlotLocal/corpsCommanderTf）・遮蔽命令（counterScreening）の受け皿へ値を渡すだけ。
    /// 手動命令（<see cref="FleetAI.ManualOverride"/>／<see cref="CorpsFormation.HasManualOrder"/>）が生きている隊には触れない。
    /// </summary>
    public class FortressDefenseDirector : MonoBehaviour
    {
        [Header("再評価")]
        [Tooltip("守備配置を再評価する間隔（秒）。毎フレーム再計算しない（終盤ラグ回避）")]
        public float evaluateInterval = 0.75f;

        [Header("要塞の支援範囲")]
        [Tooltip("要塞の支援（砲火）半径を直接指定する。0＝要塞の主砲射程から自動算出")]
        public float supportRadiusOverride = 0f;
        [Tooltip("近接支援（砲台群）の実効射程。封鎖半径に足した値を近接の支援範囲とみなす")]
        public float closeSupportMargin = 14f;

        [Header("持ち場（迎撃線／要塞直衛）")]
        [Tooltip("迎撃線を要塞の封鎖円から離す距離")]
        public float interceptMargin = 6f;
        [Tooltip("直衛を要塞の封鎖円から離す距離（迎撃線より内側に丸められる）")]
        public float guardMargin = 3f;
        [Tooltip("迎撃線の横の張り＝水路の半幅に対する割合（突破ルートを塞ぐ広さ）")]
        [Range(0f, 1f)] public float spreadRatio = 0.7f;
        [Tooltip("直衛の横の張り＝迎撃線の張りに対する割合（要塞の直近に固まる）")]
        [Range(0f, 1f)] public float guardSpanRatio = 0.35f;
        [Tooltip("直衛に回す隊の割合。最低1隊は必ず直衛（要塞を空にしない）")]
        [Range(0f, 1f)] public float guardRatio = 0.34f;
        [Tooltip("迎撃の出撃/追撃半径＝要塞の封鎖半径×これ。1未満＝封鎖円を割った敵だけ迎え撃つ")]
        [Range(0f, 1f)] public float interceptEngageRatio = 0.9f;
        [Tooltip("直衛の出撃/追撃半径＝要塞の封鎖半径×これ（迎撃を超えない）")]
        [Range(0f, 1f)] public float guardEngageRatio = 0.5f;
        [Tooltip("持ち場からこの距離ぶんの余裕を超えて離れたら復帰する")]
        public float returnSlack = 6f;
        [Tooltip("追撃半径の下限（支援範囲に食われても最低限は動ける）")]
        public float minPursuitRadius = 4f;
        [Tooltip("持ち場を突破線の手前で止める余白（守備が自分の出口の外へ出ない）")]
        public float exitStandoff = 4f;

        // ── ランタイム ──
        private float nextEvaluateTime;
        private Transform anchor;                 // 要塞位置の不動アンカー（要塞が落ちても残る＝スロットの基準）
        private bool warnedCorpsOverlap;

        private readonly List<FleetStrength> defenders = new List<FleetStrength>();
        private readonly List<FleetAI> commanded = new List<FleetAI>();       // いま指示している隊
        private readonly List<FleetAI> previous = new List<FleetAI>();        // 前回指示していた隊（解除用）
        private readonly Dictionary<FleetAI, float> savedLeash = new Dictionary<FleetAI, float>();

        // 決定論的な並び（同一性キーは EntityKey＝規約。GetInstanceID は使わない）。
        private static readonly System.Comparison<FleetStrength> ByEntityKey =
            (a, b) => EntityKey.Of(a).CompareTo(EntityKey.Of(b));

        // ────────────────────────────────────────────────
        // 自動生成（BattleSetup を変更せずに済むよう自前でブートストラップする）
        // ────────────────────────────────────────────────

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryCreate(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryCreate(scene);

        /// <summary>Battle シーンごとに1体だけ置く（ウィンドウ化会戦＝additive で複数戦場が同時に走るため）。</summary>
        private static void TryCreate(Scene scene)
        {
            if (scene.name != "Battle" || !scene.IsValid()) return;
            FortressDefenseDirector[] existing = FindObjectsByType<FortressDefenseDirector>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
                if (existing[i] != null && existing[i].gameObject.scene == scene) return;

            var go = new GameObject("FortressDefenseDirector");
            if (go.scene != scene) SceneManager.MoveGameObjectToScene(go, scene);
            go.AddComponent<FortressDefenseDirector>();
        }

        private void OnDestroy() => ReleaseAll();

        private void Update()
        {
            // timeScale 追従（ポーズで止まり倍速で速く回る）。毎フレームは回さない＝間隔で再評価してキャッシュ。
            if (Time.time < nextEvaluateTime) return;
            nextEvaluateTime = Time.time + Mathf.Max(0.1f, evaluateInterval);
            Evaluate();
        }

        // ────────────────────────────────────────────────
        // 評価（間隔ごと）
        // ────────────────────────────────────────────────

        private void Evaluate()
        {
            // ★この戦場が回廊要塞戦でなければ何もしない＝通常の会戦・攻撃側には一切影響しない。
            CorridorFortressArena arena = CorridorFortressArena.For(gameObject.scene);
            if (arena == null) { ReleaseAll(); return; }

            var bounds = new CorridorArenaRules.CorridorArenaBounds(
                arena.channelHalfWidth, arena.channelHalfLength,
                arena.fortressX, arena.fortressBlockRadius, arena.breakthroughX);

            // 要塞が扼している間だけ「直衛」の縛りが要る。落ちた／突破された後は縛りを解く（全隊が迎撃線へ）。
            bool holds = arena.FortressHolds && !arena.Breached;

            Vector2 origin = arena.transform.position;
            Vector2 fortressWorld = origin + new Vector2(arena.fortressX, 0f);
            EnsureAnchor(fortressWorld);

            float support = holds ? ResolveSupportRadius(arena) : 0f;
            var p = new FortressDefenseAiParams(support, interceptMargin, guardMargin, spreadRatio, guardSpanRatio,
                guardRatio, interceptEngageRatio, guardEngageRatio, returnSlack, minPursuitRadius, exitStandoff);

            CollectDefenders(arena);

            previous.Clear();
            previous.AddRange(commanded);
            commanded.Clear();

            float facing = FortressDefenseAiRules.DefenseFacingDeg(bounds);
            for (int i = 0; i < defenders.Count; i++)
            {
                FleetStrength fs = defenders[i];
                FleetAI ai = fs.GetComponent<FleetAI>();
                if (ai == null || !ai.enabled) continue;

                // 手動命令が生きている隊はプレイヤー優先＝この tick は触らない（命令完了で自動的に戻る）。
                if (ai.ManualOverride || CorpsFormation.HasManualOrder(CorpsFormation.KeyFor(fs)))
                {
                    Release(ai);
                    continue;
                }
                WarnIfCorpsControlled(fs);

                FortressDefenseRole role = FortressDefenseAiRules.RoleFor(i, defenders.Count, p.guardRatio, holds);
                Vector2 stationLocal = FortressDefenseAiRules.Station(i, defenders.Count, bounds, p, holds);
                Vector2 station = origin + stationLocal;
                float pursuit = FortressDefenseAiRules.PursuitRadius(role, bounds, p, holds);

                // ① 持ち場スロット（非接敵時はここへ就いて隊列を作る＝全員で突っ込まない）。
                //    基準は要塞位置のアンカー＝要塞が落ちても持ち場が消えない。
                ai.hasCorpsSlot = true;
                ai.corpsCommanderTf = anchor;
                ai.corpsFacingDeg = facing;                                        // 要塞の門（攻撃側）へ正対
                ai.corpsSlotLocal = FortressDefenseAiRules.StationLocal(stationLocal, bounds);

                // ② 持ち場の拘束＝深追い禁止（FleetAI の ApplyCorpsLeash が移動目標をこの円に丸める）＋
                //    出撃の引き金（EnemyNearCorps＝敵が要塞からこの距離に入ったら迎え撃つ）。
                ai.hasCorpsAnchor = true;
                ai.corpsAnchor = station;
                if (!savedLeash.ContainsKey(ai)) savedLeash[ai] = ai.corpsLeashRange; // 元の値は必ず戻せるよう控える
                ai.corpsLeashRange = pursuit;

                // ③ 復帰：持ち場から離れすぎ／要塞の支援外なら、交戦より優先して持ち場へ戻す。
                bool needReturn = FortressDefenseAiRules.ShouldReturnToStation(
                    fs.transform.position, station, fortressWorld, pursuit, support, p.returnSlack);
                ai.counterScreening = needReturn;
                if (needReturn) ai.counterScreenTarget = station;

                commanded.Add(ai);
            }

            // 今回の対象から外れた隊（撃沈・撤退・手動命令など）は拘束を解いて自律へ戻す。
            for (int i = 0; i < previous.Count; i++)
            {
                FleetAI ai = previous[i];
                if (ai == null || commanded.Contains(ai)) continue;
                Release(ai);
            }
        }

        /// <summary>守備側（要塞保持勢力に敵対しない）の生存戦闘艦隊をこの戦場から集める（決定論順）。</summary>
        private void CollectDefenders(CorridorFortressArena arena)
        {
            defenders.Clear();
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            if (flagships == null) return;
            Scene myScene = gameObject.scene;

            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength fs = flagships[i];
                if (fs == null || !fs.IsAlive) continue;
                if (fs.gameObject.scene != myScene) continue;            // 別戦場の艦は触らない（WIN-3）
                if (!fs.IsCombatant) continue;                            // 非戦闘艦は回避行動のまま（#128）
                // 敵対判定は FactionRelations が唯一の窓口（アリーナの blocked 判定と同じ式の裏返し）。
                if (FactionRelations.IsHostile(fs.factionData, fs.faction, null, arena.fortressOwner)) continue;
                defenders.Add(fs);
            }
            defenders.Sort(ByEntityKey);
        }

        /// <summary>要塞の支援（砲火）半径。主砲があればその射程、無ければ封鎖半径＋近接支援。</summary>
        private float ResolveSupportRadius(CorridorFortressArena arena)
        {
            if (supportRadiusOverride > 0f) return supportRadiusOverride;

            float mainRange = 0f;
            IReadOnlyList<FortressUnit> all = FortressRegistry.All;
            if (all != null)
            {
                Scene myScene = gameObject.scene;
                for (int i = 0; i < all.Count; i++)
                {
                    FortressUnit f = all[i];
                    if (f == null || !f.IsAlive) continue;
                    if (f.gameObject.scene != myScene) continue;
                    if (FactionRelations.IsHostile(null, f.Faction, null, arena.fortressOwner)) continue; // 守備側の要塞のみ
                    FortressMainCannon cannon = f.GetComponent<FortressMainCannon>();
                    if (cannon != null) mainRange = Mathf.Max(mainRange, cannon.maxRange);
                }
            }
            return FortressDefenseAiRules.SupportRadius(arena.fortressBlockRadius, closeSupportMargin, mainRange);
        }

        /// <summary>要塞位置に据える不動アンカー（持ち場スロットの基準）。要塞が陥落しても消えない。</summary>
        private void EnsureAnchor(Vector2 fortressWorld)
        {
            if (anchor == null)
            {
                var go = new GameObject("FortressDefenseAnchor");
                go.transform.SetParent(transform, false);
                anchor = go.transform;
            }
            anchor.position = new Vector3(fortressWorld.x, fortressWorld.y, 0f);
        }

        /// <summary>守備拘束を解いて自律行動へ戻す（元の持ち場距離も復元する）。</summary>
        private void Release(FleetAI ai)
        {
            if (ai == null) return;
            ai.hasCorpsSlot = false;
            ai.corpsCommanderTf = null;
            ai.hasCorpsAnchor = false;
            ai.counterScreening = false;
            if (savedLeash.TryGetValue(ai, out float leash))
            {
                ai.corpsLeashRange = leash;
                savedLeash.Remove(ai);
            }
        }

        private void ReleaseAll()
        {
            for (int i = 0; i < commanded.Count; i++) Release(commanded[i]);
            commanded.Clear();
            previous.Clear();
            savedLeash.Clear();
        }

        /// <summary>
        /// 守備艦隊が軍団（corpsName）に属していると <see cref="BattlefieldCommandManager"/> と持ち場を取り合う。
        /// 現状の回廊要塞マップ（BattleSetup.SetupCorridorFortress）は軍団名を与えないので起きないが、
        /// 将来そうなったときに黙って壊れないよう1度だけ警告する（統合担当への合図）。
        /// </summary>
        private void WarnIfCorpsControlled(FleetStrength fs)
        {
            if (warnedCorpsOverlap || fs == null || string.IsNullOrEmpty(fs.corpsName)) return;
            warnedCorpsOverlap = true;
            Debug.LogWarning("FortressDefenseDirector: 守備艦隊が軍団に属しています。" +
                             "BattlefieldCommandManager と持ち場（corpsSlot/corpsAnchor）を取り合う可能性があります。");
        }
    }
}
