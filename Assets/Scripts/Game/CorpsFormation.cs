using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// 軍団（複数艦隊）の陣形運用（Battle シーンに自動生成・手置き不要）。隷下艦隊を `CorpsFormationRules`（Core）の
    /// スロットへ誘導して軍団陣形を組む。史実準拠の配慮：<b>軍団長は後方中央</b>（前線に出過ぎない）／
    /// <b>横陣は幅制限</b>（Core が最大列数で折り返す）／<b>方陣は前列を一定時間で後方へローテーション</b>。
    /// 形成・ローテーションは各艦隊の `FleetMovement`（回頭→加減速→到着）で行うので動きは現実的。
    /// 数値ジオメトリは Core に委譲し、ここは集結・誘導・ローテーションの配線のみ。
    /// </summary>
    public class CorpsFormation : MonoBehaviour
    {
        [Tooltip("艦隊間隔（艦隊規模に合わせ大きめ）")]
        public float spacing = 7f;
        [Tooltip("軍団長の周囲この距離内の同軍団（無ければ同勢力）艦隊を集結対象にする")]
        public float gatherRange = 60f;
        [Tooltip("方陣のとき前列を後方へ入れ替えるローテーション間隔（秒・timeScale 追従）")]
        public float rotationInterval = 8f;

        public static CorpsFormation Instance { get; private set; }

        private FleetStrength commander;
        private readonly List<FleetStrength> combat = new List<FleetStrength>(); // 前→後の隊列順
        private Formation formation = Formation.方陣;
        private float facingDeg;
        private float nextRotateTime;
        private bool active;

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
            if (Instance != null) return;
            Instance = new GameObject("CorpsFormation").AddComponent<CorpsFormation>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>
        /// 指定艦隊が属する軍団（corpsName が無ければ付近の同勢力）の艦隊を集結させ、指定陣形を組む。
        /// 軍団長＝集結対象のうち最上位階級。前線には出さず後方中央に置く。敗走中の艦隊は含めない。
        /// </summary>
        public void FormCorps(FleetStrength anchorMember, Formation form)
        {
            if (!IsEligible(anchorMember)) return;
            BuildAndApply(GatherCorps(anchorMember), form, anchorMember);
        }

        /// <summary>
        /// プレイヤーが選択した艦隊だけで軍団陣形を組む（どの隷下艦隊を含めるか選択可能）。敗走中・退却・非戦闘艦は除外。
        /// 1隊だけ選択時はその軍団を自動集結（従来の利便）。
        /// </summary>
        public void FormCorpsFromSelection(List<FleetStrength> selected, Formation form)
        {
            if (selected == null) return;
            var eligible = new List<FleetStrength>();
            for (int i = 0; i < selected.Count; i++)
                if (IsEligible(selected[i])) eligible.Add(selected[i]);
            if (eligible.Count == 0) return;
            if (eligible.Count == 1) { FormCorps(eligible[0], form); return; }
            BuildAndApply(eligible, form, eligible[0]);
        }

        /// <summary>軍団長を選び、隷下艦隊を能力見立てで前後に並べて陣形を組む共通処理。</summary>
        private void BuildAndApply(List<FleetStrength> members, Formation form, FleetStrength prefer)
        {
            if (members == null || members.Count == 0) return;
            formation = form;

            // 軍団長＝最上位階級（同位は prefer を優先）。以後はその勢力に絞る。
            commander = SelectCommander(members, prefer);
            if (commander == null) return;

            combat.Clear();
            for (int i = 0; i < members.Count; i++)
            {
                FleetStrength f = members[i];
                if (f == null || f == commander) continue;
                if (f.faction != commander.faction) continue; // 他勢力は混ぜない
                combat.Add(f);
            }

            facingDeg = ComputeFacing(commander);
            // 軍団長が隷下提督の能力（戦闘力・功名心・士気）を見立てて前後を決める（見極め精度は軍団長の能力次第）。
            OrderCombatByDeployment();

            active = true;
            nextRotateTime = Time.time + rotationInterval;
            ApplyFormation();

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"{CorpsLabel(commander)}：{form} を形成（{combat.Count + 1}隊・軍団長は後方）");
        }

        /// <summary>軍団陣形に含められる艦隊か＝生存（退却していない）・戦闘艦・敗走中でない。</summary>
        private static bool IsEligible(FleetStrength f)
        {
            if (f == null || !f.IsAlive || !f.IsCombatant) return false;
            FleetMorale mo = f.GetComponent<FleetMorale>();
            if (mo != null && mo.IsRouted) return false; // 敗走中は含めない
            return true;
        }

        /// <summary>方陣の前列部隊を後方へローテーションする（前線部隊の消耗を分散・現実的な交代）。</summary>
        public void RotateCorps()
        {
            if (!active || formation != Formation.方陣 || combat.Count == 0) return;
            int cols = CorpsFormationRules.ColumnsFor(Formation.方陣, combat.Count);
            int[] order = CorpsFormationRules.RotateFrontToBack(combat.Count, cols);
            var rotated = new List<FleetStrength>(combat.Count);
            for (int i = 0; i < order.Length; i++) rotated.Add(combat[order[i]]);
            combat.Clear(); combat.AddRange(rotated);
            ApplyFormation();
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                "軍団方陣：前列部隊を後方へ交代（ローテーション）");
        }

        private void Update()
        {
            if (!active) return;

            // 死亡・退却した隊列を除外。軍団長が落ちたら後継を立てるか解散。
            PruneDead();
            if (commander == null || !commander.IsAlive)
            {
                if (combat.Count == 0) { active = false; return; }
                commander = combat[0]; combat.RemoveAt(0);
                ApplyFormation();
            }

            // 方陣は一定時間で前列を後方へローテーション（timeScale 追従）。
            if (formation == Formation.方陣 && Time.time >= nextRotateTime)
            {
                nextRotateTime = Time.time + Mathf.Max(1f, rotationInterval);
                RotateCorps();
            }
        }

        /// <summary>軍団長を後方中央に置き、隷下艦隊を前方のスロットへ誘導する。各艦隊の向きは軍団前方（敵方向）。</summary>
        private void ApplyFormation()
        {
            if (commander == null) return;
            facingDeg = ComputeFacing(commander);

            int fleetCount = combat.Count + 1;
            List<CorpsSlot> slots = CorpsFormationRules.ComputeSlots(fleetCount, formation, spacing);

            // 軍団長スロットを基準（原点）にして世界座標へ。軍団長はその場で敵方向を向き前線に出ない。
            Vector2 commanderLocal = Vector2.zero;
            for (int i = 0; i < slots.Count; i++) if (slots[i].commander) { commanderLocal = slots[i].localPos; break; }
            Vector2 anchor = commander.transform.position;

            FleetMovement cmdMove = commander.GetComponent<FleetMovement>();
            if (cmdMove != null) cmdMove.FaceTarget(anchor + DirFromAngle(facingDeg)); // 敵方向へ正対（前進しない）

            // 戦闘艦隊を非軍団スロットへ割り当て（前→後の順）。
            int ci = 0;
            for (int i = 0; i < slots.Count && ci < combat.Count; i++)
            {
                if (slots[i].commander) continue;
                Vector2 offset = Rotate(slots[i].localPos - commanderLocal, facingDeg);
                Vector2 worldPos = anchor + offset;
                FleetMovement mv = combat[ci] != null ? combat[ci].GetComponent<FleetMovement>() : null;
                if (mv != null) mv.SetDestination(worldPos, facingDeg);
                ci++;
            }
        }

        // ===== 集結・選定・補助 =====

        private List<FleetStrength> GatherCorps(FleetStrength anchor)
        {
            var result = new List<FleetStrength>();
            string corps = anchor.corpsName;
            float r2 = gatherRange * gatherRange;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (!IsEligible(fs)) continue;        // 敗走中・退却・非戦闘艦は含めない
                if (fs.faction != anchor.faction) continue;
                // 同軍団があればそれで絞る。無ければ集結範囲内の同勢力を臨時軍団とみなす。
                if (!string.IsNullOrEmpty(corps))
                {
                    if (fs.corpsName != corps) continue;
                }
                else if (((Vector2)(fs.transform.position - anchor.transform.position)).sqrMagnitude > r2)
                {
                    continue;
                }
                result.Add(fs);
            }
            return result;
        }

        private static FleetStrength SelectCommander(List<FleetStrength> members, FleetStrength prefer)
        {
            FleetStrength best = prefer;
            int bestTier = TierOf(prefer);
            for (int i = 0; i < members.Count; i++)
            {
                int t = TierOf(members[i]);
                if (t > bestTier) { bestTier = t; best = members[i]; }
            }
            return best;
        }

        private static int TierOf(FleetStrength fs)
            => (fs != null && fs.admiralData != null) ? fs.admiralData.rankTier : 0;

        /// <summary>
        /// 軍団長が隷下提督の能力を見立てて隊列（前→後）を決める。戦闘力・功名心・士気を加重し、
        /// 見極めの精度は軍団長の能力（統率＋情報）に依る＝有能なら強兵・功名の士・高士気を前へ、無能なら誤配置。
        /// </summary>
        private void OrderCombatByDeployment()
        {
            if (combat.Count == 0) return;

            var cands = new List<DeploymentCandidate>(combat.Count);
            for (int i = 0; i < combat.Count; i++)
            {
                FleetStrength f = combat[i];
                AdmiralData ad = f.admiralData;
                float combatApt = ad != null ? (ad.EffectiveAttack + ad.EffectiveDefense + ad.EffectiveLeadership) / 3f : 50f;
                float merit = ad != null ? ad.ambition : 50f;
                FleetMorale mo = f.GetComponent<FleetMorale>();
                float morale = mo != null ? mo.GetMoraleFactor() : 1f;
                cands.Add(new DeploymentCandidate(f.GetInstanceID(), combatApt, merit, morale));
            }

            float skill = CommanderSkill(commander);
            int[] order = CorpsDeploymentRules.OrderFrontToBack(cands, skill, Roll);

            var byId = new Dictionary<int, FleetStrength>(combat.Count);
            for (int i = 0; i < combat.Count; i++) byId[combat[i].GetInstanceID()] = combat[i];
            var sorted = new List<FleetStrength>(combat.Count);
            for (int i = 0; i < order.Length; i++)
                if (byId.TryGetValue(order[i], out FleetStrength f)) sorted.Add(f);
            combat.Clear(); combat.AddRange(sorted);
        }

        /// <summary>軍団長の見極め能力（0..1）＝実効統率と実効情報の平均を正規化。提督不在は中庸0.5。</summary>
        private static float CommanderSkill(FleetStrength cmd)
        {
            AdmiralData ad = cmd != null ? cmd.admiralData : null;
            if (ad == null) return 0.5f;
            return Mathf.Clamp01((ad.EffectiveLeadership + ad.EffectiveIntelligence) / 200f);
        }

        /// <summary>id から決定論的な当て推量(0..1)を作る（無能な軍団長の見立てに使う）。</summary>
        private static float Roll(int id)
        {
            float s = Mathf.Sin(id * 12.9898f + 78.233f) * 43758.5453f;
            return Mathf.Abs(s - Mathf.Floor(s));
        }

        private void PruneDead()
        {
            // 死亡・退却・敗走した艦隊は隊列から外す（敗走中は陣形に含めない）。
            for (int i = combat.Count - 1; i >= 0; i--)
                if (!IsEligible(combat[i])) combat.RemoveAt(i);
        }

        /// <summary>軍団前方＝最寄りの敵旗艦方向。敵がいなければ軍団長の現在の向き。返り値は Z 角(度・+Y を基準)。</summary>
        private float ComputeFacing(FleetStrength cmd)
        {
            if (cmd == null) return 0f;
            Vector2 pos = cmd.transform.position;
            FleetStrength nearest = null; float min = float.MaxValue;
            IReadOnlyList<FleetStrength> all = FleetRegistry.AllFlagships;
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength e = all[i];
                if (e == null || !e.IsAlive) continue;
                if (!FactionRelations.IsHostile(cmd, e)) continue;
                float d = ((Vector2)e.transform.position - pos).sqrMagnitude;
                if (d < min) { min = d; nearest = e; }
            }
            Vector2 dir = nearest != null ? ((Vector2)nearest.transform.position - pos) : (Vector2)cmd.transform.up;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector2.up;
            return Vector2.SignedAngle(Vector2.up, dir.normalized);
        }

        private static Vector2 DirFromAngle(float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            // +Y を基準に Z 回転（SignedAngle(up,dir) の逆変換）。
            return new Vector2(-Mathf.Sin(r), Mathf.Cos(r));
        }

        private static Vector2 Rotate(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad;
            float c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        private static string CorpsLabel(FleetStrength fs)
            => (fs != null && !string.IsNullOrEmpty(fs.corpsName)) ? fs.corpsName : "臨時軍団";
    }
}
