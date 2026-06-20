using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 会戦中の勝敗判定と戦績の記録を管理するクラス。
    /// 勝利条件（殲滅/時間防衛/旗艦撃破/護衛）を評価しつつ、敵対判定・勝者決定は
    /// 多勢力（FactionData）対応で一般化する（FactionRelations.IsHostile / 残存旗艦数）。
    /// </summary>
    public class BattleManager : MonoBehaviour
    {
        [Header("設定")]
        [Tooltip("勝利判定を行う間隔 (秒)")]
        public float checkInterval = 1.0f;

        private float nextCheckTime;
        private int initialImperialCount;
        private int initialAllianceCount;
        // 勢力名キーの開始時旗艦数（多勢力対応の戦績集計用）
        private readonly Dictionary<string, int> initialCountByFaction = new Dictionary<string, int>();
        private bool isBattleOver = false;
        private bool initialized = false;
        private bool initialHadHostilePair = false; // 開始時に敵対する旗艦同士が居たか（自動決着の前提）

        // 勝利条件評価用
        private ScenarioData activeScenario;     // この会戦の勝利条件・パラメータ
        private float battleElapsed = 0f;        // 会戦経過時間（timeScale 追従。ポーズで停止・倍速で加速）
        private Faction vipFaction;              // 旗艦撃破/護衛の対象VIPの陣営（開始時に解決）
        private bool vipResolved = false;        // VIPの陣営を解決できたか
        private float holdAccum = 0f;            // 拠点保持の連続保持秒数（#2259）

        /// <summary>
        /// この会戦シーンに属する旗艦のみ（WIN-2 #2569 隔離の核）。ウィンドウ化会戦は会戦ごとに別シーンへ
        /// additive ロードされるため、勝敗集計・初期化は自分のシーンの艦だけを数える（他会戦を巻き込まない）。
        /// 単一会戦（フルスクリーン）では当該シーン＝全旗艦なので従来と同一（後方互換）。
        /// </summary>
        private IReadOnlyList<FleetStrength> Flagships => FleetRegistry.FlagshipsIn(gameObject.scene);

        // WIN-3 #2570：この会戦専用の受け渡しスナップショット（複数同時で global BattleHandoff を奪い合わない）。
        // BattleDirector がロード完了時に注入する。null＝フルスクリーン会戦（global を直接使う＝従来動作）。
        private BattleHandoff.State ctx;
        /// <summary>BattleDirector が当該会戦の受け渡しスナップショットを注入する（WIN-3）。</summary>
        public void SetHandoffContext(BattleHandoff.State s) => ctx = s;
        private bool Windowed => ctx != null;
        // ウィンドウ化（additive）会戦か＝自分のシーンが active でない。フレーム0から信頼でき、ctx 注入や
        // フォーカス(BattleViewport.Active)に依存しない（時間/タイムスケールの分岐に使う）。
        private bool SceneWindowed => gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        private bool HPending => ctx != null ? ctx.Pending : BattleHandoff.Pending;
        private bool HSystemView => ctx != null ? ctx.IsSystemView : BattleHandoff.IsSystemView;
        private bool HPlanetSiege => ctx != null ? ctx.IsPlanetSiege : BattleHandoff.IsPlanetSiege;
        private Faction HFactionA => ctx != null ? ctx.factionA : BattleHandoff.factionA;
        private Faction HFactionB => ctx != null ? ctx.factionB : BattleHandoff.factionB;
        private string HReturnScene => ctx != null ? ctx.returnScene : BattleHandoff.returnScene;

        private void Start()
        {
            // 開始時にタイムスケールをリセット
            Time.timeScale = 1f;
            GameInput.SetContext(InputContext.会戦); // 入力コンテキストを会戦に（#107）
            AudioManager.Instance.PlayBGM(AudioManager.Instance.bgmBattle);
            // 開始時の隻数記録は、全艦の登録(Start)が済んだ最初の Update で行う（実行順非依存）
        }

        private void Update()
        {
            // TIME-5（#951）：会戦中も統一クロックを進める＝潜行/復帰で時間が止まらない（戦略と同一時間）。
            // 会戦の倍速(timeScale)をクロック速度へ写し、実時間で累積する（同一 static クロックを active シーンが進める）。
            // 戦果（残存兵力）は BattleHandoff で銀河へ還元され、時間は同一クロックで連続する。
            // ウィンドウ化会戦（WIN-1）では戦略マップが同居して同一クロックを進めるため、ここでは進めない（二重加算防止）。
            GameClock clock = StrategySession.Clock;
            if (clock != null && !SceneWindowed)
            {
                clock.speed = Mathf.Max(0f, Time.timeScale);
                clock.paused = Time.timeScale <= 0f;
                clock.Advance(Time.unscaledDeltaTime);
            }

            // システムビュー（非戦闘・恒星系の閲覧）：戦闘判定はせず、Backspace で戦略マップへ戻るだけ。
            if (HSystemView)
            {
                if (!isBattleOver && GameInput.WasPressed(GameAction.戦略へ復帰))
                {
                    isBattleOver = true;
                    ReturnToStrategyView();
                }
                return;
            }

            // デバッグ用：リスタート（入力は GameInput に集約・#107）
            if (GameInput.WasPressed(GameAction.リスタート))
            {
                RestartBattle();
                return;
            }

            // 戦略マップからの実会戦（C-2 二層遷移 #586 ②）：Backspace でいつでも戦略マップへ復帰。
            // 現時点の優勢側を勝者として結果を書き戻し、撤収する（離脱＝以後は自動委任）。
            if (HPending && !isBattleOver && GameInput.WasPressed(GameAction.戦略へ復帰))
            {
                LeaveToStrategy();
                return;
            }

            // 全 Start 完了後（＝レジストリ登録後）の最初の Update で開始時隻数・勝利条件・敵対状況を記録
            if (!initialized)
            {
                CountFleets(out initialImperialCount, out initialAllianceCount);
                CountInitialByFaction();
                ResolveScenarioAndVip();
                initialHadHostilePair = HasHostilePair(Flagships);
                initialized = true;
                if (Flagships.Count == 0)
                {
                    Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
                }
                // 戦略マップからの実会戦なら、いつでも復帰できることを通知（#586 ②）
                if (HPending)
                {
                    var hud = FindFirstObjectByType<FleetHUDManager>();
                    if (hud != null) hud.ShowMessage("Backspace：戦略マップへ復帰（以後は自動委任）", 5f);
                }
                return;
            }

            if (isBattleOver) return;

            // 開始時に敵対する旗艦が無い構成（単一勢力など）では自動決着しない
            if (!initialHadHostilePair) return;

            // 会戦経過時間を積算（timeScale 追従＝ポーズで止まり倍速で速く進む）
            battleElapsed += Time.deltaTime;

            if (Time.time >= nextCheckTime)
            {
                CheckVictory();
                nextCheckTime = Time.time + checkInterval;
            }
        }

        /// <summary>
        /// 勝利条件を評価し、決着していれば戦績を記録して結果画面へ遷移します。
        /// </summary>
        private void CheckVictory()
        {
            if (!EvaluateVictory(out Faction winner, out string reason, out FleetStrength winnerRep)) return;

            Debug.Log($"[WIN3] CheckVictory decided: scene={gameObject.scene.name}#{gameObject.scene.handle} winner={winner} reason={reason} flagships={Flagships.Count}");
            isBattleOver = true;

            // 決着時に時間を停止（ウィンドウ化会戦では全体時間を止めない＝戦略を凍結しない）
            if (!SceneWindowed) Time.timeScale = 0f;

            // 戦略マップからの実会戦（C-3）なら、結果を書き戻して戦略へ戻る
            if (HPending)
            {
                WriteHandoffResultAndReturn(winner);
                return;
            }

            RecordResults(winner, reason, winnerRep);

            // 結果画面へ遷移（非同期ロード中も時間は停止しているが、SceneLoaderがunscaledTimeを使う）
            SceneLoader.Instance.LoadScene("Result");
        }

        /// <summary>
        /// 実会戦の勝敗・勝者残存兵力を BattleHandoff に書き戻し、戦略シーンへ戻る（C-3）。
        /// 残存は戦術スケールの兵力を BattleHandoff.StrengthScale で戦略スケールへ逆算する。
        /// </summary>
        private void WriteHandoffResultAndReturn(Faction winner)
        {
            int winnerTactical = 0;
            IReadOnlyList<FleetStrength> alive = Flagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs != null && LegacyOf(fs) == winner) winnerTactical += fs.strength;
            }

            ReportProtagonistBattle(winner); // P1-a #2477：主人公の戦果を立身出世の武勲インボックスへ

            bool aWon = winner == HFactionA;
            int survivorStrategic = Mathf.Max(1, Mathf.RoundToInt(winnerTactical / (float)BattleHandoff.StrengthScale));

            Time.timeScale = 1f; // 戦略へ戻すので通常速度へ
            if (Windowed)
            {
                // 複数同時会戦（WIN-3）：自分の受け渡しへ結果を書き、結果キューへ積む（global を奪い合わない）。
                ctx.sideAWon = aWon; ctx.survivorStrength = survivorStrategic; ctx.Resolved = true;
                BattleResultQueue.Push(ctx);
                ReturnToStrategy(null);
            }
            else
            {
                BattleHandoff.SetResult(aWon, survivorStrategic);
                ReturnToStrategy(BattleHandoff.returnScene);
            }
        }

        /// <summary>
        /// 戦略へ戻る共通処理（WIN-1）。ウィンドウ化会戦では会戦ウィンドウを閉じてシーンをアンロードする
        /// （戦略は背後に生きているので結果は GalaxyView が BattleHandoff から反映する）。
        /// フルスクリーン会戦では従来どおり戦略シーンへ遷移する。
        /// </summary>
        private void ReturnToStrategy(string returnScene)
        {
            if (Windowed)
            {
                // 複数同時会戦（WIN-3）：自分のシーンのウィンドウを閉じてアンロード（戦略は背後に生存）。
                BattleDirector.NotifyBattleEnded(gameObject.scene);
                return;
            }
            SceneLoader.Instance.LoadScene(string.IsNullOrEmpty(returnScene) ? "Strategy" : returnScene);
        }

        /// <summary>
        /// 戦略マップへ離脱する（Backspace／会戦ウィンドウの ×）。現状の優勢側を勝者として書き戻す。
        /// 攻城は決着を書き戻さず戦略側で継続。
        /// </summary>
        public void LeaveToStrategy()
        {
            if (isBattleOver) return;
            isBattleOver = true;
            if (!SceneWindowed) Time.timeScale = 0f;
            if (!HPending) { ReturnToStrategy(HReturnScene); return; }
            if (HPlanetSiege) ReturnFromPlanetSiege(); // 攻城は戦略側で継続（決着は書き戻さない）
            else WriteHandoffResultAndReturn(LeadingFaction());
        }

        /// <summary>
        /// 惑星攻城の戦術マップから戦略マップへ戻る（#131）。攻城の決着は戦略側の TickSieges が継続するため
        /// 結果は書き戻さず、受け渡しをクリアして戻るだけ（観ていない間も攻城は抽象的に進む＝二層モデル）。
        /// </summary>
        private void ReturnFromPlanetSiege()
        {
            // 戦術マップでの攻城進捗（制空権/侵略値/占領）を割合で書き戻す（GalaxyView が惑星へ反映）。
            // arena が無くても必ず resolve して受け渡しを完結させる（Pending の残留防止）。
            SiegeArena arena = FindFirstObjectByType<SiegeArena>();
            float defR, invR, garR, garM; bool cap, surr;
            if (arena != null)
            {
                defR = arena.DefenseRatio; invR = arena.InvasionRatio; cap = arena.Captured;
                garR = arena.GarrisonRatio; garM = arena.GarrisonMoraleRatio; surr = arena.Surrendered;
            }
            else
            {
                defR = ctx != null ? ctx.planetDefenseRatio : BattleHandoff.planetDefenseRatio;
                invR = ctx != null ? ctx.planetInvasionRatio : BattleHandoff.planetInvasionRatio;
                garR = ctx != null ? ctx.planetGarrisonRatio : BattleHandoff.planetGarrisonRatio;
                garM = ctx != null ? ctx.planetGarrisonMorale : BattleHandoff.planetGarrisonMorale;
                cap = false; surr = false;
            }

            Time.timeScale = 1f;
            if (Windowed)
            {
                // 複数同時会戦（WIN-3）：攻城進捗を自分の受け渡しへ書いて結果キューへ（GalaxyView が惑星へ反映）。
                ctx.siegeResultDefense = Mathf.Clamp01(defR);
                ctx.siegeResultInvasion = Mathf.Clamp01(invR);
                ctx.siegeResultCaptured = cap;
                ctx.siegeResultGarrison = Mathf.Clamp01(garR);
                ctx.siegeResultMorale = Mathf.Clamp01(garM);
                ctx.siegeResultSurrendered = surr;
                ctx.siegeResolved = true;
                BattleResultQueue.Push(ctx);
                ReturnToStrategy(null);
            }
            else
            {
                BattleHandoff.SetSiegeResult(defR, invR, cap, garR, garM, surr);
                ReturnToStrategy(BattleHandoff.returnScene);
            }
        }

        /// <summary>
        /// 非戦闘のシステムビューから戦略マップへ戻る。戦闘結果は無いので受け渡しをクリアして戻るだけ。
        /// </summary>
        private void ReturnToStrategyView()
        {
            Time.timeScale = 1f;
            if (Windowed)
            {
                // システムビューは結果が無い＝何も積まずに窓を閉じるだけ。
                ReturnToStrategy(null);
                return;
            }
            BattleHandoff.Clear();
            SceneLoader.Instance.LoadScene(string.IsNullOrEmpty(BattleHandoff.returnScene) ? "Strategy" : BattleHandoff.returnScene);
        }

        /// <summary>
        /// 現時点で総兵力が多い側の legacy 陣営を返す（途中離脱＝Backspace 復帰時の暫定勝者）。
        /// 同数なら受け渡しの A 側（factionA）。
        /// </summary>
        private Faction LeadingFaction()
        {
            int a = 0, b = 0;
            IReadOnlyList<FleetStrength> alive = Flagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == HFactionA) a += fs.strength;
                else if (LegacyOf(fs) == HFactionB) b += fs.strength;
            }
            return (b > a) ? HFactionB : HFactionA;
        }

        /// <summary>
        /// シナリオの勝利条件に従って決着を評価する。決着していれば true＋勝者・勝因・勝者代表旗艦を返す。
        /// 終了の汎用条件は「敵対する旗艦のペアが残っていない」（多勢力対応の殲滅）。
        /// </summary>
        private bool EvaluateVictory(out Faction winner, out string reason, out FleetStrength winnerRep)
        {
            winner = Faction.同盟;
            reason = "";
            winnerRep = null;

            int imp = CountLegacy(Faction.帝国);
            int all = CountLegacy(Faction.同盟);

            VictoryCondition cond = activeScenario != null ? activeScenario.victoryCondition : VictoryCondition.殲滅;

            // --- 旗艦撃破 / 護衛：対象VIP旗艦の生死・時間で先に決着しうる ---
            if ((cond == VictoryCondition.旗艦撃破 || cond == VictoryCondition.護衛)
                && activeScenario != null && activeScenario.targetAdmiral != null && vipResolved)
            {
                AdmiralData vip = activeScenario.targetAdmiral;
                bool vipAlive = FindLivingFlagshipByAdmiral(vip) != null;

                if (!vipAlive)
                {
                    // VIP喪失 → 反対陣営の勝利
                    winner = Opposite(vipFaction);
                    reason = (cond == VictoryCondition.護衛)
                        ? $"護衛対象「{vip.FullName}」を喪失"
                        : $"敵旗艦「{vip.FullName}」を撃破";
                    winnerRep = FindLivingFlagshipByLegacy(winner);
                    return true;
                }

                // VIP生存かつ時間切れ → VIP陣営（守備側）の勝利
                if (activeScenario.timeLimit > 0f && battleElapsed >= activeScenario.timeLimit)
                {
                    winner = vipFaction;
                    reason = (cond == VictoryCondition.護衛)
                        ? "護衛成功（制限時間まで守り切った）"
                        : $"旗艦「{vip.FullName}」を制限時間まで守り切った";
                    winnerRep = FindLivingFlagshipByLegacy(winner);
                    return true;
                }
            }

            // --- 時間防衛：防衛側が制限時間まで生存で勝利 ---
            if (cond == VictoryCondition.時間防衛 && activeScenario != null)
            {
                Faction defender = activeScenario.objectiveFaction;
                int defenderCount = (defender == Faction.帝国) ? imp : all;
                if (defenderCount > 0 && activeScenario.timeLimit > 0f && battleElapsed >= activeScenario.timeLimit)
                {
                    winner = defender;
                    reason = "時間防衛成功";
                    winnerRep = FindLivingFlagshipByLegacy(winner);
                    return true;
                }
            }

            // --- 突破：objectiveFaction の旗艦が戦場端(battlefieldRadius)に到達したら勝利（#2259）---
            if (cond == VictoryCondition.突破 && activeScenario != null && activeScenario.battlefieldRadius > 0f)
            {
                Faction breaker = activeScenario.objectiveFaction;
                float radius = activeScenario.battlefieldRadius;
                foreach (var fs in Flagships)
                {
                    if (!CountsForVictory(fs)) continue;
                    if (LegacyOf(fs) != breaker) continue;
                    if (VictoryRules.BreakthroughAchieved((Vector2)fs.transform.position, radius))
                    {
                        winner = breaker;
                        reason = "突破成功（戦場端に到達）";
                        winnerRep = fs;
                        return true;
                    }
                }
            }

            // --- 拠点保持：objectiveFaction が objectivePoint 周辺を holdDuration 秒保持で勝利（#2259）---
            if (cond == VictoryCondition.拠点保持 && activeScenario != null)
            {
                Faction holder = activeScenario.objectiveFaction;
                Vector2 center = activeScenario.objectivePoint;
                float radius = activeScenario.objectiveRadius;
                float needed = activeScenario.holdDuration;
                bool holding = false;
                foreach (var fs in Flagships)
                {
                    if (!CountsForVictory(fs)) continue;
                    if (LegacyOf(fs) != holder) continue;
                    if (VictoryRules.IsInZone((Vector2)fs.transform.position, center, radius)) { holding = true; break; }
                }
                holdAccum = holding ? holdAccum + Time.deltaTime : 0f; // ゾーン離脱でリセット
                if (VictoryRules.HoldAchieved(holdAccum, needed))
                {
                    winner = holder;
                    reason = $"拠点保持成功（{needed:F0}秒保持）";
                    winnerRep = FindLivingFlagshipByLegacy(holder);
                    return true;
                }
            }

            // --- 殲滅（全条件共通の終了条件・多勢力対応）：敵対する旗艦ペアが残っていない ---
            if (!HasHostilePair(Flagships))
            {
                winnerRep = DetermineWinner(Flagships);
                if (winnerRep == null)
                {
                    winner = Faction.同盟; // 全旗艦喪失＝便宜上の勝者
                    reason = "両軍壊滅";
                }
                else
                {
                    winner = LegacyOf(winnerRep);
                    reason = "敵旗艦全滅";
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// この会戦のシナリオ（勝利条件）と、旗艦撃破/護衛の対象VIPの陣営を解決する。
        /// </summary>
        private void ResolveScenarioAndVip()
        {
            // Handoff 経由（潜行・複数同時）は創発会戦＝殲滅判定。static な ActiveScenario（他会戦の
            // ロードで上書きされうる）を引き継がない（WIN-3：別会戦の勝利条件で即決着するのを防ぐ）。
            activeScenario = HPending ? null : ScenarioData.ActiveScenario;
            // 戦略マップからの実会戦（潜行・複数艦隊・攻城など Handoff 経由）は**創発的な艦隊戦**なので、
            // 直前に遊んだ単発シナリオの勝利条件（旗艦撃破/護衛/時間防衛 等）を引き継がない＝殲滅で判定する。
            // 以前は scenarioName に残った別シナリオを Resolve してしまい、対象VIPが居らず**会戦が即決着**していた。
            if (activeScenario == null && !HPending)
            {
                activeScenario = ScenarioData.Resolve(GameSettings.Instance.scenarioName);
            }

            vipResolved = false;
            if (activeScenario != null && activeScenario.targetAdmiral != null)
            {
                FleetStrength vipFlag = FindLivingFlagshipByAdmiral(activeScenario.targetAdmiral);
                // 実際の陣営はシナリオで上書きされ得るので、生存中の旗艦から確定する。
                // 開始時に見つからなければ提督データの陣営をフォールバックに使う。
                vipFaction = (vipFlag != null) ? LegacyOf(vipFlag) : activeScenario.targetAdmiral.faction;
                vipResolved = true;
            }
        }

        /// <summary>指定 AdmiralData を持つ生存旗艦を探す（退却・破棄済みは登録外なので見つからない＝撃破扱い）。</summary>
        private FleetStrength FindLivingFlagshipByAdmiral(AdmiralData admiral)
        {
            if (admiral == null) return null;
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].admiralData == admiral) return all[i];
            }
            return null;
        }

        /// <summary>指定の旧 enum 陣営に属する生存旗艦の代表を1隻返す（勝者名/MVP算出用）。</summary>
        private FleetStrength FindLivingFlagshipByLegacy(Faction f)
        {
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (CountsForVictory(fs) && LegacyOf(fs) == f) return fs;
            }
            return null;
        }

        /// <summary>陣営の反対側を返す。</summary>
        private static Faction Opposite(Faction f) => (f == Faction.帝国) ? Faction.同盟 : Faction.帝国;

        /// <summary>勝敗カウントの対象か（生存中の戦闘艦のみ。非戦闘艦#128は残存判定から除外）。</summary>
        private static bool CountsForVictory(FleetStrength fs) => fs != null && fs.IsAlive && fs.IsCombatant;

        /// <summary>生存戦闘旗艦の中に敵対するペアが1組でもあるか（非戦闘艦は戦線を作らない＝除外）。</summary>
        private static bool HasHostilePair(IReadOnlyList<FleetStrength> flagships)
        {
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength a = flagships[i];
                if (!CountsForVictory(a)) continue;
                for (int j = i + 1; j < flagships.Count; j++)
                {
                    FleetStrength b = flagships[j];
                    if (!CountsForVictory(b)) continue;
                    if (FactionRelations.IsHostile(a, b)) return true;
                }
            }
            return false;
        }

        /// <summary>旧 enum Faction に正規化（FactionData があればその legacyFaction）。</summary>
        private static Faction LegacyOf(FleetStrength fs)
            => fs.factionData != null ? fs.factionData.legacyFaction : fs.faction;

        /// <summary>2 旗艦が同一勢力か（FactionData 優先、無ければ enum）。</summary>
        private static bool SameFaction(FleetStrength a, FleetStrength b)
        {
            if (a.factionData != null && b.factionData != null) return a.factionData == b.factionData;
            if (a.factionData == null && b.factionData == null) return a.faction == b.faction;
            return false;
        }

        /// <summary>勝者勢力の代表旗艦（残存戦闘旗艦数が最多、同数なら残存兵力が最大の勢力）。全滅なら null。非戦闘艦#128は除外。</summary>
        private static FleetStrength DetermineWinner(IReadOnlyList<FleetStrength> alive)
        {
            FleetStrength best = null;
            int bestCount = -1, bestStrength = -1;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength rep = alive[i];
                if (!CountsForVictory(rep)) continue; // 非戦闘艦は勝者代表にしない
                int count = 0, strength = 0;
                for (int j = 0; j < alive.Count; j++)
                {
                    FleetStrength other = alive[j];
                    if (!CountsForVictory(other) || !SameFaction(rep, other)) continue;
                    count++; strength += other.strength;
                }
                if (count > bestCount || (count == bestCount && strength > bestStrength))
                {
                    bestCount = count; bestStrength = strength; best = rep;
                }
            }
            return best;
        }

        /// <summary>勝者勢力の生存旗艦で与ダメージ最大の提督名。</summary>
        private static string FindMvpAdmiral(IReadOnlyList<FleetStrength> alive, FleetStrength winnerRep)
        {
            FleetStrength best = null;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null || winnerRep == null || !SameFaction(winnerRep, fs)) continue;
                if (best == null || fs.DamageDealt > best.DamageDealt) best = fs;
            }
            if (best == null) return "";
            return best.admiralData != null ? best.admiralData.FullName : best.admiralName;
        }

        /// <summary>指定の旧 enum 陣営に属する生存旗艦数。</summary>
        private int CountLegacy(Faction f)
        {
            int n = 0;
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs != null && LegacyOf(fs) == f) n++;
            }
            return n;
        }

        private void CountFleets(out int imperial, out int alliance)
        {
            // 旧 enum バケツ別の生存旗艦数（後方互換の戦績集計用）
            imperial = 0; alliance = 0;
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == Faction.帝国) imperial++; else alliance++;
            }
        }

        /// <summary>開始時の勢力名キー別旗艦数を記録する（多勢力対応の戦績の基準）。</summary>
        private void CountInitialByFaction()
        {
            initialCountByFaction.Clear();
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs == null) continue;
                Increment(initialCountByFaction, FactionKey(fs), 1);
            }
        }

        /// <summary>旗艦の勢力名キー（FactionData.factionName 優先、無ければ enum 名）。</summary>
        private static string FactionKey(FleetStrength fs)
            => (fs.factionData != null && !string.IsNullOrEmpty(fs.factionData.factionName))
                ? fs.factionData.factionName
                : fs.faction.ToString();

        /// <summary>辞書の key に amount を加算する（未登録なら新規）。</summary>
        private static void Increment(Dictionary<string, int> dict, string key, int amount)
        {
            dict.TryGetValue(key, out int cur);
            dict[key] = cur + amount;
        }

        /// <summary>
        /// 勢力名キー別の戦績（残存旗艦数・残存兵力・喪失数）を GameSettings.factionStats に記録する。
        /// 喪失数は開始時数 - 残存数。退却・破棄された旗艦はレジストリ外なので残存に数えない。
        /// </summary>
        private void RecordFactionStats(GameSettings settings)
        {
            Dictionary<string, int> remCount = new Dictionary<string, int>();
            Dictionary<string, int> remStrength = new Dictionary<string, int>();

            IReadOnlyList<FleetStrength> alive = Flagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null) continue;
                string key = FactionKey(fs);
                Increment(remCount, key, 1);
                Increment(remStrength, key, fs.strength);
            }

            settings.factionStats.Clear();

            // 開始時に存在した全勢力を基準に集計（残存ゼロでも喪失として出す）
            foreach (var kv in initialCountByFaction)
            {
                remCount.TryGetValue(kv.Key, out int rc);
                remStrength.TryGetValue(kv.Key, out int rs);
                settings.factionStats.Add(new GameSettings.FactionStat
                {
                    factionName = kv.Key,
                    initialCount = kv.Value,
                    remainingCount = rc,
                    sunkCount = Mathf.Max(0, kv.Value - rc),
                    remainingStrength = rs
                });
            }

            // 念のため：開始時に居なかったが残存している勢力があれば追加
            foreach (var kv in remCount)
            {
                if (initialCountByFaction.ContainsKey(kv.Key)) continue;
                remStrength.TryGetValue(kv.Key, out int rs);
                settings.factionStats.Add(new GameSettings.FactionStat
                {
                    factionName = kv.Key,
                    initialCount = kv.Value,
                    remainingCount = kv.Value,
                    sunkCount = 0,
                    remainingStrength = rs
                });
            }
        }

        /// <summary>
        /// 戦績を GameSettings に保存します（勝者・勝者名・喪失数・残存兵力・MVP・勝因）。
        /// 勝者・勝因は勝利条件評価(EvaluateVictory)の結果を受け取り、勝者名/MVPは多勢力対応で算出する。
        /// </summary>
        private void RecordResults(Faction winner, string reason, FleetStrength winnerRep)
        {
            GameSettings settings = GameSettings.Instance;

            // 後方互換の enum 別集計（帝国/同盟バケツ）
            int impRem = 0, allRem = 0, impStr = 0, allStr = 0;
            IReadOnlyList<FleetStrength> alive = Flagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == Faction.帝国) { impRem++; impStr += fs.strength; }
                else { allRem++; allStr += fs.strength; }
            }

            settings.winner = winner;
            settings.winnerName = (winnerRep != null)
                ? (winnerRep.factionData != null ? winnerRep.factionData.factionName : winnerRep.faction.ToString())
                : winner.ToString();
            settings.imperialSunkCount = initialImperialCount - impRem;
            settings.allianceSunkCount = initialAllianceCount - allRem;
            settings.remainingStrength = impStr + allStr;
            settings.imperialRemainingStrength = impStr;
            settings.allianceRemainingStrength = allStr;

            // MVP：勝者勢力の生存旗艦で与ダメージ最大の提督
            settings.mvpAdmiral = (winnerRep != null) ? FindMvpAdmiral(alive, winnerRep) : "";

            // 勝因（勝利条件の評価結果）
            settings.victoryReason = string.IsNullOrEmpty(reason) ? "敵旗艦全滅" : reason;

            // 勢力名キー別の戦績（多勢力対応。ResultManager が勢力数可変で表示）
            RecordFactionStats(settings);

            // #2260 会戦結果のメタ反映：生存旗艦の提督に会戦経験値を付与（純ロジックは BattleMetaRules）。
            ApplyBattleExperience(winner);
        }

        /// <summary>
        /// 会戦終了時に各提督へ経験値を付与する（#2260 最小配線・実データ永続は Growth 永続化後に拡張）。
        /// </summary>
        private void ApplyBattleExperience(Faction winnerFaction)
        {
            ReportProtagonistBattle(winnerFaction); // P1-a #2477：主人公の戦果を立身出世の武勲インボックスへ
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs == null || fs.admiralData == null) continue;

                bool isWinner = (LegacyOf(fs) == winnerFaction);
                float amount = BattleMetaRules.ExperienceFromBattle(fs.DamageDealt, 0, isWinner);
                if (amount <= 0f) continue;

                // 会戦で得た経験を id キーの成長台帳へ蓄える（P1-b #2477＝捨てない）。基準能力は AdmiralData、経験はここ。
                // 共有 ScriptableObject(AdmiralData) を実行時に書き換えず、GetInstanceID キーで分離（MedalRegistry と同型）。
                GrowthArchetype arch = fs.admiralData.growth != null ? fs.admiralData.growth.archetype : GrowthArchetype.叩き上げ;
                GrowthRegistry.GainExperience(fs.admiralData.GetInstanceID(), arch, amount, dt: 1f);

                // #2263 叙勲：戦功（与ダメ＋勝利）に応じて武功章を授与。次戦の士気底上げ（名誉）へ繋がる。
                float merit = Mathf.Clamp(fs.DamageDealt / MedalMeritScale, 0f, 100f) + (isWinner ? MedalWinnerMeritBonus : 0f);
                if (merit >= MedalAwardThreshold)
                {
                    int admiralId = fs.admiralData.GetInstanceID();
                    Decoration d = MedalRegistry.Award(admiralId, MedalKind.武功章, merit, 0, $"{currentName(fs)} の戦功");
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.情報,
                        $"{fs.admiralName} に武功章 {d.grade} を叙勲（戦功）");
                }
            }
        }

        /// <summary>
        /// 主人公（<see cref="AdmiralData.isProtagonist"/> もしくは選択提督名一致）の艦隊が会戦に居れば、その戦果
        /// （与ダメ＋勝敗）を立身出世の武勲インボックス（<see cref="ProtagonistCareerDirector.ReportBattle"/>）へ積む（P1-a #2477）。
        /// 戦略へ戻った後の月次評定で武勲・主命達成へ変換される＝出世が会戦の結果で駆動する。
        /// </summary>
        private void ReportProtagonistBattle(Faction winner)
        {
            GameSettings gs = GameSettings.Instance;
            string heroName = gs != null ? gs.selectedAdmiral : null;
            IReadOnlyList<FleetStrength> all = Flagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (fs == null || fs.admiralData == null) continue;
                bool isHero = fs.admiralData.isProtagonist ||
                              (!string.IsNullOrEmpty(heroName) && fs.admiralData.admiralName == heroName);
                if (!isHero) continue;
                ProtagonistCareerDirector.ReportBattle(fs.DamageDealt, LegacyOf(fs) == winner);
                break; // 主人公は1名
            }
        }

        // 叙勲の調整値（#2263）。
        private const float MedalMeritScale = 200f;       // 与ダメ→戦功スコアの正規化（20000ダメで満点付近）
        private const float MedalWinnerMeritBonus = 15f;  // 勝利側の戦功加点
        private const float MedalAwardThreshold = 30f;    // この戦功以上で叙勲（乱発防止）

        private static string currentName(FleetStrength fs) => fs != null ? fs.admiralName : "";

        /// <summary>
        /// 会戦を最初からやり直します。
        /// </summary>
        public void RestartBattle()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
