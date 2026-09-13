using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 不退転（#2175）中に敗走が反復しないことを、<b>実コンポーネント</b>で確かめる。
    ///
    /// 今回の不具合は<b>更新順</b>（被弾はフレーム中のいつでも来るのに、士気を戻すのは1フレームに1回）
    /// なので、実物を動かさないと再現しない。
    ///
    /// <b>空振り合格を作らない</b>のがこの試験の要点：
    /// <list type="bullet">
    ///   <item>組み立ての正しさを <see cref="Fixture_DamageActuallyDrainsMorale"/> で<b>先に証明</b>する
    ///         （被弾が士気を削れていなければ「敗走しない」は自明に真になってしまう）。</item>
    ///   <item>各試験で<b>士気が実際に下限まで落ちたこと</b>を assert する。</item>
    ///   <item>肝心の判定の瞬間に<b>艦隊が生存していること</b>を assert する
    ///         （死亡や未到達でループを抜けた合格にしない）。</item>
    /// </list>
    ///
    /// <b>結果は書かない</b>：不退転は本物の <see cref="ActiveCommandState.Issue"/>、
    /// 士気は <see cref="FleetStrength.TakeDamage"/>（通常の被弾経路）だけで動かす。
    /// </summary>
    public class MoraleLockRoutPlayModeTests
    {
        /// <summary>
        /// 1発あたりの素ダメージ。
        /// <b>士気は1発あたりの上限（<c>maxSingleHitMoraleFraction</c>）まで削れるが、
        /// 艦隊は死なない</b>大きさに選ぶ＝死亡で試験が空振りするのを避ける。
        /// </summary>
        private const int RawHit = 1000;

        /// <summary>初期兵力（上の1発を何十回受けても尽きない）。</summary>
        private const int StartStrength = 100000;

        private readonly System.Collections.Generic.List<GameObject> spawned =
            new System.Collections.Generic.List<GameObject>();

        private float savedTimeScale;

        [SetUp]
        public void SetUp() => savedTimeScale = Time.timeScale;

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            Time.timeScale = savedTimeScale;
        }

        /// <summary>
        /// 会戦の艦隊を最小構成で組む。
        ///
        /// ★<b>いったん非アクティブにしてから全部品を付け、最後に有効化する</b>。
        /// <see cref="FleetStrength"/> は <c>Awake</c> で <c>GetComponent&lt;FleetMorale&gt;()</c> を
        /// キャッシュするため、順に <c>AddComponent</c> すると
        /// <b>FleetStrength の Awake 時点で FleetMorale がまだ無く、被弾が士気を削らなくなる</b>
        /// （前回の 2件失敗＋4件の空振り合格はこれが原因だった）。
        /// 実機はプレハブ生成なので全部品が同時に揃う＝製品側の問題ではない。
        /// </summary>
        private GameObject BuildFleet(string name)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.SetActive(false);                 // ★Awake を全部品が揃うまで遅らせる
            go.transform.position = Vector3.zero;

            var strength = go.AddComponent<FleetStrength>();
            strength.faction = Faction.同盟;
            strength.admiralName = name;
            strength.strength = StartStrength;
            strength.maxStrength = StartStrength;

            var morale = go.AddComponent<FleetMorale>();
            morale.recoveryRate = 0f;            // 試験入力：自然回復で戻らないようにする

            go.AddComponent<FleetMovement>();
            go.AddComponent<WeaponArc>();
            go.AddComponent<FleetWeapon>();
            var sq = go.AddComponent<Squadron>();
            sq.escortCount = 0;
            sq.currentFormation = Formation.紡錘陣;
            var ai = go.AddComponent<FleetAI>();
            ai.autoFormation = false;

            go.SetActive(true);                  // ここで全部品の Awake が走る
            return go;
        }

        /// <summary>この構成で士気を0まで削るのに必要な被弾回数（1発あたりの上限から逆算）＋余裕。</summary>
        private static int HitsToDrainMorale(FleetMorale morale)
        {
            float capPerHit = morale.maxMorale * Mathf.Clamp01(morale.maxSingleHitMoraleFraction);
            if (capPerHit <= 0f) return 60;
            return Mathf.CeilToInt(morale.maxMorale / capPerHit) + 3;
        }

        /// <summary>
        /// ★<b>不退転が乗っていないこと</b>を確かめる（自然回復2件の前提）。
        ///
        /// 不退転が乗ると <see cref="MoraleLockRules.IsRouted"/> は
        /// <b>士気に関係なく false</b> になり、士気も下限 <see cref="MoraleLockRules.LockedFloor"/>(=1)
        /// で止まる。これは製品として正しい挙動だが、
        /// 「敗走が続くか」を見る試験に混ざると<b>敗走が解けたように見える</b>。
        /// revision8 の失敗（非敗走 545 回・最終士気 <b>1.014</b>＝下限1のすぐ上）が実際にそれだった。
        /// </summary>
        private static void AssertNoMoraleLock(FleetStrength strength, string where)
        {
            Assert.IsFalse(strength.activeMoraleLock,
                where + "：不退転が乗っている（特殊指揮が混入＝この試験の前提が崩れている。" +
                "敗走の継続ではなく下限1で止まっているだけになる）");
        }

        /// <summary>不退転を本物の経路で発動する。</summary>
        private static void Activate不退転(GameObject go)
        {
            Assert.IsTrue(ActiveCommandState.Issue(go.GetComponent<FleetStrength>(), ActiveCommand.不退転),
                "不退転を発動できなかった（前提が組めていない）");
            Assert.IsTrue(go.GetComponent<FleetStrength>().activeMoraleLock,
                "前提：不退転の効果が乗っていること");
        }

        // ===== 0. 組み立ての証明（これが通らないと以降は全部空振り） =====

        /// <summary>
        /// ★<b>被弾が実際に士気を削ること</b>を先に証明する。
        /// これが無いと「不退転中は敗走しない」が、
        /// 単に士気が動いていないだけで真になってしまう（前回の空振り合格）。
        /// </summary>
        [UnityTest]
        public IEnumerator Fixture_DamageActuallyDrainsMorale()
        {
            GameObject go = BuildFleet("QA組立確認艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();

            float before = morale.morale;
            Assert.Greater(before, 0f, "前提：初期士気が正であること");
            Assert.IsFalse(strength.activeMoraleLock, "前提：不退転は乗っていないこと");

            strength.TakeDamage(RawHit);

            Assert.Less(morale.morale, before,
                "被弾しても士気が減っていない＝FleetStrength が FleetMorale を掴めていない" +
                "（この構成では以降の試験がすべて空振りになる）");
            Assert.IsTrue(strength.IsAlive, "1発で死んでしまう被弾量は大きすぎる");
        }

        /// <summary>
        /// ★効果なしで、この被弾のやり方なら<b>確実に敗走まで到達する</b>ことを示す対照。
        /// 以降の「不退転中は敗走しない」は、この条件と同等の負荷で比較する。
        /// </summary>
        [UnityTest]
        public IEnumerator WithoutLock_NormalRoutStillHappens()
        {
            GameObject go = BuildFleet("QA通常敗走艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            Assert.IsFalse(strength.activeMoraleLock, "前提：効果は乗っていないこと");

            int budget = HitsToDrainMorale(morale);
            int hits = 0;
            while (!morale.IsRouted && hits < budget)
            {
                Assert.IsTrue(strength.IsAlive, "敗走へ到達する前に撃沈された（被弾量が大きすぎる）");
                strength.TakeDamage(RawHit);
                hits++;
                yield return null;
            }

            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
            // ★万一 AI が特殊指揮で不退転を発動していたら、敗走しないのは当然＝
            //   「通常の敗走が起きない」ではなく混入として報せる（revision8 の失敗と同じ取り違えを避ける）。
            AssertNoMoraleLock(strength, "通常敗走の判定時");
            Assert.IsTrue(morale.IsRouted,
                "不退転なしの通常の敗走が起きない（" + hits + " 発／士気 " + morale.morale.ToString("0.0") + "）");
            Assert.LessOrEqual(morale.morale, 0f, "敗走判定と士気の値が食い違っている");
        }

        // ===== 芯：被弾直後（AI の更新前）に敗走にならない =====

        /// <summary>
        /// ★効果中に、<b>対照と同じやり方で同じだけ被弾しても</b>
        /// その場（次の Update を待たずに）敗走にならない。
        /// ここが false だと、被弾から次の士気更新までの隙に
        /// AI・陣形保持・支援要請が「敗走した」と読んで1フレーム反復になる。
        /// </summary>
        [UnityTest]
        public IEnumerator Locked_NotRoutedImmediatelyAfterDamage()
        {
            GameObject go = BuildFleet("QA不退転艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            Activate不退転(go);

            int budget = HitsToDrainMorale(morale) * 2;   // 対照より多めに叩く
            for (int i = 0; i < budget; i++)
            {
                Assert.IsTrue(strength.IsAlive, "被弾 " + i + " 回目までに撃沈された（空振り）");
                strength.TakeDamage(RawHit);

                // ★Update を挟まずにその場で読む（実機の観測窓と同じ条件）。
                Assert.IsFalse(morale.IsRouted,
                    "被弾 " + i + " 回目の直後に敗走になった（更新順に依存している）");
            }

            // ★ちゃんと危ない所まで削れたことを示す（空振りでない証明）。
            Assert.AreEqual(MoraleLockRules.LockedFloor, morale.morale, 0.001f,
                "士気が下限まで落ちていない＝危険な条件を踏めていない（空振り）");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        /// <summary>★フレームを跨いでも敗走が反転しない（記録に出た反復そのもの）。</summary>
        [UnityTest]
        public IEnumerator Locked_RoutFlagDoesNotOscillateAcrossFrames()
        {
            GameObject go = BuildFleet("QA反復確認艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            Activate不退転(go);

            int budget = HitsToDrainMorale(morale) * 2;
            int routedSeen = 0;
            for (int i = 0; i < budget; i++)
            {
                Assert.IsTrue(strength.IsAlive, "被弾 " + i + " 回目までに撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                if (morale.IsRouted) routedSeen++;               // 被弾直後
                yield return null;
                if (morale.IsRouted) routedSeen++;               // Update 後
            }

            Assert.AreEqual(0, routedSeen,
                "不退転中に敗走状態が観測された（" + routedSeen + " 回）＝反復が残っている");
            Assert.AreEqual(MoraleLockRules.LockedFloor, morale.morale, 0.001f,
                "士気が下限まで落ちていない＝危険な条件を踏めていない（空振り）");
        }

        // ===== 巻き添えになっていた仕組み =====

        /// <summary>★効果中の被弾で陣形の保持が解けないこと。</summary>
        [UnityTest]
        public IEnumerator Locked_FormationHoldSurvivesDamage()
        {
            GameObject go = BuildFleet("QA不退転保持艦隊");
            yield return null;

            var sq = go.GetComponent<Squadron>();
            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            Assert.AreEqual(FormationOrderResult.受理,
                sq.RequestFormation(Formation.円陣, FormationOrderSource.直接命令));
            Activate不退転(go);

            int budget = HitsToDrainMorale(morale) * 2;
            for (int i = 0; i < budget; i++)
            {
                Assert.IsTrue(strength.IsAlive, "被弾 " + i + " 回目までに撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                yield return null;
            }

            Assert.AreEqual(MoraleLockRules.LockedFloor, morale.morale, 0.001f,
                "士気が下限まで落ちていない＝危険な条件を踏めていない（空振り）");
            Assert.IsTrue(sq.IsFormationHeld, "不退転中の被弾で陣形の保持が解けた");
            Assert.AreEqual(Formation.円陣, sq.currentFormation);
        }

        /// <summary>★効果中の被弾で支援の命令が中断されないこと。</summary>
        [UnityTest]
        public IEnumerator Locked_SupportOrderIsNotInterruptedByDamage()
        {
            GameObject go = BuildFleet("QA不退転支援艦隊");
            yield return null;

            var ai = go.GetComponent<FleetAI>();
            var mv = go.GetComponent<FleetMovement>();
            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();

            mv.SetDestination(new Vector2(80f, 0f), null);
            ai.BeginManualOverride(ManualOverrideKind.支援要請);
            Activate不退転(go);

            int budget = HitsToDrainMorale(morale) * 2;
            for (int i = 0; i < budget; i++)
            {
                Assert.IsTrue(strength.IsAlive, "被弾 " + i + " 回目までに撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                yield return null;
            }

            Assert.AreEqual(MoraleLockRules.LockedFloor, morale.morale, 0.001f,
                "士気が下限まで落ちていない＝危険な条件を踏めていない（空振り）");
            Assert.AreEqual(ManualOverrideKind.支援要請, ai.OverrideKind,
                "不退転中の被弾で支援の命令が中断された");
            Assert.AreNotEqual(FleetAI.AIState.撤退, ai.currentState,
                "不退転中なのに撤退へ移った");
        }

        // ===== 自然回復を有効にした対照（実機で出た敗走/解除の反復） =====

        /// <summary>
        /// 自然回復を<b>既定のまま</b>（止めずに）組む。
        /// 上の試験群は <c>recoveryRate = 0</c> で回復を止めているため、
        /// 実機で出た「敗走 → 次フレームに解除」の反復を検出できない。
        /// </summary>
        private GameObject BuildFleetWithNaturalRecovery(string name)
        {
            GameObject go = BuildFleet(name);
            var morale = go.GetComponent<FleetMorale>();
            morale.recoveryRate = 0.7f;     // ★既定のまま＝自然回復あり（試験入力で止めない）

            // ★AI の特殊指揮を切り離す（revision8 の失敗の原因）。
            //
            // FleetAI は searchInterval(既定2秒) ごとに ConsiderActiveCommand を呼び、
            // BattleAiRules.TryChooseCommand は <b>士気比 &lt; 0.4 なら必ず 不退転</b> を選ぶ。
            // 敗走中は士気比が 0 なので毎回これに当たり、
            // admiralData 無しの AiSkill()=0.5＝1/2 の抽選を1度でも通れば発動する。
            // 不退転の持続は 8 秒（ActiveCommandRules.Spec）＝この試験の観測窓 5 秒を丸ごと覆うため、
            // 以後は士気が下限1で固定され IsRouted が false になり、
            // <b>敗走が解け続けているように見える</b>（記録：非敗走 545 回・最終士気 1.014）。
            // autoFormation=false は陣形の自動切替を止めるだけで、特殊指揮は止まらない。
            //
            // ★止めるのは<b>これだけ</b>：立ち直りの門
            //   FleetStrength.TakeDamage → FleetMorale.OnTakeDamage → FleetMorale.Update
            //   → RoutRecoveryRules.CanRecover は FleetAI を通らないので、
            //   試験対象の経路はそのまま実物が動く（回復も被弾も無効化しない）。
            //   不退転が乗っていないことは観測中も毎フレーム assert する（AssertNoMoraleLock）。
            go.GetComponent<FleetAI>().enabled = false;
            return go;
        }

        /// <summary>継続被弾に使う小さめの素ダメージ（長く撃ち続けても艦隊が死なない量）。</summary>
        private const int SustainHit = 200;

        /// <summary>長い待ちを実時間で短く済ませるための倍速（ゲーム時間はこの倍で進む）。</summary>
        private const float FastForward = 8f;

        /// <summary>継続被弾の間隔（<b>ゲーム秒</b>）。毎フレームだとダメージが FPS 依存になる。待ち時間より十分短く。</summary>
        private const float HitInterval = 0.5f;

        /// <summary>
        /// ★<b>立ち直りの門が開いた状態</b>を作ってから敗走させる（試験の前提づくり）。
        ///
        /// 立ち直りは「交戦が <c>routedRecoveryDelay</c> 秒途切れたら」始まる。
        /// 修正前は<b>被弾が交戦として数えられていなかった</b>ので、
        /// 一度も撃ち合っていないこの構成では待ち時間がとっくに満ちていて、
        /// 敗走した次のフレームに立ち直ってしまっていた。
        ///
        /// そこで<b>まず非交戦のままゲーム時間で待ち時間を超過させる</b>。
        /// こうすると「修正前なら確実に門が開いている」状態になり、
        /// そのうえで敗走が解けなければ、被弾が待ち時間を数え直している証拠になる。
        /// （フレーム数で数えると FPS 次第で 4 秒に届かず、修正前でも通ってしまう。）
        /// </summary>
        private IEnumerator EstablishOpenRecoveryGateThenRout(
            FleetStrength strength, FleetMorale morale)
        {
            float delay = morale.routedRecoveryDelay;
            Assert.Greater(delay, 0f, "前提：立ち直りの待ち時間が正であること");
            AssertNoMoraleLock(strength, "前提づくりの開始時");

            // ① 非交戦のまま、ゲーム時間で待ち時間を超過させる。
            Time.timeScale = FastForward;
            float idleStart = Time.time;
            while (Time.time - idleStart < delay + 1f)
            {
                AssertNoMoraleLock(strength, "非交戦の待機中");
                yield return null;
            }
            float idleElapsed = Time.time - idleStart;
            Assert.GreaterOrEqual(idleElapsed, delay,
                "前提：非交戦の経過がゲーム時間で待ち時間を超えていること（超えないと修正前でも通る）");

            // ② 通常の被弾で敗走させる。
            int budget = HitsToDrainMorale(morale) * 2;
            int hits = 0;
            while (!morale.IsRouted && hits < budget)
            {
                Assert.IsTrue(strength.IsAlive, "敗走へ到達する前に撃沈された（空振り）");
                AssertNoMoraleLock(strength, "敗走させる被弾中");
                strength.TakeDamage(RawHit);
                hits++;
                yield return null;
            }
            AssertNoMoraleLock(strength, "敗走した時点");
            Assert.IsTrue(morale.IsRouted,
                "前提：自然回復ありでも敗走まで到達すること（士気 " + morale.morale.ToString("0.000") + "）");
            Assert.IsTrue(strength.IsAlive, "前提：敗走した時点で生存していること");
        }

        /// <summary>
        /// ★<b>撃たれ続けている間は敗走が解けない</b>こと。
        ///
        /// 実機の記録では「敗走 開始（士気 2.0→0.0）→ 次フレームに 敗走 解除（士気 0.0→0.0）」が
        /// 反復していた。回復は <c>recoveryRate × deltaTime</c> のごく小さな正値なので、
        /// 立ち直りの待ち時間が効いていないと、被弾のたびにこの往復が起きる。
        ///
        /// 観測は<b>フレーム数ではなくゲーム時間</b>で行い、
        /// 継続被弾の区間が待ち時間より長いことを assert する
        /// （フレーム数だと FPS 次第で待ち時間に届かず、修正前でも通ってしまう）。
        ///
        /// ★観測を長くしたことで、今度は<b>AI の特殊指揮（不退転）が混入</b>した
        /// （revision8 の失敗：非敗走 545 回・最終士気 1.014＝下限1のすぐ上）。
        /// 不退転は敗走を止めるのが仕様なので、これは製品の不具合ではなく<b>試験条件の欠陥</b>。
        /// <see cref="BuildFleetWithNaturalRecovery"/> で FleetAI を止めて切り分け、
        /// 乗っていないことを観測中も毎フレーム確かめる（<see cref="AssertNoMoraleLock"/>）。
        /// </summary>
        [UnityTest]
        public IEnumerator NaturalRecovery_RoutDoesNotClearWhileStillUnderFire()
        {
            GameObject go = BuildFleetWithNaturalRecovery("QA自然回復艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            float delay = morale.routedRecoveryDelay;

            yield return EstablishOpenRecoveryGateThenRout(strength, morale);

            // ★被弾を続けている間、敗走が一度も解けないこと（ゲーム時間で待ち時間より長く観測）。
            //
            // ★被弾は<b>ゲーム時間の間隔</b>（HitInterval）で行う。毎フレーム撃つと
            //   FPS 上限のない headless では数千発になり観測中に撃沈される（FPS 依存の試験になる）。
            //   「どのフレームの士気更新から見ても最終被弾が待ち時間より近い」ことは実測で assert する。
            Assert.Less(HitInterval, delay, "前提：被弾の間隔が立ち直りの待ち時間より短いこと");
            int clearedWhileUnderFire = 0;
            int frames = 0;
            int shots = 0;
            int strengthBefore = strength.strength;
            float fireStart = Time.time;
            float nextHit = fireStart;
            float lastHitTime = fireStart;
            float maxSinceHit = 0f;
            while (Time.time - fireStart < delay + 1f)
            {
                Assert.IsTrue(strength.IsAlive, "観測中に撃沈された（空振り）");
                // ★観測中も不退転が乗らないことを確かめる。乗ったまま数えると
                //   「敗走が解けた」ではなく「下限1で止まっている」を数えてしまう。
                AssertNoMoraleLock(strength, "継続被弾の観測中");
                if (shots > 0) maxSinceHit = Mathf.Max(maxSinceHit, Time.time - lastHitTime);
                while (nextHit <= Time.time)
                {
                    strength.TakeDamage(SustainHit);
                    shots++;
                    lastHitTime = Time.time;
                    nextHit += HitInterval;
                }
                if (!morale.IsRouted) clearedWhileUnderFire++;   // 被弾判定の直後
                frames++;
                yield return null;
                if (!morale.IsRouted) clearedWhileUnderFire++;   // Update 後
            }
            float fireElapsed = Time.time - fireStart;
            maxSinceHit = Mathf.Max(maxSinceHit, Time.time - lastHitTime);   // 最後のフレームの更新が見た間隔
            AssertNoMoraleLock(strength, "継続被弾の観測後");

            Assert.GreaterOrEqual(fireElapsed, delay,
                "継続被弾の観測がゲーム時間で待ち時間に届いていない＝修正前でも通る試験になっている");
            Assert.Greater(frames, 1, "フレームが進んでいない（試験になっていない）");
            Assert.GreaterOrEqual(shots, Mathf.FloorToInt((delay + 1f) / HitInterval),
                "ゲーム時間どおりに撃てていない（" + shots + " 発）");
            Assert.Less(maxSinceHit, delay,
                "被弾の切れ目が待ち時間に達した（最大 " + maxSinceHit.ToString("0.00") +
                " 秒）＝継続被弾になっていない");
            Assert.Less(strength.strength, strengthBefore,
                "撃っているのに兵力が減っていない＝被弾が届いていない（空振り）");
            Assert.AreEqual(0, clearedWhileUnderFire,
                "撃たれ続けているのに敗走が解けた（" + clearedWhileUnderFire + " 回／士気 " +
                morale.morale.ToString("0.000") + "／観測 " + fireElapsed.ToString("0.0") + " 秒）" +
                "＝実機の反復が残っている");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        /// <summary>
        /// ★<b>最後の被弾からの境界</b>を実コンポーネントで確かめる。
        /// 待ち時間<b>未満</b>のあいだは敗走が続き、待ち時間を<b>過ぎたら</b>立ち直る
        /// （修正で立ち直りそのものを殺していないことの対照でもある）。
        /// </summary>
        [UnityTest]
        public IEnumerator NaturalRecovery_RoutPersistsUntilDelayThenRecovers()
        {
            GameObject go = BuildFleetWithNaturalRecovery("QA立ち直り艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            float delay = morale.routedRecoveryDelay;

            yield return EstablishOpenRecoveryGateThenRout(strength, morale);

            // 最後の被弾の時刻をはっきりさせる。
            strength.TakeDamage(SustainHit);
            float lastHit = Time.time;
            Assert.IsTrue(morale.IsRouted, "前提：最後の被弾の時点で敗走していること");

            // ★待ち時間<b>未満</b>のあいだは敗走が続く（ここで解けたら反復が残っている）。
            int heldFrames = 0;
            while (Time.time - lastHit < delay * 0.9f)
            {
                // ★不退転が乗ると IsRouted は士気に関係なく false になる＝
                //   「敗走が解けた」と「不退転で踏みとどまった」を取り違えないよう先に切り分ける。
                AssertNoMoraleLock(strength, "待ち時間未満の観測中");
                Assert.IsTrue(morale.IsRouted,
                    "最後の被弾から " + (Time.time - lastHit).ToString("0.00") +
                    " 秒（待ち時間 " + delay.ToString("0.0") + " 秒）で敗走が解けた");
                heldFrames++;
                yield return null;
            }
            Assert.Greater(heldFrames, 1, "待ち時間未満の観測が進んでいない（試験になっていない）");

            // ★待ち時間を過ぎたら立ち直る。
            float until = lastHit + delay + 2f;
            while (Time.time < until && morale.IsRouted)
            {
                AssertNoMoraleLock(strength, "立ち直り待ちの観測中");
                yield return null;
            }
            AssertNoMoraleLock(strength, "立ち直りの判定時");

            Assert.IsFalse(morale.IsRouted,
                "攻撃が止んで待ち時間を過ぎても立ち直らない（回復を殺してしまった）");
            Assert.Greater(morale.morale, 0f, "敗走が解けたのに士気が0のまま");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        // ===== 効果終了後 =====

        /// <summary>
        /// ★効果が切れただけでは敗走せず、そのあと被弾すれば通常どおり敗走すること
        /// （不退転が通常の敗走を無効化し続けない）。
        /// </summary>
        [UnityTest]
        public IEnumerator AfterLockExpires_NormalRoutResumes()
        {
            GameObject go = BuildFleet("QA効果終了艦隊");
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            Activate不退転(go);

            // 効果中に下限まで削る。
            int budget = HitsToDrainMorale(morale) * 2;
            for (int i = 0; i < budget; i++)
            {
                Assert.IsTrue(strength.IsAlive, "被弾 " + i + " 回目までに撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                yield return null;
            }
            Assert.AreEqual(MoraleLockRules.LockedFloor, morale.morale, 0.001f,
                "前提：効果中に士気が下限まで落ちていること");
            Assert.IsFalse(morale.IsRouted, "前提：効果中は敗走していないこと");

            // 効果が切れるまで待つ（持続は ActiveCommandRules の spec）。倍速で待ち時間を詰める。
            Time.timeScale = 8f;
            var state = go.GetComponent<ActiveCommandState>();
            float timeout = Time.time + 60f;
            while (state != null && state.IsActive && Time.time < timeout) yield return null;
            Time.timeScale = 1f;
            Assert.IsFalse(strength.activeMoraleLock, "前提：効果が切れていること");

            // 切れただけでは敗走しない。
            yield return null;
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
            Assert.IsFalse(morale.IsRouted, "効果が切れた瞬間に敗走した");

            // そのあと被弾すれば通常どおり敗走する。
            strength.TakeDamage(RawHit);
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
            Assert.IsTrue(morale.IsRouted,
                "効果終了後に通常の敗走が起きない（士気 " + morale.morale.ToString("0.0") + "）");
        }
    }
}
