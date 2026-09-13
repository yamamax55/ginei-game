using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ginei
{
    /// <summary>
    /// <b>軍団</b>（複数艦隊）の隊形運用（Battle シーンに自動生成・手置き不要）。隷下<b>艦隊</b>を
    /// `CorpsFormationRules`（Core）のスロットへ誘導して<b>軍団全体の隊形</b>（軍団内で艦隊をどう並べるか）を組む。
    ///
    /// <para><b>用語の区別（混同しない）</b>：
    /// ・<b>軍団全体の隊形</b>＝本クラスが扱う層。艦隊を単位に前後左右へ並べる（軍団長は後方中央）。
    /// ・<b>各艦隊内の配下艦の陣形</b>＝<see cref="Squadron"/> が持つ <see cref="Formation"/> 値。1艦隊の中で
    ///   配下艦をどう並べるか。<b>本クラスは触らない</b>（艦隊陣形の窓口は `FleetCommander.ChangeFormation`）。
    /// 同じ <see cref="Formation"/> 列挙を「並べ方の型」として両層が使うだけで、レイヤーは別物。</para>
    ///
    /// <para><b>軍団ごとに命令を持つ</b>：状態は「軍団キー（シーン＋勢力＋軍団名）→ 命令レコード」の辞書。
    /// 単一の commander/formation フィールドを持たない＝<b>B軍団への命令がA軍団の状態を置き換えない</b>。
    /// キーにシーンを含めるので、ウィンドウ化会戦（additive で複数の "Battle" シーンが同時に載る）でも
    /// <b>別戦場の同名軍団と混ざらない</b>。</para>
    ///
    /// <para><b>手動指定と AI 自動復帰の方針</b>：ここに登録された命令は「プレイヤーの手動指定」。
    /// <see cref="BattlefieldCommandManager"/>（AI ドクトリン）は手動命令のある軍団の隊形・スロットに触れない
    /// （＝AI の解決周期を跨いでも手動指定が戻らない）。手動が解けるのは
    /// ①プレイヤーの明示解除（軍団 ▸ 解除）②軍団の消滅（生存メンバー0）③総退却の下令（生存優先）の3つだけ。
    /// 判定は `CorpsFormationOrderRules.ShouldReleaseManual`（Core・test-first）に集約。</para>
    ///
    /// 数値ジオメトリ・優先順位判定は Core に委譲し、ここは集結・誘導・追従・ローテーションの配線のみ。
    /// </summary>
    public class CorpsFormation : MonoBehaviour
    {
        [Header("軍団隊形（艦隊の並べ方）")]
        [Tooltip("艦隊間隔（艦隊規模に合わせ大きめ）")]
        public float spacing = 7f;
        [Tooltip("軍団長の周囲この距離内の同軍団（無ければ同勢力）艦隊を集結対象にする")]
        public float gatherRange = 60f;
        [Tooltip("方陣のとき前列を後方へ入れ替えるローテーション間隔（秒・timeScale 追従）")]
        public float rotationInterval = 8f;
        [Tooltip("軍団スロットへの追従を再計算する間隔（秒・timeScale 追従）。軍団長が動いても隷下が付いてくる")]
        public float applyInterval = 0.25f;
        [Tooltip("最大スロットズレがこの距離以内なら『形成完了』とみなす（表示・整列判定）")]
        public float formTolerance = CorpsFormationOrderRules.DefaultFormTolerance;

        public static CorpsFormation Instance { get; private set; }

        /// <summary>軍団ごとの手動隊形命令。単一フィールドではなく軍団キーで引く＝別軍団の命令と混ざらない。</summary>
        private class CorpsOrderState
        {
            public string key;                       // 軍団キー（シーン＋勢力＋軍団名）
            public FleetStrength commander;          // 軍団長（後方中央・前線に出さない）
            public readonly List<FleetStrength> combat = new List<FleetStrength>(); // 隷下（前→後の隊列順）
            public Formation formation;              // 軍団全体の隊形
            public float facingDeg;                  // 軍団正面（度・+Y 基準）
            public float nextRotateTime;             // 次の前列交代時刻
            public bool formed;                      // 形成完了か
            public float progress;                   // 形成の進み具合(0..1)
        }

        // 軍団キー → 命令。static＝本オブジェクトが1つでも全戦場（additive シーン）の軍団を別々に保持できる。
        private static readonly Dictionary<string, CorpsOrderState> orders = new Dictionary<string, CorpsOrderState>();
        private static readonly List<string> scratchKeys = new List<string>();

        private float nextApplyTime;

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

        private void Awake() => orders.Clear(); // 新しい会戦＝手動命令をリセット

        private void OnDestroy()
        {
            if (Instance != this) return;
            ReleaseAll();
            Instance = null;
        }

        // ===== 軍団キー（唯一の窓口・BattlefieldCommandManager も同じ窓口でキーを作る）=====

        /// <summary>
        /// その艦隊のシーンキー（別戦場の同名軍団と混ざらないための識別子）。
        /// シーン名は additive のウィンドウ化会戦で重複する（どれも "Battle"）ため使えない。
        /// <see cref="UnityEngine.SceneManagement.Scene"/> のハッシュ（＝シーンハンドル由来）を使う＝
        /// ロード中は不変・シーンごとに異なり、Unity のバージョン差（handle の型変更）にも影響されない。
        /// </summary>
        public static int SceneKeyOf(FleetStrength fs)
            => fs != null ? fs.gameObject.scene.GetHashCode() : 0;

        /// <summary>勢力名（多勢力対応＝FactionData 優先、無ければ enum 名）。</summary>
        public static string FactionNameOf(FleetStrength fs)
        {
            if (fs == null) return "?";
            if (fs.factionData != null && !string.IsNullOrEmpty(fs.factionData.factionName)) return fs.factionData.factionName;
            return fs.faction.ToString();
        }

        /// <summary>
        /// 名前付き軍団のキー（シーン＋勢力＋軍団名）。<see cref="BattlefieldCommandManager"/> と同じ窓口を使い、
        /// 手動命令の有無を同じキーで引けるようにする（キーの二重定義を作らない）。軍団名が空なら null。
        /// </summary>
        public static string KeyFor(FleetStrength fs)
        {
            if (fs == null || string.IsNullOrEmpty(fs.corpsName)) return null;
            return CorpsFormationOrderRules.MakeKey(SceneKeyOf(fs), FactionNameOf(fs), fs.corpsName);
        }

        /// <summary>軍団名を持たない選択編成のキー（軍団長 id で一意＝別の臨時編成と混ざらない）。</summary>
        private static string AdhocKeyFor(FleetStrength commander)
            => CorpsFormationOrderRules.MakeAdhocKey(SceneKeyOf(commander), FactionNameOf(commander), EntityKey.Of(commander));

        /// <summary>その艦隊に対応する軍団キー（名前付きが優先・無ければ臨時編成キー）。</summary>
        private static string ResolveKey(FleetStrength commander)
            => KeyFor(commander) ?? AdhocKeyFor(commander);

        // ===== 外部窓口（AI 側からの問い合わせ・UI 表示）=====

        /// <summary>その軍団キーに手動隊形命令が生きているか（AI＝BattlefieldCommandManager が譲る判定に使う）。</summary>
        public static bool HasManualOrder(string corpsKey)
            => !string.IsNullOrEmpty(corpsKey) && orders.ContainsKey(corpsKey);

        /// <summary>手動指定された軍団隊形を取り出す（無ければ false）。</summary>
        public static bool TryGetManualFormation(string corpsKey, out Formation formation)
        {
            formation = Formation.方陣;
            if (string.IsNullOrEmpty(corpsKey) || !orders.TryGetValue(corpsKey, out CorpsOrderState o)) return false;
            formation = o.formation;
            return true;
        }

        /// <summary>
        /// 手動命令を解除して AI 自動へ返す（総退却の下令・軍団消滅・プレイヤーの明示解除）。解除したら true。
        /// ★<b>既定はプレイヤー権限</b>＝指揮系統外の軍団は解除できない（#67）。
        /// AI・QA・内部の自動失効は <see cref="ReleaseManualOrder(string,string,CommandOrderSource)"/> で明示する。
        /// </summary>
        public static bool ReleaseManualOrder(string corpsKey, string reason = null)
            => ReleaseManualOrder(corpsKey, reason, CommandOrderSource.プレイヤー);

        /// <summary>
        /// <inheritdoc cref="ReleaseManualOrder(string,string)"/>
        /// 出どころを明示する版。<see cref="CommandOrderSource.プレイヤー"/> のときだけ指揮権を判定する。
        /// </summary>
        public static bool ReleaseManualOrder(string corpsKey, string reason, CommandOrderSource source)
        {
            if (string.IsNullOrEmpty(corpsKey) || !orders.TryGetValue(corpsKey, out CorpsOrderState o)) return false;

            // #67：解除も命令の一種。系統外の軍団の手動指定を勝手に解いてはいけない。
            // 判定の基準は軍団長（居なければ隷下の先頭）＝その軍団の所属で決まる。
            FleetStrength anchor = o.commander != null ? o.commander : (o.combat.Count > 0 ? o.combat[0] : null);
            if (!CanOrderFrom(source, anchor, "軍団指定の解除")) return false;

            ReleaseOrder(o);
            orders.Remove(corpsKey);
            if (!string.IsNullOrEmpty(reason))
                NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                    $"{CorpsFormationOrderRules.DisplayName(corpsKey)}：軍団隊形の手動指定を解除（{reason}）");
            return true;
        }

        /// <summary>適用中の軍団命令の表示文字列（軍団名・隊形・手動/自動・形成中/完了）。無ければ null。</summary>
        public static string StatusFor(FleetStrength fs)
        {
            if (fs == null) return null;
            foreach (var kv in orders)
            {
                CorpsOrderState o = kv.Value;
                if (o.commander != fs && !o.combat.Contains(fs)) continue;
                return CorpsFormationOrderRules.StatusLabel(
                    CorpsFormationOrderRules.DisplayName(o.key), o.formation, true, o.formed);
            }
            return null;
        }

        // ===== 命令の発行 =====

        /// <summary>
        /// 命令の出どころ（GitHub #67）。判定の方針は Core の <see cref="CommandOrderSource"/> が持つ
        /// ＝ここでは別定義を作らず、その別名として使う（プレイヤーだけ指揮権を判定する）。
        /// </summary>

        /// <summary>その軍団へプレイヤーが直接命令できるか（#67）。AI/QA は常に true。</summary>
        private bool CanOrder(CommandOrderSource source, FleetStrength member, string label)
            => CanOrderFrom(source, member, label);

        /// <summary>
        /// その軍団へプレイヤーが直接命令できるか（#67・static 版）。
        /// AI・QA・内部の自動失効は判定の対象外＝素通し。
        ///
        /// 判定に使う指揮官は<b>その部隊が居る会戦シーン</b>から引く（静的呼び出しでも戦場を取り違えない）。
        /// </summary>
        private static bool CanOrderFrom(CommandOrderSource source, FleetStrength member, string label)
        {
            // 判定が要るのはプレイヤーの命令だけ（方針は Core の CommandOrderSourceRules が持つ）。
            if (!CommandOrderSourceRules.RequiresAuthorityCheck(source)) return true;
            if (member == null) return false;

            FleetCommander commander = BattleWindowUI.FindInSceneOrAny<FleetCommander>(member.gameObject.scene);
            if (commander == null) return true;   // 指揮官が居ない（テスト等）＝従来どおり

            Selectable sel = member.GetComponent<Selectable>();
            BattleCommandRight right = sel != null ? commander.RightFor(sel) : BattleCommandRight.不可;
            if (right == BattleCommandRight.直接命令) return true;

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.注意,
                BattleCommandAuthorityRules.ReasonText(right, member.corpsName));
            return false;
        }

        /// <summary>
        /// 指定艦隊が属する軍団（corpsName が無ければ付近の同勢力）の艦隊を集結させ、指定隊形を組む。
        /// 軍団長＝集結対象のうち軍団旗艦または最上位階級。前線には出さず後方中央に置く。敗走中の艦隊は含めない。
        /// </summary>
        public void FormCorps(FleetStrength anchorMember, Formation form)
            => FormCorps(anchorMember, form, CommandOrderSource.プレイヤー);

        /// <summary><inheritdoc cref="FormCorps(FleetStrength,Formation)"/> 出どころを指定する（#67）。</summary>
        public void FormCorps(FleetStrength anchorMember, Formation form, CommandOrderSource source)
        {
            if (!IsEligible(anchorMember)) return;
            if (!CanOrder(source, anchorMember, "軍団隊形")) return;
            BuildAndApply(GatherCorps(anchorMember), form, anchorMember);
        }

        /// <summary>
        /// プレイヤーが選択した艦隊で軍団隊形を組む。選択が複数の軍団に跨っていても<b>軍団ごとに束ねて別々の命令</b>を
        /// 作る（A軍団＝横陣、B軍団＝方陣を同時に維持できる）。1隊だけの軍団はその軍団を自動集結（従来の利便）。
        /// </summary>
        public void FormCorpsFromSelection(List<FleetStrength> selected, Formation form)
        {
            if (selected == null) return;
            // #67：プレイヤーの選択は FleetCommander の関門を通ってから渡される（CommandMenu 経由）。
            // ここへ直接来る経路（スクリプト）に備え、対象ごとにもう一度確かめる。

            // 軍団キーごとに束ねる（キー未確定の臨時編成は「シーン＋勢力」で束ねる）。
            var groups = new Dictionary<string, List<FleetStrength>>();
            var groupOrder = new List<string>();
            for (int i = 0; i < selected.Count; i++)
            {
                FleetStrength f = selected[i];
                if (!IsEligible(f)) continue;
                if (!CanOrder(CommandOrderSource.プレイヤー, f, "軍団隊形")) continue;
                string gk = KeyFor(f) ?? CorpsFormationOrderRules.MakeKey(SceneKeyOf(f), FactionNameOf(f), "");
                if (!groups.TryGetValue(gk, out var list)) { list = new List<FleetStrength>(); groups[gk] = list; groupOrder.Add(gk); }
                list.Add(f);
            }

            for (int i = 0; i < groupOrder.Count; i++)
            {
                var list = groups[groupOrder[i]];
                if (list.Count == 0) continue;
                // 1隊だけ選択＝その軍団を自動集結（残りの隷下も呼ぶ）。複数選択＝選んだ艦隊だけで組む。
                if (list.Count == 1) FormCorps(list[0], form);
                else BuildAndApply(list, form, list[0]);
            }
        }

        /// <summary>軍団長を選び、隷下艦隊を能力見立てで前後に並べて隊形命令を作る共通処理。</summary>
        private void BuildAndApply(List<FleetStrength> members, Formation form, FleetStrength prefer)
        {
            if (members == null || members.Count == 0) return;

            // 軍団長＝軍団旗艦 or 最上位階級（同位は prefer を優先）。以後はその勢力・そのシーンに絞る。
            FleetStrength commander = SelectCommander(members, prefer);
            if (commander == null) return;

            var o = new CorpsOrderState { key = ResolveKey(commander), commander = commander, formation = form };
            Scene scene = commander.gameObject.scene;
            for (int i = 0; i < members.Count; i++)
            {
                FleetStrength f = members[i];
                if (f == null || f == commander) continue;
                if (f.faction != commander.faction) continue;          // 他勢力は混ぜない
                if (f.gameObject.scene != scene) continue;             // 別戦場の艦隊は混ぜない
                if (o.combat.Contains(f)) continue;
                o.combat.Add(f);
            }

            o.facingDeg = ComputeFacing(commander);
            // 軍団長が隷下提督の能力（戦闘力・功名心・士気）を見立てて前後を決める（見極め精度は軍団長の能力次第）。
            OrderCombatByDeployment(o);
            o.nextRotateTime = Time.time + Mathf.Max(1f, rotationInterval);

            // 同じ軍団への再命令：古い命令の隊列だけ解いて置き換える（他軍団の命令には一切触れない）。
            if (orders.TryGetValue(o.key, out CorpsOrderState old)) ReleaseOrder(old, o);
            DetachFromOtherOrders(o);
            orders[o.key] = o;

            // 名前付き軍団で命令に含めなかった同軍団の艦隊は拘束を解いて自律行動へ戻す（宙ぶらりんにしない）。
            ReleaseUnselectedCorpsMates(o);

            ApplyOrder(o);

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"{CorpsFormationOrderRules.DisplayName(o.key)}：{form} を形成（{o.combat.Count + 1}隊・軍団長は後方・手動指定）");
        }

        /// <summary>選択中の艦隊が属する軍団だけを前列交代させる（他軍団へ波及しない）。交代した軍団数を返す。</summary>
        public int RotateCorps(List<FleetStrength> selected)
        {
            if (selected == null) return 0;
            var done = new HashSet<string>();
            int count = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                string key = FindOrderKeyOf(selected[i]);
                if (string.IsNullOrEmpty(key) || !done.Add(key)) continue;
                if (RotateCorpsByKey(key)) count++;
            }
            return count;
        }

        /// <summary>指定軍団キーの前列交代（前列部隊を後方へ回して消耗を分散）。対象が無ければ false。</summary>
        public bool RotateCorpsByKey(string corpsKey) => RotateCorpsByKey(corpsKey, CommandOrderSource.プレイヤー);

        /// <summary><inheritdoc cref="RotateCorpsByKey(string)"/> 出どころを指定する（#67）。</summary>
        public bool RotateCorpsByKey(string corpsKey, CommandOrderSource source)
        {
            if (string.IsNullOrEmpty(corpsKey) || !orders.TryGetValue(corpsKey, out CorpsOrderState o)) return false;
            if (!CanOrder(source, o.commander, "前列交代")) return false;
            PruneOrder(o);
            if (o.combat.Count == 0) return false;

            int[] order = CorpsFormationOrderRules.RotateOrder(o.combat.Count, o.formation);
            var rotated = new List<FleetStrength>(o.combat.Count);
            for (int i = 0; i < order.Length; i++) rotated.Add(o.combat[order[i]]);
            o.combat.Clear(); o.combat.AddRange(rotated);
            o.nextRotateTime = Time.time + Mathf.Max(1f, rotationInterval);

            ApplyOrder(o);
            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.情報,
                $"{CorpsFormationOrderRules.DisplayName(o.key)}：前列部隊を後方へ交代（{o.formation}）");
            return true;
        }

        /// <summary>選択中の艦隊が属する軍団の手動指定を解除して AI 自動へ返す（明示解除）。解除数を返す。</summary>
        public int ReleaseCorps(List<FleetStrength> selected)
            => ReleaseCorps(selected, CommandOrderSource.プレイヤー);

        /// <summary>
        /// <inheritdoc cref="ReleaseCorps(List{FleetStrength})"/>
        /// 出どころを明示する版（#67）。<b>既定はプレイヤー</b>＝指揮系統外の軍団は解除できない。
        /// 混在選択のときは、<b>解除できる軍団だけ</b>を解除し、できないものは理由を出す
        /// （まとめて失敗にしない＝自分の軍団の操作は通る）。
        /// </summary>
        public int ReleaseCorps(List<FleetStrength> selected, CommandOrderSource source)
        {
            if (selected == null) return 0;
            var done = new HashSet<string>();
            int count = 0;
            for (int i = 0; i < selected.Count; i++)
            {
                string key = FindOrderKeyOf(selected[i]);
                if (string.IsNullOrEmpty(key) || !done.Add(key)) continue;
                if (ReleaseManualOrder(key, "指揮官の解除命令", source)) count++;
            }
            return count;
        }

        /// <summary>その艦隊が属する手動命令のキー（無ければ null）。</summary>
        private static string FindOrderKeyOf(FleetStrength fs)
        {
            if (fs == null) return null;
            foreach (var kv in orders)
                if (kv.Value.commander == fs || kv.Value.combat.Contains(fs)) return kv.Key;
            return null;
        }

        // ===== 毎フレームの追従・維持 =====

        private void Update()
        {
            if (orders.Count == 0) return;
            if (Time.time < nextApplyTime) return;
            nextApplyTime = Time.time + Mathf.Max(0.05f, applyInterval);

            scratchKeys.Clear();
            foreach (var k in orders.Keys) scratchKeys.Add(k);

            for (int i = 0; i < scratchKeys.Count; i++)
            {
                if (!orders.TryGetValue(scratchKeys[i], out CorpsOrderState o)) continue;

                // 死亡・退却・敗走した艦隊を隊列から外す。
                PruneOrder(o);

                // 軍団長が落ちたら後継を立てる。誰も残らなければ軍団消滅＝手動指定を解除（Core の解除条件②）。
                if (o.commander == null || !o.commander.IsAlive)
                {
                    if (o.combat.Count == 0)
                    {
                        if (CorpsFormationOrderRules.ShouldReleaseManual(true, false, false, false))
                            // 軍団が消滅した＝内部の自動失効。権限判定の対象外（明示）。
                            ReleaseManualOrder(o.key, null, CommandOrderSource.AI);
                        continue;
                    }
                    o.commander = o.combat[0];
                    o.combat.RemoveAt(0);
                }

                // 方陣は一定時間で前列を後方へローテーション（timeScale 追従）。
                if (CorpsFormationOrderRules.AutoRotates(o.formation) && Time.time >= o.nextRotateTime)
                {
                    RotateCorpsByKey(o.key);
                    continue; // RotateCorpsByKey が ApplyOrder 済み
                }

                ApplyOrder(o);
            }
        }

        /// <summary>
        /// 軍団長を基準に隷下をスロットへ就かせる。<b>毎 applyInterval で再適用</b>するため軍団長が移動しても
        /// 隷下はスロットに追従する（一度きりの SetDestination ではない）。
        /// AI 操舵の艦は <see cref="FleetAI"/> のスロット追従に載せ、AI 無効（プレイヤー操艦）の艦は直接誘導する。
        /// 個別の手動命令中（<see cref="FleetAI.ManualOverride"/>）の艦はプレイヤー優先でこの tick は触らない。
        /// </summary>
        private void ApplyOrder(CorpsOrderState o)
        {
            if (o == null || o.commander == null) return;
            o.facingDeg = ComputeFacing(o.commander);

            List<Vector2> offsets = CorpsFormationOrderRules.SubordinateSlotOffsets(o.combat.Count + 1, o.formation, spacing);
            Vector2 anchor = o.commander.transform.position;

            // 軍団長：その場で敵方向へ正対（前進しない＝前線に出過ぎない）。拘束は解いておく（隊を率いる側）。
            FleetMovement cmdMove = o.commander.GetComponent<FleetMovement>();
            if (cmdMove != null) cmdMove.FaceTarget(anchor + DirFromAngle(o.facingDeg));
            FleetAI cmdAi = o.commander.GetComponent<FleetAI>();
            if (cmdAi != null)
            {
                cmdAi.corpsControlled = true;   // 隷下の陣形自己判断を止める（軍団長が主導）
                cmdAi.hasCorpsAnchor = false;
                cmdAi.hasCorpsSlot = false;
                cmdAi.corpsHold = false;        // 手動指定中はプレイヤー命令優先＝前進保留しない
                ClearAiFlowFlags(cmdAi);        // AI の会戦フロー（包囲/遮蔽/決戦）は手動命令中は解く
            }

            float maxErr = 0f;
            int n = Mathf.Min(offsets.Count, o.combat.Count);
            for (int i = 0; i < n; i++)
            {
                FleetStrength sub = o.combat[i];
                if (sub == null) continue;

                Vector2 slotLocal = offsets[i];
                Vector2 slotWorld = anchor + Rotate(slotLocal, o.facingDeg);
                float err = ((Vector2)sub.transform.position - slotWorld).magnitude;
                if (err > maxErr) maxErr = err;

                FleetAI ai = sub.GetComponent<FleetAI>();
                if (ai != null && ai.enabled && ai.ManualOverride) continue; // 個別命令中はプレイヤー優先

                if (ai != null)
                {
                    // AI 操舵艦：スロットを渡して FleetAI に追従させる（軍団長が動いても付いてくる）。
                    ClearAiFlowFlags(ai);   // 手動命令中は AI の包囲/遮蔽/決戦を解く（隊形を崩さない）
                    ai.corpsControlled = true;
                    ai.hasCorpsAnchor = true; ai.corpsAnchor = anchor;   // 持ち場（深追い抑制）
                    ai.hasCorpsSlot = true;
                    ai.corpsCommanderTf = o.commander.transform;
                    ai.corpsSlotLocal = slotLocal;
                    ai.corpsFacingDeg = o.facingDeg;
                }
                if (ai == null || !ai.enabled)
                {
                    // プレイヤー操艦（AI 無効）：FleetAI がスロットを消費しないので直接誘導する。
                    FleetMovement mv = sub.GetComponent<FleetMovement>();
                    if (mv != null) mv.SetDestination(slotWorld, o.facingDeg);
                }
            }

            o.progress = CorpsFormationOrderRules.FormationProgress(maxErr, formTolerance);
            o.formed = CorpsFormationOrderRules.IsFormed(maxErr, formTolerance);
        }

        // ===== 集結・選定・解除・補助 =====

        /// <summary>軍団隊形に含められる艦隊か＝生存（退却していない）・戦闘艦・敗走中でない。</summary>
        private static bool IsEligible(FleetStrength f)
        {
            if (f == null || !f.IsAlive || !f.IsCombatant) return false;
            FleetMorale mo = f.GetComponent<FleetMorale>();
            if (mo != null && mo.IsRouted) return false; // 敗走中は含めない
            return true;
        }

        /// <summary>
        /// 同一戦場（同シーン）・同勢力の同軍団を集める。<b>シーンで絞る</b>ので別戦場（additive の同名シーン）の
        /// 同名軍団とは混ざらない。軍団名が無ければ集結範囲内の同勢力を臨時編成とみなす。
        /// </summary>
        private List<FleetStrength> GatherCorps(FleetStrength anchor)
        {
            var result = new List<FleetStrength>();
            string corps = anchor.corpsName;
            Scene scene = anchor.gameObject.scene;
            float r2 = gatherRange * gatherRange;
            IReadOnlyList<FleetStrength> all = FleetRegistry.FlagshipsIn(scene); // ★戦場ごとに引く（混線防止）
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength fs = all[i];
                if (!IsEligible(fs)) continue;        // 敗走中・退却・非戦闘艦は含めない
                if (fs.faction != anchor.faction) continue;
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
            // 軍団長が乗艦している軍団旗艦（CSG＝打撃群指揮官の乗る艦）を最優先で軍団長扱いにする。
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && members[i].IsCorpsFlagship) return members[i];

            // 乗艦が無ければ最上位階級の艦隊司令を充てる（後方互換）。
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
        private static void OrderCombatByDeployment(CorpsOrderState o)
        {
            if (o.combat.Count == 0) return;

            var cands = new List<DeploymentCandidate>(o.combat.Count);
            for (int i = 0; i < o.combat.Count; i++)
            {
                FleetStrength f = o.combat[i];
                AdmiralData ad = f.admiralData;
                float combatApt = ad != null ? (ad.EffectiveAttack + ad.EffectiveDefense + ad.EffectiveLeadership) / 3f : 50f;
                float merit = ad != null ? ad.ambition : 50f;
                FleetMorale mo = f.GetComponent<FleetMorale>();
                float morale = mo != null ? mo.GetMoraleFactor() : 1f;
                cands.Add(new DeploymentCandidate(EntityKey.Of(f), combatApt, merit, morale));
            }

            float skill = CommanderSkill(o.commander);
            long[] order = CorpsDeploymentRules.OrderFrontToBack(cands, skill, Roll);

            var byId = new Dictionary<long, FleetStrength>(o.combat.Count);
            for (int i = 0; i < o.combat.Count; i++) byId[EntityKey.Of(o.combat[i])] = o.combat[i];
            var sorted = new List<FleetStrength>(o.combat.Count);
            for (int i = 0; i < order.Length; i++)
                if (byId.TryGetValue(order[i], out FleetStrength f)) sorted.Add(f);
            o.combat.Clear(); o.combat.AddRange(sorted);
        }

        /// <summary>軍団長の見極め能力（0..1）＝実効統率と実効情報の平均を正規化。軍団旗艦なら乗艦する軍団長を優先（CSG）。</summary>
        private static float CommanderSkill(FleetStrength cmd)
        {
            AdmiralData ad = (cmd != null && cmd.IsCorpsFlagship) ? cmd.corpsCommander : (cmd != null ? cmd.admiralData : null);
            if (ad == null) return 0.5f;
            return Mathf.Clamp01((ad.EffectiveLeadership + ad.EffectiveIntelligence) / 200f);
        }

        /// <summary>id から決定論的な当て推量(0..1)を作る（無能な軍団長の見立てに使う）。</summary>
        private static float Roll(long id)
        {
            float s = Mathf.Sin(id * 12.9898f + 78.233f) * 43758.5453f;
            return Mathf.Abs(s - Mathf.Floor(s));
        }

        /// <summary>死亡・退却・敗走した艦隊を隊列から外す。</summary>
        private static void PruneOrder(CorpsOrderState o)
        {
            for (int i = o.combat.Count - 1; i >= 0; i--)
                if (!IsEligible(o.combat[i])) { ReleaseFromCorps(o.combat[i]); o.combat.RemoveAt(i); }
        }

        /// <summary>命令に属する艦隊の軍団拘束を解く（AI 自律行動へ返す）。keep に含まれる艦は解かない。</summary>
        private static void ReleaseOrder(CorpsOrderState o, CorpsOrderState keep = null)
        {
            if (o == null) return;
            if (keep == null || (keep.commander != o.commander && !keep.combat.Contains(o.commander)))
                ReleaseFromCorps(o.commander);
            for (int i = 0; i < o.combat.Count; i++)
            {
                FleetStrength f = o.combat[i];
                if (keep != null && (keep.commander == f || keep.combat.Contains(f))) continue;
                ReleaseFromCorps(f);
            }
        }

        /// <summary>新命令のメンバーを他軍団の命令から外す（1隻が2つの軍団に属さない）。</summary>
        private static void DetachFromOtherOrders(CorpsOrderState o)
        {
            foreach (var kv in orders)
            {
                if (kv.Key == o.key) continue;
                CorpsOrderState other = kv.Value;
                for (int i = other.combat.Count - 1; i >= 0; i--)
                    if (other.combat[i] == o.commander || o.combat.Contains(other.combat[i])) other.combat.RemoveAt(i);
            }
        }

        /// <summary>名前付き軍団で命令に含めなかった同軍団（同シーン）の艦隊は拘束を解いて自律行動へ戻す。</summary>
        private static void ReleaseUnselectedCorpsMates(CorpsOrderState o)
        {
            if (o.commander == null || string.IsNullOrEmpty(o.commander.corpsName)) return;
            IReadOnlyList<FleetStrength> all = FleetRegistry.FlagshipsIn(o.commander.gameObject.scene);
            for (int i = 0; i < all.Count; i++)
            {
                FleetStrength f = all[i];
                if (f == null || f == o.commander) continue;
                if (f.faction != o.commander.faction || f.corpsName != o.commander.corpsName) continue;
                if (o.combat.Contains(f)) continue;
                ReleaseFromCorps(f);
            }
        }

        /// <summary>AI の会戦フロー（包囲・カウンター遮蔽・決戦）のフラグを解く。手動指定中は隊形を優先する。</summary>
        private static void ClearAiFlowFlags(FleetAI ai)
        {
            if (ai == null) return;
            ai.enveloping = false;
            ai.counterScreening = false;
            ai.decisiveCommit = false;
        }

        /// <summary>軍団指揮の拘束を解く（陣形の自己判断・自律行動へ戻す＝スロット追従も解除）。</summary>
        private static void ReleaseFromCorps(FleetStrength f)
        {
            if (f == null) return;
            FleetAI ai = f.GetComponent<FleetAI>();
            if (ai == null) return;
            ai.corpsControlled = false;
            ai.hasCorpsAnchor = false;
            ai.hasCorpsSlot = false;
            ai.corpsCommanderTf = null;
            ai.corpsHold = false;
        }

        /// <summary>全ての手動命令を解いて AI 自動へ返す（会戦終了・本オブジェクト破棄時）。</summary>
        /// <summary>
        /// 会戦シーンの後片付け（<see cref="OnDestroy"/>）。命令ではなくオブジェクトの破棄なので
        /// 指揮権の判定は行わない（戦場ごと消えるときに全部返す）。
        /// </summary>
        private static void ReleaseAll()
        {
            foreach (var kv in orders) ReleaseOrder(kv.Value);
            orders.Clear();
        }

        /// <summary>軍団前方＝同一戦場の最寄りの敵旗艦方向。敵がいなければ軍団長の現在の向き。返り値は Z 角(度・+Y 基準)。</summary>
        private static float ComputeFacing(FleetStrength cmd)
        {
            if (cmd == null) return 0f;
            Vector2 pos = cmd.transform.position;
            FleetStrength nearest = null; float min = float.MaxValue;
            IReadOnlyList<FleetStrength> all = FleetRegistry.FlagshipsIn(cmd.gameObject.scene); // ★同一戦場のみ
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
    }
}
