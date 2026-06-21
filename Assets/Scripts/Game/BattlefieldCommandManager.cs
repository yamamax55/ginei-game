using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 会戦中の臨時指揮（#147 拡張・史実準拠）の配線（Battle シーンに自動生成・手置き不要）。
    /// 軍団（`FleetStrength.corpsName`）ごとに生存旗艦から「最上位階級→先任」の指揮官を解決し（`BattlefieldCommandRules`）、
    /// <b>軍団長が戦死/離脱したら下位（階級不足でも）が臨時で継承</b>して通知する。
    /// 戦闘終了（Battle シーン離脱＝本オブジェクト破棄）で `ActingCommandLedger.Clear`＝臨時指揮を解いて正規人事へ戻す。
    /// ※艦隊内（副提督→提督）の継承は `CommandStaffRules`#885 が担う。本クラスは軍団以上の臨時指揮を扱う。
    /// </summary>
    public class BattlefieldCommandManager : MonoBehaviour
    {
        [Tooltip("臨時指揮を解決する間隔（秒）")]
        public float resolveInterval = 1.0f;

        private float nextResolveTime;

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
            if (scene.name != "Battle") return;
            if (FindAnyObjectByType<BattlefieldCommandManager>() != null) return;
            new GameObject("BattlefieldCommandManager").AddComponent<BattlefieldCommandManager>();
        }

        // 軍団ごとの直近の発令陣形（変化時のみ通知＝洪水回避・#軍団長の陣形変更命令）。
        private static readonly Dictionary<string, Formation> lastCorpsFormation = new Dictionary<string, Formation>();

        // 軍団スロット間隔（配下艦の非重複・#軍団集結）。spacing = 2×最大footprint＋余白、最低でも下限。
        private const float CorpsSpacingMargin = 3f;
        private const float CorpsMinSpacing = 7f;

        // 軍団長の前進保留（集結優先・#軍団集結）。隷下がスロットへこの許容範囲内に就けば「整った」とみなす。
        private const float CorpsFormTolerance = 1.5f;     // 許容＝spacing×この係数
        private const float CorpsFormMinTolerance = 8f;    // 許容の下限（spacing が小さくても確保）
        private const float CorpsFormUpAbortRange = 45f;   // 敵がこの距離まで迫れば整列を待たず前進・交戦へ

        private void Awake()
        {
            ActingCommandLedger.Clear();   // 新しい会戦＝臨時指揮をリセット
            lastCorpsFormation.Clear();    // 軍団陣形の通知履歴もリセット
        }

        private void OnDestroy() => ActingCommandLedger.Clear(); // 戦闘終了（シーン離脱）＝正規人事へ戻す

        private void Update()
        {
            if (Time.time < nextResolveTime) return;
            nextResolveTime = Time.time + Mathf.Max(0.1f, resolveInterval);
            ResolveActingCommands();
        }

        /// <summary>軍団ごとに生存旗艦から臨時指揮官を解決し、交代があれば通知する。</summary>
        private void ResolveActingCommands()
        {
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            if (flagships == null) return;

            // 軍団（corpsName）ごとに生存旗艦を集める。
            var groups = new Dictionary<string, List<FleetStrength>>();
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength f = flagships[i];
                if (f == null || !f.IsAlive || string.IsNullOrEmpty(f.corpsName)) continue;
                string key = f.faction + "/" + f.corpsName;
                if (!groups.TryGetValue(key, out var list)) { list = new List<FleetStrength>(); groups[key] = list; }
                list.Add(f);
            }
            if (groups.Count == 0) return;

            int requiredTier = CommandCapacityRules.CommanderTierFor(EchelonType.軍団);
            foreach (var kv in groups)
            {
                var list = kv.Value;
                var candidates = new List<CommandCandidate>(list.Count);
                for (int i = 0; i < list.Count; i++)
                {
                    int tier = list[i].admiralData != null ? list[i].admiralData.rankTier : 0;
                    candidates.Add(new CommandCandidate(list[i].GetInstanceID(), tier, 0));
                }

                // 軍団長のバフ/デバフ（CSG）：軍団旗艦に乗艦している軍団長の統率で軍団全体の能力・士気を上下。
                ApplyCorpsCommanderBuff(list);

                // 軍団長が陣形を主導し（#持ち場・#軍団集結）、隷下艦隊に持ち場スロット（軍団長旗艦基準）を与えてまとまらせる。
                ApplyCorpsFormationAndPost(kv.Key, list);

                var acting = BattlefieldCommandRules.SelectActingSuccessor(candidates);
                if (acting.id < 0) continue;

                int prev = ActingCommandLedger.ActingFor(kv.Key);
                ActingCommandLedger.Record(kv.Key, acting.id, acting.id);

                if (prev != -1 && prev != acting.id)
                {
                    // 上官（軍団長）を失い、先任の下位が臨時で指揮を継承。
                    FleetStrength newCmd = FindById(list, acting.id);
                    string name = (newCmd != null && newCmd.admiralData != null) ? newCmd.admiralData.admiralName : "次席";
                    bool underRank = acting.rankTier < requiredTier;
                    NotificationCenter.Push(NotificationCategory.人事, NotificationSeverity.注意,
                        $"{kv.Key} の指揮を {name} が継承{(underRank ? "（階級不足ながら臨時）" : "（臨時）")}");
                }
            }
        }

        /// <summary>
        /// 軍団旗艦（軍団長乗艦）の軍団長の統率から、軍団全体（同 corpsName の生存旗艦）の能力・士気バフ/デバフを設定する。
        /// 軍団長が乗艦していなければ等倍（軍団旗艦喪失は指揮空白デバフ）。CSG＝打撃群指揮官の影響。
        /// </summary>
        private static void ApplyCorpsCommanderBuff(List<FleetStrength> corpsFleets)
        {
            AdmiralData cc = null;
            for (int i = 0; i < corpsFleets.Count; i++)
                if (corpsFleets[i] != null && corpsFleets[i].IsCorpsFlagship) { cc = corpsFleets[i].corpsCommander; break; }

            // 軍団長が乗艦していなければ等倍（軍団長を持たない軍団を不当にデバフしない）。
            float ability = 1f, morale = 1f;
            if (cc != null)
            {
                float lead = cc.EffectiveLeadership;
                ability = CorpsCommandRules.AbilityFactor(lead);
                morale = CorpsCommandRules.MoraleFactor(lead);
            }

            for (int i = 0; i < corpsFleets.Count; i++)
            {
                FleetStrength f = corpsFleets[i];
                if (f == null) continue;
                f.corpsAbilityFactor = ability;
                f.corpsMoraleFactor = morale;
            }
        }

        /// <summary>
        /// 軍団長が陣形を主導し、隷下艦隊に「持ち場」（軍団長旗艦の位置）を与える（#持ち場）。
        /// 軍団全体の兵力と近傍の敵兵力から <see cref="FormationDoctrineRules"/> で推奨陣形を決め、軍団長が
        /// 各艦隊へ発令する（陣形変更は各艦隊の指揮スキルポイントを消費＝#陣形コスト）。隷下は陣形を自己判断せず
        /// （corpsControlled）、軍団長旗艦から離れすぎない（hasCorpsAnchor）。<b>プレイヤー軍団も開始時は自動集結する</b>
        /// （#軍団集結 設計：開始時は自動集結／命令で上書き）。ただし手動命令中（manualOverride）の艦はこの tick は触らず
        /// プレイヤー命令を優先する（命令完了で AI＝集結へ復帰）。軍団長（軍団旗艦）を喪失した軍団は拘束を解いて各自で戦う。
        /// </summary>
        private static void ApplyCorpsFormationAndPost(string corpsKey, List<FleetStrength> corpsFleets)
        {
            // 軍団長（軍団旗艦＝軍団長乗艦）を探す。
            FleetStrength commander = null;
            for (int i = 0; i < corpsFleets.Count; i++)
                if (corpsFleets[i] != null && corpsFleets[i].IsCorpsFlagship) { commander = corpsFleets[i]; break; }

            // 軍団長不在（旗艦喪失）：拘束を解いて各艦隊は自律行動へ戻す。次の再編で再通知できるよう履歴も消す。
            if (commander == null)
            {
                for (int i = 0; i < corpsFleets.Count; i++) ReleaseFromCorps(corpsFleets[i]);
                lastCorpsFormation.Remove(corpsKey);
                return;
            }

            FleetAI cmdAi = commander.GetComponent<FleetAI>();

            // 隷下（軍団長を除く）を集める。プレイヤー軍団も対象＝開始時は自動集結する（#軍団集結 設計：
            // 開始時は自動集結／命令で上書き）。手動命令中（manualOverride）の艦は下のループ/保留判定で個別に除外する。
            var subs = new List<FleetStrength>();
            for (int i = 0; i < corpsFleets.Count; i++)
            {
                FleetStrength f = corpsFleets[i];
                if (f == null || f == commander) continue;
                subs.Add(f);
            }

            // 軍団の総兵力 vs 近傍の敵兵力 → 推奨陣形（軍団長の能力で判断）。
            float corpsOwn = Mathf.Max(0f, commander.strength);
            for (int i = 0; i < subs.Count; i++) corpsOwn += Mathf.Max(0f, subs[i].strength);
            FleetStrength nearest = NearestHostile(commander);
            float enemyStr = nearest != null ? Mathf.Max(1f, nearest.strength) : 1f;
            FleetMorale cmdMorale = commander.GetComponent<FleetMorale>();
            bool routed = cmdMorale != null && cmdMorale.IsRouted;
            AdmiralData decider = commander.corpsCommander != null ? commander.corpsCommander : commander.admiralData;
            Formation rec = FormationDoctrineRules.RecommendFormation(corpsOwn, enemyStr, routed, decider);

            // 軍団スロット間隔（#軍団集結 非重複）：隣の艦隊の配下艦と重ならないよう、各艦隊の占有半径から決める。
            float maxFoot = FootprintOf(commander);
            for (int i = 0; i < subs.Count; i++) maxFoot = Mathf.Max(maxFoot, FootprintOf(subs[i]));
            float spacing = Mathf.Max(CorpsMinSpacing, 2f * maxFoot + CorpsSpacingMargin);

            // 軍団の正面（敵方向）と、軍団スロット（軍団長を含む全艦隊ぶん）。ジオメトリは Core に委譲。
            float facingDeg = FacingDeg(commander, nearest);
            int fleetCount = subs.Count + 1;
            List<CorpsSlot> slots = CorpsFormationRules.ComputeSlots(fleetCount, rec, spacing);
            Vector2 commanderLocal = Vector2.zero;
            for (int i = 0; i < slots.Count; i++) if (slots[i].commander) { commanderLocal = slots[i].localPos; break; }
            Vector2 anchor = commander.transform.position;

            // 軍団長：陣形を発令し、隊を率いる（スロット拘束なし）。手動命令中はプレイヤー優先で陣形を被せない。
            bool cmdManual = cmdAi != null && cmdAi.ManualOverride;
            Squadron cSq = commander.GetComponent<Squadron>();
            if (cSq != null && !cmdManual) cSq.TryChangeFormation(rec);
            if (cmdAi != null) { cmdAi.corpsControlled = true; cmdAi.hasCorpsAnchor = false; cmdAi.hasCorpsSlot = false; }

            // 隷下：軍団長が陣形を発令し、軍団長基準のスロットへ就かせる（非接敵時はスロット集結・接敵時は持ち場内で交戦）。
            // あわせて各隷下の現在地とスロットのズレを測り、隊形の整い具合（cohesion）を判定する（#軍団集結）。
            float maxSlotErrorSqr = 0f;
            int ci = 0;
            for (int i = 0; i < slots.Count && ci < subs.Count; i++)
            {
                if (slots[i].commander) continue;
                FleetStrength sub = subs[ci]; ci++;

                // 手動命令中の隷下はプレイヤー優先＝この tick は陣形/スロットを被せず、隊形の整い判定からも除く。
                FleetAI ai = sub.GetComponent<FleetAI>();
                if (ai != null && ai.ManualOverride) continue;

                Squadron sq = sub.GetComponent<Squadron>();
                if (sq != null) sq.TryChangeFormation(rec); // 各艦隊の配下艦陣形（スキルポイント消費・#陣形コスト）

                Vector2 slotLocal = slots[i].localPos - commanderLocal; // 軍団長を原点とした局所スロット
                Vector2 slotWorld = anchor + RotateVec(slotLocal, facingDeg);
                float errSqr = ((Vector2)sub.transform.position - slotWorld).sqrMagnitude;
                if (errSqr > maxSlotErrorSqr) maxSlotErrorSqr = errSqr;

                if (ai == null) continue;
                ai.corpsControlled = true;
                ai.hasCorpsAnchor = true; ai.corpsAnchor = anchor;     // 持ち場（深追い抑制）
                ai.hasCorpsSlot = true;                                // 軍団スロット集結（#軍団集結）
                ai.corpsCommanderTf = commander.transform;
                ai.corpsSlotLocal = slotLocal;
                ai.corpsFacingDeg = facingDeg;
            }

            // 軍団長の前進保留（#軍団集結）：隊形が整う前に突進すると「各艦バラバラに突撃」になるため、
            // 隷下がスロットへ就くまで軍団長は前進を待つ（敵方向へ正対して待機）。整い次第／敵が間近に迫れば解除し、
            // 軍団ごとまとまって前進・交戦する。隷下がいない（単艦隊）軍団は待たない（従来どおり前進）。
            if (cmdAi != null)
            {
                if (cmdManual)
                {
                    cmdAi.corpsHold = false; // 手動命令中はプレイヤー優先（保留しない＝命令どおり動かす）
                }
                else
                {
                    float formTol = Mathf.Max(spacing * CorpsFormTolerance, CorpsFormMinTolerance);
                    bool formed = subs.Count == 0 || maxSlotErrorSqr <= formTol * formTol;
                    bool enemyNear = nearest != null
                        && ((Vector2)nearest.transform.position - anchor).sqrMagnitude <= CorpsFormUpAbortRange * CorpsFormUpAbortRange;
                    cmdAi.corpsHold = subs.Count > 0 && !formed && !enemyNear;
                }
            }

            // 軍団長の陣形変更命令を通知（変化時のみ＝洪水回避・#軍団長の陣形変更命令）。会戦開始の初回も発火する。
            if (!lastCorpsFormation.TryGetValue(corpsKey, out Formation prev) || prev != rec)
            {
                lastCorpsFormation[corpsKey] = rec;
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    $"{CommanderName(commander)}：軍団を{rec}に再編し集結を命令（{CorpsName(commander)}・{fleetCount}隊）");
            }
        }

        /// <summary>軍団指揮の拘束を解く（陣形の自己判断・自律行動へ戻す＝スロット集結も解除）。</summary>
        private static void ReleaseFromCorps(FleetStrength f)
        {
            if (f == null) return;
            FleetAI ai = f.GetComponent<FleetAI>();
            if (ai == null) return;
            ai.corpsControlled = false;
            ai.hasCorpsAnchor = false;
            ai.hasCorpsSlot = false;
            ai.corpsCommanderTf = null;
            ai.corpsHold = false;   // 拘束解除＝前進保留も解く（#軍団集結）
        }

        /// <summary>ベクトルを Z 角(度・+Y 基準)で回す（CorpsFormation / FleetAI と同一規約）。</summary>
        private static Vector2 RotateVec(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>艦隊の配下艦占有半径（無ければ0）。軍団スロット間隔の非重複に使う。</summary>
        private static float FootprintOf(FleetStrength f)
        {
            Squadron sq = f != null ? f.GetComponent<Squadron>() : null;
            return sq != null ? sq.FootprintRadius() : 0f;
        }

        /// <summary>軍団長に最も近い敵対旗艦（推奨陣形の相手兵力・軍団正面に使う）。敵不在は null。</summary>
        private static FleetStrength NearestHostile(FleetStrength commander)
        {
            if (commander == null) return null;
            IReadOnlyList<FleetStrength> flagships = FleetRegistry.AllFlagships;
            float best = float.MaxValue;
            FleetStrength nearest = null;
            Vector2 pos = commander.transform.position;
            for (int i = 0; i < flagships.Count; i++)
            {
                FleetStrength e = flagships[i];
                if (e == null || e == commander || !e.IsAlive) continue;
                if (!FactionRelations.IsHostile(commander, e)) continue;
                float d = ((Vector2)e.transform.position - pos).sqrMagnitude;
                if (d < best) { best = d; nearest = e; }
            }
            return nearest;
        }

        /// <summary>軍団の正面（度・+Y 基準）＝軍団長→最寄り敵。敵不在は軍団長の現在の向き。CorpsFormation と同一規約。</summary>
        private static float FacingDeg(FleetStrength cmd, FleetStrength enemy)
        {
            Vector2 dir = enemy != null
                ? ((Vector2)enemy.transform.position - (Vector2)cmd.transform.position)
                : (Vector2)cmd.transform.up;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
            return Vector2.SignedAngle(Vector2.up, dir.normalized);
        }

        /// <summary>軍団長名（乗艦している軍団長＞艦長＞表示名）。通知の主語に使う。</summary>
        private static string CommanderName(FleetStrength commander)
        {
            if (commander == null) return "軍団長";
            if (commander.corpsCommander != null && !string.IsNullOrEmpty(commander.corpsCommander.admiralName))
                return commander.corpsCommander.admiralName;
            if (commander.admiralData != null && !string.IsNullOrEmpty(commander.admiralData.admiralName))
                return commander.admiralData.admiralName;
            return string.IsNullOrEmpty(commander.admiralName) ? "軍団長" : commander.admiralName;
        }

        /// <summary>軍団名（無ければ臨時軍団）。</summary>
        private static string CorpsName(FleetStrength commander)
            => (commander != null && !string.IsNullOrEmpty(commander.corpsName)) ? commander.corpsName : "臨時軍団";

        private static FleetStrength FindById(List<FleetStrength> list, int instanceId)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && list[i].GetInstanceID() == instanceId) return list[i];
            return null;
        }
    }
}
