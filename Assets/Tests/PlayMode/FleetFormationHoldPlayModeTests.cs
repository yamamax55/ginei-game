using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 確定仕様1の<b>配線</b>を実コンポーネントで確かめる統合試験。
    ///
    /// レビュー指摘を受けて、窓口を手で叩くだけの確認をやめ、
    /// <b>実際の <see cref="FleetAI"/> の探索周期</b>・<b>実際の到着</b>・
    /// <b>実際の <see cref="BattlefieldCommandManager"/> の総退却</b>を通して検証する。
    /// </summary>
    public class FleetFormationHoldPlayModeTests
    {
        private readonly System.Collections.Generic.List<GameObject> spawned =
            new System.Collections.Generic.List<GameObject>();

        // ★試験間で共有される状態（static／グローバル）は必ず元へ戻す。
        //   戻さないと「前の試験がクロックを止めたせいで次が落ちる」ような順序依存が生まれる
        //   （実際 fix1 では止まったクロックが原因で支援要請の試験が落ちた）。
        private float savedTimeScale;
        private bool savedClockPaused;
        private float savedClockSpeed;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            GameClock clock = StrategySession.Clock;
            savedClockPaused = clock != null && clock.paused;
            savedClockSpeed = clock != null ? clock.speed : 1f;
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();

            // 変えた入力条件を戻す（timeScale・統一クロック）。
            Time.timeScale = savedTimeScale;
            GameClock clock = StrategySession.Clock;
            if (clock != null) { clock.paused = savedClockPaused; clock.speed = savedClockSpeed; }
        }

        /// <summary>
        /// 直近の通知のうち、要請の往復に関するものを並べる（失敗時にどこで止まったか分かるように）。
        /// 受付「要請しました」→判断「応じました／断りました／流れました」→実行 のどこまで進んだかが読める。
        /// </summary>
        private static string RecentSupportMessages(long afterSeq)
        {
            var sb = new System.Text.StringBuilder();
            System.Collections.Generic.IReadOnlyList<Notification> all = NotificationCenter.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].seq <= afterSeq) continue;
                if (sb.Length > 0) sb.Append(" / ");
                sb.Append(all[i].message);
            }
            return sb.Length > 0 ? sb.ToString() : "通知なし（受付にも至っていない）";
        }

        /// <summary>AI の探索周期（短くして観測時間内に何度も回す）。</summary>
        private const float AiSearchInterval = 0.02f;
        /// <summary>AI を観測する時間（秒）。</summary>
        private const float AiObserveSeconds = 1.0f;

        /// <summary>観測時間のあいだに回る AI 探索周期のおおよその回数。</summary>
        private static int ExpectedAiTicks() => Mathf.FloorToInt(AiObserveSeconds / AiSearchInterval);

        /// <summary>
        /// 実AIが<b>確実に「別の陣形へ変えたい」</b>状況を作る（保持あり／なしの両試験で共通）。
        ///
        /// 前に対照が落ちた原因はここに2つあった：
        /// <list type="number">
        ///   <item><b>設定が遅すぎた</b>。<c>yield return null</c> のあとに <c>searchInterval</c> を縮めても、
        ///         最初の <c>FleetAI.Update</c> が<b>既定の2秒</b>で次回探索を予約済みなので、
        ///         1秒の観測では<b>2度と探索が回らない</b>＝AI の判断は実質1回だけだった。
        ///         → <b>最初のフレームが走る前</b>に設定する。</item>
        ///   <item><b>AI が変えたくならない状況</b>だった。兵力が互角だと
        ///         <c>FormationDoctrineRules.RecommendFormation</c> は 紡錘陣 を返し、
        ///         初期陣形と同じなので <c>TryChangeFormation</c> が呼ばれない。
        ///         唯一変わる経路は<b>乱数</b>まかせのカウンター陣形だけで、試験が不安定だった。
        ///         → <b>劣勢</b>（自/敵 ≦ <c>OutnumberedRatio</c>）にして 方陣 を推奨させ、
        ///         初期を 紡錘陣 にする＝<b>乱数が外れても必ず変えたくなる</b>。</item>
        /// </list>
        /// AI の経路そのものは迂回していない（判断も適用も本物の <see cref="FleetAI"/> が行う）。
        /// </summary>
        private void SetupAiWantsFormationChange(GameObject mine, GameObject enemy,
                                                 out Squadron sq, out FleetAI ai)
        {
            sq = mine.GetComponent<Squadron>();
            ai = mine.GetComponent<FleetAI>();

            // ★最初の Update が走る前に設定する（呼び出し側はここまで yield していない）。
            ai.autoFormation = true;
            ai.searchInterval = AiSearchInterval;

            // ★劣勢にして「方陣を布きたい」状態にする。初期は紡錘陣＝必ず差がある。
            var mineStrength = mine.GetComponent<FleetStrength>();
            var enemyStrength = enemy.GetComponent<FleetStrength>();
            enemyStrength.strength = mineStrength.strength * 10;
            enemyStrength.maxStrength = enemyStrength.strength;
            sq.currentFormation = Formation.紡錘陣;

            // 前提：この兵力比なら推奨は紡錘陣ではない（＝AI は必ず変えたくなる）。
            float ratio = (float)mineStrength.strength / enemyStrength.strength;
            Assert.LessOrEqual(ratio, FormationDoctrineRules.OutnumberedRatio,
                "前提：劣勢の兵力比になっていること");
            Assert.AreNotEqual(Formation.紡錘陣,
                FormationDoctrineRules.RecommendFormation(
                    mineStrength.strength, enemyStrength.strength, false, mineStrength.admiralData),
                "前提：AI の推奨が初期陣形と違うこと（同じだと AI は何もしない）");
        }

        private GameObject BuildFleet(string name, string corpsName, Faction faction = Faction.同盟,
                                      Vector2 pos = default, bool corpsFlagship = false)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var strength = go.AddComponent<FleetStrength>();
            strength.faction = faction;
            strength.admiralName = name;
            strength.corpsName = corpsName;
            strength.strength = 10000;
            strength.maxStrength = 10000;

            if (corpsFlagship)
            {
                // 軍団長（軍団旗艦）＝ BattlefieldCommandManager が総退却の判断に使う。
                var ad = ScriptableObject.CreateInstance<AdmiralData>();
                ad.hideFlags = HideFlags.DontSave;
                ad.admiralName = name + "・軍団長";
                ad.leadership = 50;
                ad.ambition = 50;
                ad.rankTier = 8;
                strength.corpsCommander = ad;
            }

            go.AddComponent<FleetMorale>();
            go.AddComponent<FleetMovement>();
            go.AddComponent<WeaponArc>();
            go.AddComponent<FleetWeapon>();
            var squadron = go.AddComponent<Squadron>();
            squadron.escortCount = 0;                 // 配下艦の生成は本試験に不要
            squadron.currentFormation = Formation.紡錘陣;
            go.AddComponent<FleetAI>();

            spawned.Add(go);
            return go;
        }

        // ===== 実 AI 周期を越える（レビュー指摘2） =====

        /// <summary>
        /// ★<b>実際の艦隊AI</b>を敵つきで何周期も回して、保持した陣形が上書きされないことを見る。
        /// 窓口を手で叩くのではなく <see cref="FleetAI.UpdateFormationDoctrine"/> に実際に走らせる。
        /// </summary>
        [UnityTest]
        public IEnumerator DirectOrder_SurvivesRealAiDoctrineCycles()
        {
            // ★対照（WithoutHold_RealAiDoesChangeFormation）と<b>まったく同じ条件</b>を作る。
            //   あちらで「AI は必ず陣形を変える」ことが示されているので、
            //   ここで変わらなければ、保持が効いている証拠になる。
            GameObject mine = BuildFleet("QA保持艦隊", "", Faction.同盟, new Vector2(0f, 0f));
            GameObject enemy = BuildFleet("QA敵艦隊", "", Faction.帝国, new Vector2(6f, 0f));
            SetupAiWantsFormationChange(mine, enemy, out Squadron sq, out FleetAI ai);

            Assert.AreEqual(FormationOrderResult.受理,
                sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令));
            Assert.IsTrue(sq.IsFormationHeld);

            int ticks = ExpectedAiTicks();
            float until = Time.time + AiObserveSeconds;
            int frames = 0;
            while (Time.time < until) { frames++; yield return null; }

            Assert.Greater(frames, 10, "フレームが進んでいない（試験になっていない）");
            Assert.GreaterOrEqual(ticks, 1, "AI の探索周期が1回も回らない設定になっている");
            Assert.AreEqual(Formation.円陣, sq.currentFormation,
                "実AIの探索周期を跨いで陣形が上書きされた");
            Assert.IsTrue(sq.IsFormationHeld, "実AIの周期で保持が解けた");
            Assert.AreEqual(FormationOrderSource.直接命令, sq.FormationHold.source,
                "保持の指定元が書き換わった");
            Assert.AreNotEqual(FormationOrderSource.艦隊AI, sq.LastFormationSource,
                "AI の指定が受理されている（保持が効いていない）");
        }

        /// <summary>保持していなければ、同じ状況で実AIが陣形を変えること（試験が空振りでない証拠）。</summary>
        [UnityTest]
        public IEnumerator WithoutHold_RealAiDoesChangeFormation()
        {
            GameObject mine = BuildFleet("QA自律艦隊", "", Faction.同盟, new Vector2(0f, 0f));
            GameObject enemy = BuildFleet("QA敵艦隊2", "", Faction.帝国, new Vector2(6f, 0f));
            SetupAiWantsFormationChange(mine, enemy, out Squadron sq, out FleetAI ai);

            Formation initial = sq.currentFormation;
            float until = Time.time + AiObserveSeconds;
            while (Time.time < until && sq.LastFormationSource == FormationOrderSource.なし)
                yield return null;

            Assert.IsFalse(sq.IsFormationHeld, "AI の指定で保持が付いている");
            Assert.AreEqual(FormationOrderSource.艦隊AI, sq.LastFormationSource,
                "実AIが一度も陣形を決めていない＝保持ありの試験が空振りになる" +
                "（現在陣形=" + sq.currentFormation + " / AI状態=" + ai.currentState + "）");
            Assert.AreNotEqual(initial, sq.currentFormation,
                "AI が決めたのに陣形が変わっていない");
        }

        // ===== 実際に到着してから保持を見る（レビュー指摘2） =====

        /// <summary>★<b>移動が実際に終わった</b>ことを確かめてから、保持が残っていることを見る。</summary>
        [UnityTest]
        public IEnumerator Hold_SurvivesActualArrival()
        {
            GameObject go = BuildFleet("QA移動保持艦隊", "", Faction.同盟, new Vector2(0f, 0f));
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var mv = go.GetComponent<FleetMovement>();
            var ai = go.GetComponent<FleetAI>();
            ai.autoFormation = false;   // 陣形の上書きはここでは論点にしない

            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            mv.SetDestination(new Vector2(1.5f, 0f), null);
            ai.BeginManualOverride();   // 直接命令＝到着まで AI に渡さない（実経路と同じ）

            // ★到着するまで待つ（到着を assert してから保持を見る）。
            float timeout = Time.time + 10f;
            while (mv.IsMoving && Time.time < timeout) yield return null;
            Assert.IsFalse(mv.IsMoving, "移動が終わらなかった（到着を確認できていない）");

            // 直接命令が終わって AI へ返ったことも確かめる。
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(ManualOverrideKind.なし, ai.OverrideKind,
                "移動が終わったのに直接命令が終了していない");

            Assert.IsTrue(sq.IsFormationHeld, "移動と直接命令が終わったら保持が解けている（独立していない）");
            Assert.AreEqual(Formation.円陣, sq.currentFormation);
        }

        // ===== 軍団総退却の実経路（レビュー指摘1・2） =====

        /// <summary>
        /// ★<b>士気は正常</b>のまま、残存兵力を落として
        /// <see cref="BattlefieldCommandManager"/> に<b>実際に総退却を発令させ</b>、
        /// その最中（まだ戦場端に着いていない）に陣形を指定しても
        /// <b>拒否され、スキルポイントも消費しない</b>ことを見る。
        /// </summary>
        [UnityTest]
        public IEnumerator CorpsRetreatMovement_RejectsFormationWithoutSpendingPoints()
        {
            const string corps = "QA退却軍団";
            GameObject cmdGo = BuildFleet("QA退却軍団長", corps, Faction.同盟, new Vector2(0f, 0f), true);
            GameObject subGo = BuildFleet("QA退却隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            BuildFleet("QA退却敵", "", Faction.帝国, new Vector2(8f, 0f));   // 敵がいないとフローが回らない

            var mgrGo = new GameObject("QA_BattlefieldCommandManager");
            var mgr = mgrGo.AddComponent<BattlefieldCommandManager>();
            mgr.resolveInterval = 0.05f;      // 判断周期を詰める
            spawned.Add(mgrGo);
            yield return null;

            var sub = subGo.GetComponent<FleetStrength>();
            var subSq = subGo.GetComponent<Squadron>();
            var subMorale = subGo.GetComponent<FleetMorale>();
            var subAi = subGo.GetComponent<FleetAI>();
            subAi.autoFormation = false;

            // ★士気は正常なまま（ここが要点）。
            Assert.IsFalse(subMorale.IsRouted, "前提：士気は正常であること");

            // 残存兵力だけを落として総退却の条件を作る（撤退状態は書かない）。
            var cmd = cmdGo.GetComponent<FleetStrength>();
            cmd.strength = Mathf.Max(1, Mathf.RoundToInt(cmd.maxStrength * 0.1f));
            sub.strength = Mathf.Max(1, Mathf.RoundToInt(sub.maxStrength * 0.1f));

            // ★BattlefieldCommandManager に実際に判断させ、「軍団総退却の発令」を前提として確かめる。
            //   （個体の AIState=撤退 は FleetAI 自身の retreatRatio でも立つので、それでは前提にならない）
            string corpsKey = CorpsFormation.KeyFor(sub);
            float timeout = Time.time + 10f;
            while (!BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey) && Time.time < timeout)
                yield return null;
            Assert.IsTrue(BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey),
                "軍団総退却が発令されなかった（前提が成立していない）");
            Assert.AreEqual(FleetAI.AIState.撤退, subAi.currentState,
                "総退却の発令後も隷下が撤退へ移っていない");

            // ★まだ戦場端には着いていない＝IsRetreating は false のはず。
            Assert.IsFalse(sub.IsRetreating, "前提：まだ戦場離脱していないこと");
            Assert.IsFalse(subMorale.IsRouted, "前提：士気は正常のままであること");

            float pointsBefore = subSq.SkillPoints;
            Formation before = subSq.currentFormation;

            FormationOrderResult r = subSq.RequestFormation(
                before == Formation.円陣 ? Formation.横陣 : Formation.円陣,
                FormationOrderSource.直接命令);

            Assert.AreEqual(FormationOrderResult.撤退中, r,
                "士気正常の総退却移動中に陣形指定を受理してしまった（ちらつき＋費用の無駄）");
            Assert.AreEqual(before, subSq.currentFormation, "拒否したのに陣形が変わっている");
            Assert.IsFalse(subSq.IsFormationHeld, "拒否したのに保持が付いている");
            Assert.AreEqual(pointsBefore, subSq.SkillPoints, 0.001f, "拒否したのに消費している");
        }

        /// <summary>
        /// ★総退却の発令中は、直接命令を維持している例外の艦でも新規の保持を受け付けない。
        /// ただし<b>移動命令そのものは止めない</b>（既存の対照を壊さない）。
        /// </summary>
        [UnityTest]
        public IEnumerator CorpsRetreatOrdered_RejectsNewHoldForDirectOrderedFleet()
        {
            const string corps = "QA例外軍団";
            GameObject cmdGo = BuildFleet("QA例外軍団長", corps, Faction.同盟, new Vector2(0f, 0f), true);
            GameObject subGo = BuildFleet("QA例外隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            BuildFleet("QA例外敵", "", Faction.帝国, new Vector2(8f, 0f));

            var mgrGo = new GameObject("QA_BattlefieldCommandManager2");
            var mgr = mgrGo.AddComponent<BattlefieldCommandManager>();
            mgr.resolveInterval = 0.05f;
            spawned.Add(mgrGo);
            yield return null;

            var subSq = subGo.GetComponent<Squadron>();
            var subAi = subGo.GetComponent<FleetAI>();
            var subMv = subGo.GetComponent<FleetMovement>();
            subAi.autoFormation = false;

            // 直接命令の移動中＝総退却に巻き込まれない例外の艦。
            subMv.SetDestination(new Vector2(60f, 0f), null);
            subAi.BeginManualOverride();

            var cmd = cmdGo.GetComponent<FleetStrength>();
            var sub = subGo.GetComponent<FleetStrength>();
            cmd.strength = Mathf.Max(1, Mathf.RoundToInt(cmd.maxStrength * 0.1f));
            sub.strength = Mathf.Max(1, Mathf.RoundToInt(sub.maxStrength * 0.1f));

            // ★軍団の「総退却の発令」を待つ。
            //   軍団長個体の AIState=撤退 は FleetAI 自身の retreatRatio(既定0.3) でも立つので、
            //   兵力を1割に落とした時点で<b>次のフレームに</b>立ってしまう＝軍団総退却の成立とは別物。
            //   ここを取り違えると BattlefieldCommandManager が回る前に先へ進んでしまう（fix1 の失敗原因）。
            string corpsKey = CorpsFormation.KeyFor(sub);
            Assert.IsFalse(string.IsNullOrEmpty(corpsKey), "軍団キーが取れない（前提が組めていない）");

            float timeout = Time.time + 10f;
            while (!BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey) && Time.time < timeout)
                yield return null;
            Assert.IsTrue(BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey),
                "軍団総退却が発令されなかった（前提が成立していない）");

            // 例外の艦は直接命令のまま（既存の対照）。
            Assert.AreEqual(ManualOverrideKind.直接命令, subAi.OverrideKind,
                "直接命令が総退却に巻き込まれている（既存仕様の変更）");
            Assert.IsTrue(subMv.IsMoving, "直接命令の移動が止められている");

            // それでも新規の陣形保持は受け付けない。
            float pointsBefore = subSq.SkillPoints;
            Assert.AreEqual(FormationOrderResult.撤退中,
                subSq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令),
                "総退却の発令中なのに新規の陣形保持を受理した");
            Assert.AreEqual(pointsBefore, subSq.SkillPoints, 0.001f, "拒否したのに消費している");
        }

        /// <summary>★総退却では、保持していた陣形が実際に解除されること。</summary>
        [UnityTest]
        public IEnumerator CorpsRetreat_ReleasesExistingHold()
        {
            const string corps = "QA解除軍団";
            GameObject cmdGo = BuildFleet("QA解除軍団長", corps, Faction.同盟, new Vector2(0f, 0f), true);
            GameObject subGo = BuildFleet("QA解除隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            BuildFleet("QA解除敵", "", Faction.帝国, new Vector2(8f, 0f));

            var mgrGo = new GameObject("QA_BattlefieldCommandManager3");
            var mgr = mgrGo.AddComponent<BattlefieldCommandManager>();
            mgr.resolveInterval = 0.05f;
            spawned.Add(mgrGo);
            yield return null;

            var subSq = subGo.GetComponent<Squadron>();
            subGo.GetComponent<FleetAI>().autoFormation = false;

            Assert.AreEqual(FormationOrderResult.受理,
                subSq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令));
            Assert.IsTrue(subSq.IsFormationHeld);

            var cmd = cmdGo.GetComponent<FleetStrength>();
            var sub = subGo.GetComponent<FleetStrength>();
            cmd.strength = Mathf.Max(1, Mathf.RoundToInt(cmd.maxStrength * 0.1f));
            sub.strength = Mathf.Max(1, Mathf.RoundToInt(sub.maxStrength * 0.1f));

            float timeout = Time.time + 10f;
            while (subSq.IsFormationHeld && Time.time < timeout) yield return null;

            Assert.IsFalse(subSq.IsFormationHeld, "総退却で陣形の保持が解除されなかった");
        }

        // ===== 軍団隊形との分離（レビュー指摘2） =====

        /// <summary>
        /// ★軍団長が陣形を発令しても、艦隊が手動保持していれば上書きされない
        /// ＝軍団隊形（並べ方）と艦隊陣形は別レイヤー。
        /// </summary>
        [UnityTest]
        public IEnumerator CorpsFormationBroadcast_DoesNotOverrideFleetHold()
        {
            const string corps = "QA分離軍団";
            BuildFleet("QA分離軍団長", corps, Faction.同盟, new Vector2(0f, 0f), true);
            GameObject subGo = BuildFleet("QA分離隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            BuildFleet("QA分離敵", "", Faction.帝国, new Vector2(8f, 0f));

            var mgrGo = new GameObject("QA_BattlefieldCommandManager4");
            var mgr = mgrGo.AddComponent<BattlefieldCommandManager>();
            mgr.resolveInterval = 0.05f;
            spawned.Add(mgrGo);
            yield return null;

            var subSq = subGo.GetComponent<Squadron>();
            subGo.GetComponent<FleetAI>().autoFormation = false;

            subSq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);

            // 軍団長の発令が何度も走る時間だけ回す（兵力は減らさない＝総退却させない）。
            float until = Time.time + 1.0f;
            while (Time.time < until) yield return null;

            Assert.AreEqual(Formation.円陣, subSq.currentFormation,
                "軍団長の陣形発令が艦隊の手動保持を上書きした");
            Assert.IsTrue(subSq.IsFormationHeld);
            Assert.AreEqual(FormationOrderSource.直接命令, subSq.FormationHold.source);
        }

        // ===== 支援要請の受理経路（レビュー指摘2） =====

        /// <summary>
        /// ★<b>本物の <see cref="SupportRequestDirector"/></b> の受理→承諾を通して陣形が指定され、
        /// 保持になり、以後 AI に上書きされないことを見る。
        /// </summary>
        [UnityTest]
        public IEnumerator SupportRequest_AcceptedFormationBecomesHold()
        {
            GameObject go = BuildFleet("QA支援陣形艦隊", "", Faction.同盟, new Vector2(0f, 0f));
            var sel = go.AddComponent<Selectable>();

            // 応じる気を確実に出すため統率を最大にする（Willingness は統率と士気で決まる）。
            var ad = ScriptableObject.CreateInstance<AdmiralData>();
            ad.hideFlags = HideFlags.DontSave;
            ad.admiralName = "QA協力的";
            ad.leadership = 100;
            go.GetComponent<FleetStrength>().admiralData = ad;

            var dirGo = new GameObject("QA_SupportRequestDirector");
            var dir = dirGo.AddComponent<SupportRequestDirector>();
            dir.replySeconds = 0.05f;
            dir.expireSeconds = 5f;
            spawned.Add(dirGo);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            go.GetComponent<FleetAI>().autoFormation = false;

            long seqBefore = NotificationCenter.All.Count > 0
                ? NotificationCenter.All[NotificationCenter.All.Count - 1].seq : 0;

            dir.RequestFormation(sel, (int)Formation.方陣);

            // ★SupportRequestDirector は game-time（StrategySession.Clock）で判断する。
            //   通常の会戦では BattleManager がこのクロックを進めるが、この試験には居ないので
            //   誰も進めず ElapsedSeconds が止まったまま＝いつまでも「検討中」になる（fix1 の失敗原因）。
            //   BattleManager と同じやり方でクロックを進める（判定は Director に行わせる）。
            GameClock clock = StrategySession.Clock;
            Assert.IsNotNull(clock, "前提：統一クロックが無い");
            clock.paused = false;   // 前の試験/シーンが止めたまま残っていることがある
            clock.speed = 1f;

            float timeout = Time.time + 10f;
            while (!sq.IsFormationHeld && Time.time < timeout)
            {
                if (clock != null) clock.Advance(Time.deltaTime);
                yield return null;
            }

            // どこで止まったか分かるよう、要請にまつわる通知を添えて落とす。
            Assert.IsTrue(sq.IsFormationHeld,
                "承諾された支援要請の陣形が保持にならなかった。経過=" +
                (clock != null ? clock.ElapsedSeconds.ToString("0.00") : "クロックなし") +
                " 秒／通知=[" + RecentSupportMessages(seqBefore) + "]");
            Assert.AreEqual(Formation.方陣, sq.currentFormation);
            Assert.AreEqual(FormationOrderSource.支援要請, sq.FormationHold.source);

            // 以後、軍団AI・艦隊AI は上書きできない。
            Assert.AreEqual(FormationOrderResult.保持により拒否,
                sq.RequestFormation(Formation.横陣, FormationOrderSource.軍団AI));
        }

        // ===== 拒否・所属変更・復帰（初版から維持） =====

        /// <summary>ポイント不足で断られたら、旧指定も保持もポイントもそのまま。</summary>
        [UnityTest]
        public IEnumerator Rejection_KeepsPreviousFormationAndHold()
        {
            GameObject go = BuildFleet("QA拒否艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            go.GetComponent<FleetAI>().autoFormation = false;
            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            float pointsBefore = sq.SkillPoints;

            sq.formationPeaceCost = sq.SkillPointsMax * 10f;   // 変更不可能な高コスト
            sq.formationCombatCostMultiplier = 1f;

            Assert.AreEqual(FormationOrderResult.ポイント不足,
                sq.RequestFormation(Formation.横陣, FormationOrderSource.直接命令));

            Assert.AreEqual(Formation.円陣, sq.currentFormation, "断ったのに陣形が変わっている");
            Assert.IsTrue(sq.IsFormationHeld, "断ったのに保持が消えている");
            Assert.AreEqual(Formation.円陣, sq.FormationHold.formation, "断ったのに旧指定が変わっている");
            Assert.AreEqual(pointsBefore, sq.SkillPoints, 0.001f, "断ったのにポイントを消費している");
        }

        /// <summary>同一陣形はポイントが無くても受理して保持だけ付け直せる。</summary>
        [UnityTest]
        public IEnumerator SameFormation_IsFreeAndSetsHold()
        {
            GameObject go = BuildFleet("QA同一陣形艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            go.GetComponent<FleetAI>().autoFormation = false;
            sq.formationPeaceCost = sq.SkillPointsMax * 10f;
            float pointsBefore = sq.SkillPoints;

            Assert.AreEqual(FormationOrderResult.受理,
                sq.RequestFormation(sq.currentFormation, FormationOrderSource.直接命令));
            Assert.IsTrue(sq.IsFormationHeld, "同一陣形で保持が付いていない");
            Assert.AreEqual(pointsBefore, sq.SkillPoints, 0.001f, "同一陣形なのに消費している");
        }

        /// <summary>敗走したら陣形の保持が解ける。直接命令の移動は止めない。</summary>
        [UnityTest]
        public IEnumerator Rout_ReleasesHold_ButNotMovement()
        {
            GameObject go = BuildFleet("QA敗走艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var mv = go.GetComponent<FleetMovement>();
            var morale = go.GetComponent<FleetMorale>();
            var ai = go.GetComponent<FleetAI>();
            ai.autoFormation = false;

            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            mv.SetDestination(new Vector2(50f, 0f), null);
            ai.BeginManualOverride();
            yield return null;
            Assert.IsTrue(sq.IsFormationHeld);

            // ★敗走の状態を<b>実際に維持</b>させる。
            //   FleetMorale は「非交戦が routedRecoveryDelay(既定4秒) 続いたら回復」する。
            //   この試験の艦は一度も交戦していないので lastCombatTime=0 のままで、
            //   PlayMode の Time.time はとっくに4秒を超えている＝士気を0にした次のフレームに
            //   recoveryRate ぶん回復して IsRouted が false へ戻ってしまう（fix1 の失敗原因）。
            //   回復率という<b>入力条件</b>を止めて、敗走を維持する。
            morale.recoveryRate = 0f;
            morale.ApplyMoraleDelta(-morale.morale);
            Assert.IsTrue(morale.IsRouted, "前提：敗走状態になっていること");

            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsTrue(morale.IsRouted, "前提：敗走が維持されていること（回復で戻っている）");

            Assert.IsFalse(sq.IsFormationHeld, "敗走しても陣形の保持が解けていない");
            Assert.AreEqual(ManualOverrideKind.直接命令, ai.OverrideKind,
                "陣形の解除が直接の移動命令まで止めている（既存の対照を壊している）");
            Assert.IsTrue(mv.IsMoving, "陣形の解除が直接の移動を止めている");
        }

        /// <summary>軍団の所属が変わったら旧指揮系統の保持を解く。</summary>
        [UnityTest]
        public IEnumerator CorpsChange_ReleasesHold()
        {
            GameObject go = BuildFleet("QA配属換え艦隊", "A軍団", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            go.GetComponent<FleetAI>().autoFormation = false;
            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            yield return null;
            Assert.IsTrue(sq.IsFormationHeld);

            go.GetComponent<FleetStrength>().corpsName = "B軍団";
            yield return null;

            Assert.IsFalse(sq.IsFormationHeld, "所属が変わったのに旧系統の保持が残っている");
        }

        /// <summary>ずれた陣形は無料で復帰し、移動先を変えない。</summary>
        [UnityTest]
        public IEnumerator Hold_RestoresDriftedFormation_WithoutStoppingMovement()
        {
            GameObject go = BuildFleet("QA復帰艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var mv = go.GetComponent<FleetMovement>();
            go.GetComponent<FleetAI>().autoFormation = false;

            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            var destination = new Vector2(50f, 0f);
            mv.SetDestination(destination, null);

            sq.currentFormation = Formation.横陣;   // 何らかの経路でずれた状況
            yield return null;

            Assert.AreEqual(Formation.円陣, sq.currentFormation, "保持している陣形へ戻っていない");
            Assert.IsTrue(mv.IsMoving, "陣形の復帰が移動を止めている");
            Assert.AreEqual(destination.x, mv.Destination.x, 0.01f, "陣形の復帰が行き先を変えている");
        }

        // ===== 時間操作を跨ぐ（最終QA・依頼1） =====

        /// <summary>
        /// ★一時停止 → その状態で陣形を指定 → 再開 → 倍速、と時間操作を跨いでも保持が続くこと。
        /// 保持は<b>時間の流れ方と無関係</b>（移動・攻撃と独立しているのと同じ理由）。
        /// timeScale は <see cref="PauseManager"/> と同じ意味で動かし、TearDown で元へ戻す。
        /// </summary>
        [UnityTest]
        public IEnumerator Hold_SurvivesPauseResumeAndFastForward()
        {
            GameObject go = BuildFleet("QA時間操作艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var ai = go.GetComponent<FleetAI>();
            ai.autoFormation = true;      // AI が上書きを狙う状態にしておく
            ai.searchInterval = 0.02f;

            // ① 一時停止中に指定する（停止中でもアクティブポーズで命令できる＝実機と同じ）。
            Time.timeScale = 0f;
            yield return null;
            Assert.AreEqual(FormationOrderResult.受理,
                sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令),
                "一時停止中に陣形を指定できない");
            Assert.IsTrue(sq.IsFormationHeld);

            for (int i = 0; i < 5; i++) yield return null;      // 停止のまま数フレーム
            Assert.IsTrue(sq.IsFormationHeld, "一時停止中に保持が解けた");
            Assert.AreEqual(Formation.円陣, sq.currentFormation);

            // ② 再開。
            Time.timeScale = 1f;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.IsTrue(sq.IsFormationHeld, "再開したら保持が解けた");

            // ③ 倍速（AI の探索周期が実時間で何度も回る）。
            Time.timeScale = 3f;
            float until = Time.time + 1.0f;   // Time.time は timeScale 追従＝会戦時間で1秒ぶん
            while (Time.time < until) yield return null;

            Assert.IsTrue(sq.IsFormationHeld, "倍速で保持が解けた");
            Assert.AreEqual(Formation.円陣, sq.currentFormation, "倍速中に AI へ陣形を上書きされた");
            Assert.AreEqual(FormationOrderSource.直接命令, sq.FormationHold.source);
        }

        // ===== 攻撃の終了・標的消失（最終QA・依頼1） =====

        /// <summary>
        /// ★直接の攻撃命令が終わっても（標的が消えて手動標的が落ちても）陣形の保持は続くこと。
        /// 攻撃の終了は「命令の完了」であって「陣形をやめる理由」ではない。
        /// </summary>
        [UnityTest]
        public IEnumerator Hold_SurvivesAttackOrderEndingAndTargetLoss()
        {
            GameObject go = BuildFleet("QA攻撃保持艦隊", "", Faction.同盟, new Vector2(0f, 0f));
            GameObject enemyGo = BuildFleet("QA攻撃目標", "", Faction.帝国, new Vector2(4f, 0f));
            var enemySquadron = enemyGo.GetComponent<Squadron>();
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var ai = go.GetComponent<FleetAI>();
            var weapon = go.GetComponent<FleetWeapon>();
            ai.autoFormation = false;

            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            weapon.SetManualTargetFleet(enemySquadron);
            ai.BeginManualOverride();          // 直接の攻撃命令（FleetCommander.ConfirmAttack と同じ）
            yield return null;

            Assert.IsTrue(weapon.HasManualTarget, "前提：手動標的が設定されていること");
            Assert.IsTrue(sq.IsFormationHeld);

            // ★標的が消える（撃沈・退却に相当）。
            Object.DestroyImmediate(enemyGo);
            spawned.Remove(enemyGo);

            // 手動標的が落ち、命令が完了して AI へ返るまで進める。
            float timeout = Time.time + 10f;
            while (ai.OverrideKind != ManualOverrideKind.なし && Time.time < timeout) yield return null;

            Assert.IsFalse(weapon.HasManualTarget, "標的消失で手動標的が落ちていない（前提が崩れている）");
            Assert.AreEqual(ManualOverrideKind.なし, ai.OverrideKind, "攻撃命令が終了していない");

            // ここが本題：攻撃が終わっても陣形の保持は続く。
            Assert.IsTrue(sq.IsFormationHeld, "攻撃命令の終了で陣形の保持が解けた（独立していない）");
            Assert.AreEqual(Formation.円陣, sq.currentFormation);
        }

        // ===== 会戦を跨いで漏れない（最終QA・依頼1） =====

        /// <summary>
        /// ★会戦が終わって次の会戦になったとき、前の会戦の状態が漏れないこと。
        ///
        /// 陣形の保持は <see cref="Squadron"/> の非直列化フィールドなので、
        /// 艦隊オブジェクトが破棄されれば一緒に消える（＝新しい艦隊は保持なしで始まる）。
        /// 一方 <b>軍団の総退却の発令は static</b> なので、そちらが漏れると
        /// 次の会戦で同じ軍団名の艦隊が「撤退中」と判定されて陣形を受け付けなくなる。
        /// <see cref="BattlefieldCommandManager"/> の Awake がリセットすることを確かめる。
        /// </summary>
        [UnityTest]
        public IEnumerator NextBattle_DoesNotInheritHoldOrCorpsRetreat()
        {
            const string corps = "QA持越軍団";
            GameObject cmdGo = BuildFleet("QA持越軍団長", corps, Faction.同盟, new Vector2(0f, 0f), true);
            GameObject subGo = BuildFleet("QA持越隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            BuildFleet("QA持越敵", "", Faction.帝国, new Vector2(8f, 0f));

            var mgrGo = new GameObject("QA_BCM_Battle1");
            var mgr = mgrGo.AddComponent<BattlefieldCommandManager>();
            mgr.resolveInterval = 0.05f;
            spawned.Add(mgrGo);
            yield return null;

            var subSq = subGo.GetComponent<Squadron>();
            subGo.GetComponent<FleetAI>().autoFormation = false;
            subSq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);

            string corpsKey = CorpsFormation.KeyFor(subGo.GetComponent<FleetStrength>());

            // 総退却を発令させる（前の会戦で起きたこと）。
            var cmd = cmdGo.GetComponent<FleetStrength>();
            var sub = subGo.GetComponent<FleetStrength>();
            cmd.strength = Mathf.Max(1, Mathf.RoundToInt(cmd.maxStrength * 0.1f));
            sub.strength = Mathf.Max(1, Mathf.RoundToInt(sub.maxStrength * 0.1f));

            float timeout = Time.time + 10f;
            while (!BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey) && Time.time < timeout)
                yield return null;
            Assert.IsTrue(BattlefieldCommandManager.IsCorpsRetreatOrdered(corpsKey),
                "前提：前の会戦で総退却が発令されていること");

            // ★会戦終了＝艦隊も管理役も破棄される（シーンの寿命に相当）。
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            yield return null;

            // ★次の会戦：同じ軍団名で組み直す（キーが一致するので漏れていれば必ず刺さる）。
            GameObject newSubGo = BuildFleet("QA持越隷下", corps, Faction.同盟, new Vector2(2f, 0f));
            var newMgrGo = new GameObject("QA_BCM_Battle2");
            newMgrGo.AddComponent<BattlefieldCommandManager>();   // Awake が static をリセットする
            spawned.Add(newMgrGo);
            yield return null;

            var newSq = newSubGo.GetComponent<Squadron>();
            newSubGo.GetComponent<FleetAI>().autoFormation = false;

            Assert.IsFalse(newSq.IsFormationHeld, "次の会戦へ陣形の保持が漏れている");
            Assert.AreEqual(FormationOrderSource.なし, newSq.LastFormationSource,
                "次の会戦へ指定元が漏れている");
            Assert.IsFalse(BattlefieldCommandManager.IsCorpsRetreatOrdered(
                    CorpsFormation.KeyFor(newSubGo.GetComponent<FleetStrength>())),
                "次の会戦へ総退却の発令状態が漏れている（同じ軍団名の艦隊が撤退中扱いになる）");

            // 漏れていなければ、新しい会戦で普通に陣形を指定できる。
            Assert.AreEqual(FormationOrderResult.受理,
                newSq.RequestFormation(Formation.横陣, FormationOrderSource.直接命令),
                "次の会戦で陣形を指定できない（前の会戦の状態が残っている）");
        }

        /// <summary>保持を明示的に解けば、AI が再び陣形を決められる。</summary>
        [UnityTest]
        public IEnumerator ReleasingHold_ReturnsControlToAi()
        {
            GameObject go = BuildFleet("QA解除艦隊", "", Faction.同盟);
            yield return null;

            var sq = go.GetComponent<Squadron>();
            go.GetComponent<FleetAI>().autoFormation = false;
            sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令);
            Assert.IsTrue(sq.ReleaseFormationHold("QA"));
            Assert.IsFalse(sq.IsFormationHeld);

            Assert.AreEqual(FormationOrderResult.受理,
                sq.RequestFormation(Formation.横陣, FormationOrderSource.艦隊AI));
            Assert.AreEqual(Formation.横陣, sq.currentFormation);
            Assert.IsFalse(sq.IsFormationHeld, "AI の指定で保持が付いている");
        }
    }
}
