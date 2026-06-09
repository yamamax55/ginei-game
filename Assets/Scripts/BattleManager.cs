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
            // デバッグ用：リスタート（入力は GameInput に集約・#107）
            if (GameInput.WasPressed(GameAction.リスタート))
            {
                RestartBattle();
                return;
            }

            // 戦略マップからの実会戦（C-2 二層遷移 #586 ②）：Backspace でいつでも戦略マップへ復帰。
            // 現時点の優勢側を勝者として結果を書き戻し、撤収する（離脱＝以後は自動委任）。
            if (BattleHandoff.Pending && !isBattleOver && GameInput.WasPressed(GameAction.戦略へ復帰))
            {
                isBattleOver = true;
                Time.timeScale = 0f;
                if (BattleHandoff.IsPlanetSiege) ReturnFromPlanetSiege(); // 攻城は戦略側で継続（決着は書き戻さない）
                else WriteHandoffResultAndReturn(LeadingFaction());
                return;
            }

            // 全 Start 完了後（＝レジストリ登録後）の最初の Update で開始時隻数・勝利条件・敵対状況を記録
            if (!initialized)
            {
                CountFleets(out initialImperialCount, out initialAllianceCount);
                CountInitialByFaction();
                ResolveScenarioAndVip();
                initialHadHostilePair = HasHostilePair(FleetRegistry.AllFlagships);
                initialized = true;
                if (FleetRegistry.AllFlagships.Count == 0)
                {
                    Debug.LogWarning("BattleManager: 開始時に艦隊が見つかりませんでした。");
                }
                // 戦略マップからの実会戦なら、いつでも復帰できることを通知（#586 ②）
                if (BattleHandoff.Pending)
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

            isBattleOver = true;

            // 決着時に時間を停止
            Time.timeScale = 0f;

            // 戦略マップからの実会戦（C-3）なら、結果を書き戻して戦略へ戻る
            if (BattleHandoff.Pending)
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
            IReadOnlyList<FleetStrength> alive = FleetRegistry.AllFlagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs != null && LegacyOf(fs) == winner) winnerTactical += fs.strength;
            }

            bool aWon = winner == BattleHandoff.factionA;
            int survivorStrategic = Mathf.Max(1, Mathf.RoundToInt(winnerTactical / (float)BattleHandoff.StrengthScale));
            BattleHandoff.SetResult(aWon, survivorStrategic);

            Time.timeScale = 1f; // 戦略へ戻すので通常速度へ
            SceneLoader.Instance.LoadScene(BattleHandoff.returnScene);
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
            if (arena != null)
                BattleHandoff.SetSiegeResult(arena.DefenseRatio, arena.InvasionRatio, arena.Captured);
            else
                BattleHandoff.SetSiegeResult(BattleHandoff.planetDefenseRatio, BattleHandoff.planetInvasionRatio, false);

            string ret = BattleHandoff.returnScene;
            Time.timeScale = 1f;
            SceneLoader.Instance.LoadScene(string.IsNullOrEmpty(ret) ? "Strategy" : ret);
        }

        /// <summary>
        /// 現時点で総兵力が多い側の legacy 陣営を返す（途中離脱＝Backspace 復帰時の暫定勝者）。
        /// 同数なら受け渡しの A 側（factionA）。
        /// </summary>
        private Faction LeadingFaction()
        {
            int a = 0, b = 0;
            IReadOnlyList<FleetStrength> alive = FleetRegistry.AllFlagships;
            for (int i = 0; i < alive.Count; i++)
            {
                FleetStrength fs = alive[i];
                if (fs == null) continue;
                if (LegacyOf(fs) == BattleHandoff.factionA) a += fs.strength;
                else if (LegacyOf(fs) == BattleHandoff.factionB) b += fs.strength;
            }
            return (b > a) ? BattleHandoff.factionB : BattleHandoff.factionA;
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

            // --- 殲滅（全条件共通の終了条件・多勢力対応）：敵対する旗艦ペアが残っていない ---
            if (!HasHostilePair(FleetRegistry.AllFlagships))
            {
                winnerRep = DetermineWinner(FleetRegistry.AllFlagships);
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
            activeScenario = ScenarioData.ActiveScenario;
            if (activeScenario == null)
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
        private static FleetStrength FindLivingFlagshipByAdmiral(AdmiralData admiral)
        {
            if (admiral == null) return null;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].admiralData == admiral) return all[i];
            }
            return null;
        }

        /// <summary>指定の旧 enum 陣営に属する生存旗艦の代表を1隻返す（勝者名/MVP算出用）。</summary>
        private static FleetStrength FindLivingFlagshipByLegacy(Faction f)
        {
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
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
        private static int CountLegacy(Faction f)
        {
            int n = 0;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
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
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
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
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
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

            IReadOnlyList<FleetStrength> alive = FleetRegistry.AllFlagships;
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
            IReadOnlyList<FleetStrength> alive = FleetRegistry.AllFlagships;
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
        }

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
