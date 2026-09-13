using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 士気を上げた<b>原因</b>を切り分ける（#士気原因の切り分け）。
    ///
    /// 実機の記録には「敗走 解除（士気 0.0→6.0）」のように<b>結果だけ</b>が残る。
    /// 自然回復・会戦イベント（英雄的奮戦＝+6）・敵旗艦撃墜の高揚は<b>どれも士気を上げる</b>ので、
    /// 値と時刻だけで原因を言い当てると推測になる。
    /// ここでは実コンポーネントを動かし、<see cref="MoraleAuditLog"/> が
    /// <b>書いた側の名乗り</b>をそのまま残すことを確かめる。
    ///
    /// <b>この試験で確かめないこと</b>
    /// <list type="bullet">
    ///   <item>会戦イベントが<b>抽選で発火する</b>ところ（<see cref="BattleEventManager"/> の Tick と
    ///         決裁デスク）は Battle シーンが要るため自動試験の対象外＝実機で QA メニューを使って確認する。
    ///         ここで確かめるのは「イベントが入れた +6 が敗走を解き、原因として記録される」ところまで。</item>
    /// </list>
    ///
    /// <b>空振り合格を作らない</b>：不退転が乗っていないことを assert し（前回の混入）、
    /// 判定の瞬間の生存も確かめる。
    /// </summary>
    public class MoraleSourcePlayModeTests
    {
        private const int RawHit = 1000;
        private const int SustainHit = 200;
        private const int StartStrength = 100000;
        private const float FastForward = 8f;

        /// <summary>
        /// 継続被弾の間隔（<b>ゲーム秒</b>）。毎フレーム撃つと実時間あたりのダメージが FPS 依存になり、
        /// headless（描画なし＝FPS 上限なし）では観測中に撃沈される。立ち直りの待ち時間より十分短くする。
        /// </summary>
        private const float HitInterval = 0.5f;

        private readonly System.Collections.Generic.List<GameObject> spawned =
            new System.Collections.Generic.List<GameObject>();

        private float savedTimeScale;

        [SetUp]
        public void SetUp()
        {
            savedTimeScale = Time.timeScale;
            MoraleAuditLog.Clear();
            MoraleAuditLog.Enabled = true;
            MoraleAuditLog.MinAbsDelta = 0f;   // 試験では自然回復の刻みまで残す（Clear では戻らない）
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < spawned.Count; i++)
                if (spawned[i] != null) Object.DestroyImmediate(spawned[i]);
            spawned.Clear();
            Time.timeScale = savedTimeScale;
            MoraleAuditLog.Enabled = false;
            MoraleAuditLog.Clear();
            MoraleAuditLog.ResetSettings();   // 試験用のしきい値を持ち越さない
        }

        /// <summary>
        /// 会戦の艦隊を最小構成で組む。
        /// ★非アクティブで組んでから有効化する（<see cref="FleetStrength"/> が Awake で
        /// <see cref="FleetMorale"/> をキャッシュするため・前task で踏んだ落とし穴）。
        /// ★FleetAI は止める：低士気だと AI が『不退転』を発動して敗走判定を止めてしまい、
        ///   原因の切り分けに混ざる（前task revision8 の失敗）。士気の書込経路は AI を通らない。
        /// </summary>
        private GameObject BuildFleet(string name, Faction faction, Vector2 pos, float recoveryRate)
        {
            var go = new GameObject(name);
            spawned.Add(go);
            go.SetActive(false);
            go.transform.position = pos;

            var strength = go.AddComponent<FleetStrength>();
            strength.faction = faction;
            strength.admiralName = name;
            strength.strength = StartStrength;
            strength.maxStrength = StartStrength;

            var morale = go.AddComponent<FleetMorale>();
            morale.recoveryRate = recoveryRate;
            morale.enableIntimidation = false;   // 威圧は別経路＝今回の切り分けに混ぜない

            go.AddComponent<FleetMovement>();
            go.AddComponent<WeaponArc>();
            go.AddComponent<FleetWeapon>();
            var sq = go.AddComponent<Squadron>();
            sq.escortCount = 0;
            sq.currentFormation = Formation.紡錘陣;
            var ai = go.AddComponent<FleetAI>();
            ai.autoFormation = false;

            go.SetActive(true);
            ai.enabled = false;                  // ★AI の特殊指揮を切り離す
            return go;
        }

        private static void AssertNoMoraleLock(FleetStrength s, string where)
        {
            Assert.IsFalse(s.activeMoraleLock,
                where + "：不退転が乗っている（特殊指揮が混入＝原因の切り分けが崩れる）");
        }

        /// <summary>指定の原因の記録件数。</summary>
        private static int CountOf(MoraleChangeSource source) => MoraleAuditLog.CountBySource(source);

        /// <summary>指定の原因で入った増分の合計（正のぶんだけ）。</summary>
        private static float GainFrom(MoraleChangeSource source, string fleet)
        {
            float sum = 0f;
            System.Collections.Generic.IReadOnlyList<MoraleChangeRecord> all = MoraleAuditLog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].source != source) continue;
                if (!string.IsNullOrEmpty(fleet) && all[i].fleet != fleet) continue;
                if (all[i].Delta > 0f) sum += all[i].Delta;
            }
            return sum;
        }

        /// <summary>
        /// 台帳を<b>欠落なく</b>集計するための試験ローカルの累計。
        /// 自然回復は毎フレーム1件積まれるため、FPS 上限のない環境では
        /// 観測中に容量（<see cref="MoraleAuditLog.Capacity"/>）を超えて古い記録が捨てられる。
        /// 容量は変えず、こまめに集計してから <see cref="MoraleAuditLog.Clear"/> で排出する。
        /// </summary>
        private sealed class AuditTally
        {
            private readonly int[] counts = new int[System.Enum.GetValues(typeof(MoraleChangeSource)).Length];
            public float recoveryGain;
            public int drains;

            public int CountOf(MoraleChangeSource source) => counts[(int)source];

            /// <summary>いまの台帳を累計へ移して消す。捨てられた記録があれば失敗（欠落を合格にしない）。</summary>
            public void Drain(string fleet)
            {
                Assert.AreEqual(0, MoraleAuditLog.Dropped,
                    "台帳が容量あふれで記録を捨てた＝集計に欠落がある（排出が間に合っていない）");
                System.Collections.Generic.IReadOnlyList<MoraleChangeRecord> all = MoraleAuditLog.All;
                for (int i = 0; i < all.Count; i++)
                {
                    counts[(int)all[i].source]++;
                    if (all[i].source != MoraleChangeSource.自然回復) continue;
                    if (!string.IsNullOrEmpty(fleet) && all[i].fleet != fleet) continue;
                    if (all[i].Delta > 0f) recoveryGain += all[i].Delta;
                }
                drains++;
                MoraleAuditLog.Clear();
            }
        }

        // ===== 0. 台帳が実経路を捉えていることの証明 =====

        /// <summary>
        /// ★<b>実際の被弾が「被弾」として記録される</b>ことを先に示す。
        /// これが通らないと、以降の「自然回復が無い」等が
        /// 単に台帳が動いていないだけで真になってしまう（空振り合格）。
        /// </summary>
        [UnityTest]
        public IEnumerator Fixture_RealDamageIsRecordedAsDamage()
        {
            GameObject go = BuildFleet("QA台帳確認", Faction.同盟, Vector2.zero, 0f);
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            MoraleAuditLog.Clear();

            strength.TakeDamage(RawHit);

            Assert.GreaterOrEqual(CountOf(MoraleChangeSource.被弾), 1,
                "実際の被弾が台帳に残らない＝観測が実経路に配線されていない");
            MoraleChangeRecord r = MoraleAuditLog.All[0];
            Assert.AreEqual(MoraleChangeSource.被弾, r.source);
            Assert.AreEqual("QA台帳確認", r.fleet, "艦隊が識別できていない");
            Assert.Less(r.Delta, 0f, "被弾なのに士気が減っていない");
            Assert.IsTrue(strength.IsAlive, "1発で死ぬ被弾量は大きすぎる（空振り）");
        }

        // ===== 1. 非交戦の自然回復（量まで確かめる） =====

        /// <summary>
        /// ★非交戦で放置したときの上昇は<b>自然回復だけ</b>で、
        /// その量は <c>recoveryRate × 経過ゲーム秒</c> に一致する。
        ///
        /// 実機の +6.0 が自然回復で説明できるかを判断する土台＝
        /// 「1秒あたりどれだけ戻るのか」をここで固定する。
        /// </summary>
        [UnityTest]
        public IEnumerator Idle_OnlyNaturalRecovery_AndRateMatches()
        {
            const float Rate = 0.5f;     // ★実機の FleetUnit.prefab と同じ値（クラス既定 0.7 ではない）
            GameObject go = BuildFleet("QA自然回復", Faction.同盟, Vector2.zero, Rate);
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();

            // 士気を下げてから、非交戦のまま放置する。
            strength.TakeDamage(RawHit);
            yield return null;
            MoraleAuditLog.Clear();

            // ★区切ったあとも「細かく見る」設定が生きていること。
            //   revision2 では Clear がしきい値を既定 0.01 へ戻していたため、
            //   自然回復の1フレームぶん（約 0.009 以下）が黙って落ち、件数が 1 しか立たなかった。
            Assert.AreEqual(0f, MoraleAuditLog.MinAbsDelta, 1e-6f,
                "記録の区切りでしきい値が戻っている＝自然回復の刻みを取りこぼす");

            float start = morale.morale;
            float t0 = Time.time;
            Time.timeScale = FastForward;
            // ★毎フレーム集計して排出する（FPS 上限なしだと 3 ゲーム秒で数千件＝容量400を超えて欠落する）。
            var tally = new AuditTally();
            while (Time.time - t0 < 3f)
            {
                AssertNoMoraleLock(strength, "放置の観測中");
                yield return null;
                tally.Drain("QA自然回復");
            }
            float elapsed = Time.time - t0;
            float gained = morale.morale - start;
            tally.Drain("QA自然回復");   // 最後のフレーム以降に積まれたぶん（通常0件）

            Assert.Greater(elapsed, 0f);
            Assert.Greater(tally.drains, 1, "集計が走っていない（試験になっていない）");
            Assert.AreEqual(0, tally.CountOf(MoraleChangeSource.戦況イベント), "放置しただけで会戦イベントが入っている");
            Assert.AreEqual(0, tally.CountOf(MoraleChangeSource.撃墜高揚), "放置しただけで撃墜高揚が入っている");
            Assert.AreEqual(0, tally.CountOf(MoraleChangeSource.捨てがまり高揚), "放置しただけで捨てがまり高揚が入っている");
            Assert.AreEqual(0, tally.CountOf(MoraleChangeSource.威圧), "威圧を切ったのに入っている");
            Assert.Greater(tally.CountOf(MoraleChangeSource.自然回復), 1, "自然回復が記録されていない（試験になっていない）");

            // ★量の一致：上がったぶんは全部「自然回復」で説明できる。
            Assert.AreEqual(gained, tally.recoveryGain, 0.05f,
                "上昇量が自然回復の記録と合わない＝他の原因が混ざっている");
            // ★率の一致：recoveryRate × 経過秒（1フレームぶんの誤差を許す）。
            Assert.AreEqual(Rate * elapsed, gained, Rate * 0.1f + 0.05f,
                "自然回復の量が recoveryRate × 経過ゲーム秒 と合わない（士気 " +
                start.ToString("0.000") + "→" + morale.morale.ToString("0.000") +
                " ／経過 " + elapsed.ToString("0.00") + " 秒）");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        // ===== 2. 敗走中・継続被弾では自然回復が1件も入らない =====

        /// <summary>
        /// ★<b>敗走したまま撃たれ続けている間は、自然回復が1件も記録されない</b>。
        ///
        /// 既存の <c>NaturalRecovery_RoutDoesNotClearWhileStillUnderFire</c> は
        /// 「敗走フラグが解けないこと」を見る。こちらは<b>原因の側</b>から同じ門を見て、
        /// 立ち直りの回復そのものが1度も走っていないことを示す（重複ではなく裏づけ）。
        /// </summary>
        [UnityTest]
        public IEnumerator RoutedUnderFire_NoNaturalRecoveryAtAll()
        {
            GameObject go = BuildFleet("QA継続被弾", Faction.同盟, Vector2.zero, 0.5f);
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            float delay = morale.routedRecoveryDelay;
            Assert.Greater(delay, 0f, "前提：立ち直りの待ち時間が正であること");

            // 敗走させる。
            Time.timeScale = FastForward;
            int budget = 200;
            int hits = 0;
            while (!morale.IsRouted && hits < budget)
            {
                Assert.IsTrue(strength.IsAlive, "敗走へ到達する前に撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                hits++;
                yield return null;
            }
            Assert.IsTrue(morale.IsRouted, "前提：敗走まで到達すること");
            AssertNoMoraleLock(strength, "敗走した時点");

            // ★ここから数える。ゲーム時間で待ち時間より長く撃ち続ける。
            //
            // 継続被弾の証明に<b>台帳の「被弾」件数を使わない</b>：
            //   敗走後の士気は 0 で、そこへ被弾しても下限 0 でクランプされ<b>士気が動かない</b>。
            //   台帳は「変化」を残す仕組みなので、被弾が届いていても0件になる
            //   （revision2 の失敗 Expected>1 / Actual 0 はこれ＝製品ではなく assert の選び方の誤り）。
            //   実際に届いていることは<b>兵力が減ったこと</b>と<b>撃った回数</b>で示す。
            //
            // ★被弾は<b>ゲーム時間の間隔</b>（HitInterval）で行う。毎フレーム撃つと
            //   FPS 上限のない headless では数千発になり観測中に撃沈される（FPS 依存の試験になる）。
            //   代わりに「どのフレームの士気更新から見ても最終被弾が待ち時間より近い」ことを実測で assert する。
            Assert.Less(HitInterval, delay, "前提：被弾の間隔が立ち直りの待ち時間より短いこと");
            MoraleAuditLog.Clear();
            int strengthBefore = strength.strength;
            int shots = 0;
            float t0 = Time.time;
            float nextHit = t0;
            float lastHitTime = t0;
            float maxSinceHit = 0f;
            while (Time.time - t0 < delay + 1f)
            {
                Assert.IsTrue(strength.IsAlive, "観測中に撃沈された（空振り）");
                AssertNoMoraleLock(strength, "継続被弾の観測中");
                if (shots > 0) maxSinceHit = Mathf.Max(maxSinceHit, Time.time - lastHitTime);
                while (nextHit <= Time.time)
                {
                    strength.TakeDamage(SustainHit);
                    shots++;
                    lastHitTime = Time.time;
                    nextHit += HitInterval;
                }
                yield return null;
            }
            float elapsed = Time.time - t0;
            maxSinceHit = Mathf.Max(maxSinceHit, Time.time - lastHitTime);   // 最後のフレームの更新が見た間隔

            Assert.GreaterOrEqual(elapsed, delay,
                "観測がゲーム時間で待ち時間に届いていない＝門を跨げていない試験になっている");
            Assert.GreaterOrEqual(shots, Mathf.FloorToInt((delay + 1f) / HitInterval),
                "ゲーム時間どおりに撃てていない（" + shots + " 発）");
            Assert.Less(maxSinceHit, delay,
                "被弾の切れ目が待ち時間に達した（最大 " + maxSinceHit.ToString("0.00") +
                " 秒）＝継続被弾になっていない");
            Assert.AreEqual(0, MoraleAuditLog.Dropped, "台帳が容量あふれで記録を捨てた＝0件の判定に欠落がありうる");
            Assert.Less(strength.strength, strengthBefore,
                "撃っているのに兵力が減っていない＝被弾が届いていない（空振り）");
            Assert.AreEqual(0f, morale.morale, 0.0001f,
                "前提：敗走中の士気は下限0に張り付いていること（だから被弾では台帳が動かない）");

            // ★立ち直りの回復が1度でも走れば、士気は 0 から正へ動く＝<b>敗走の境界を跨ぐ</b>ので、
            //   しきい値に関係なく必ず台帳へ残る。よって 0 件は「1度も走らなかった」ことを意味する。
            Assert.IsTrue(MoraleAuditLog.Enabled, "台帳が止まっている＝0件が空振りになる");
            Assert.AreEqual(0, CountOf(MoraleChangeSource.自然回復),
                "撃たれ続けているのに自然回復が走っている（" + CountOf(MoraleChangeSource.自然回復) +
                " 件／観測 " + elapsed.ToString("0.0") + " 秒／" + shots + " 発）");
            Assert.IsTrue(morale.IsRouted, "観測の終わりに敗走が解けている");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        // ===== 3. 被弾が止んだあと：待ち時間の前後 =====

        /// <summary>
        /// ★被弾が止んだあと、<b>待ち時間を過ぎてから</b>自然回復が始まる。
        /// 最初の自然回復の記録が持つ「最終交戦からの秒」が
        /// <c>routedRecoveryDelay</c> 以上であることで示す。
        /// </summary>
        [UnityTest]
        public IEnumerator AfterFireStops_FirstRecoveryIsPastTheDelay()
        {
            GameObject go = BuildFleet("QA被弾停止", Faction.同盟, Vector2.zero, 0.5f);
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();
            float delay = morale.routedRecoveryDelay;

            Time.timeScale = FastForward;
            int hits = 0;
            while (!morale.IsRouted && hits < 200)
            {
                Assert.IsTrue(strength.IsAlive, "敗走へ到達する前に撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                hits++;
                yield return null;
            }
            Assert.IsTrue(morale.IsRouted, "前提：敗走まで到達すること");

            // 最後の被弾からの時刻をはっきりさせ、そこから数える。
            strength.TakeDamage(SustainHit);
            float lastHit = Time.time;
            MoraleAuditLog.Clear();

            float until = lastHit + delay + 2f;
            while (Time.time < until)
            {
                AssertNoMoraleLock(strength, "立ち直り待ちの観測中");
                yield return null;
            }

            Assert.Greater(CountOf(MoraleChangeSource.自然回復), 0,
                "待ち時間を過ぎても自然回復が1件も走っていない（回復を殺してしまった）");

            // ★最初の自然回復は待ち時間を過ぎてから。
            System.Collections.Generic.IReadOnlyList<MoraleChangeRecord> all = MoraleAuditLog.All;
            bool foundFirst = false;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].source != MoraleChangeSource.自然回復) continue;
                Assert.GreaterOrEqual(all[i].secondsSinceCombat, delay - 0.05f,
                    "最終交戦から " + all[i].secondsSinceCombat.ToString("0.00") +
                    " 秒（待ち時間 " + delay.ToString("0.0") + " 秒）で自然回復が走っている");
                Assert.GreaterOrEqual(all[i].gameTime - lastHit, delay - 0.05f,
                    "最後の被弾から待ち時間の前に自然回復が走っている");
                foundFirst = true;
                break;
            }
            Assert.IsTrue(foundFirst, "自然回復の記録を取り出せていない");
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        // ===== 4. 会戦イベントによる上昇（原因が区別されること） =====

        /// <summary>
        /// ★<b>会戦イベントぶんの上昇は、自然回復ではなくイベントとして記録される</b>。
        ///
        /// 実機の「敗走 解除（士気 0.0→6.0）」の 6.0 は、自然回復の量では説明できない
        /// （<c>recoveryRate 0.5</c> なら 12 秒ぶん）。英雄的奮戦（+6）が候補だが、
        /// 記録が無ければ推測にとどまる。ここでは<b>実際の <see cref="FleetMorale"/> の
        /// イベント経路</b>（<see cref="BattleEventManager"/> が呼ぶのと同じ窓口）を通し、
        /// 原因が区別され、敗走が解けることを示す。
        ///
        /// ※イベントが抽選で発火するところは Battle シーンが要るため自動試験の範囲外。
        /// </summary>
        [UnityTest]
        public IEnumerator BattleEventGain_IsRecordedAsEventAndClearsRout()
        {
            GameObject go = BuildFleet("QAイベント", Faction.同盟, Vector2.zero, 0f);
            yield return null;

            var strength = go.GetComponent<FleetStrength>();
            var morale = go.GetComponent<FleetMorale>();

            int hits = 0;
            while (!morale.IsRouted && hits < 200)
            {
                Assert.IsTrue(strength.IsAlive, "敗走へ到達する前に撃沈された（空振り）");
                strength.TakeDamage(RawHit);
                hits++;
                yield return null;
            }
            Assert.IsTrue(morale.IsRouted, "前提：敗走まで到達すること");
            AssertNoMoraleLock(strength, "イベント適用の直前");
            MoraleAuditLog.Clear();

            // ★英雄的奮戦と同じ経路・同じ量（BattleEventManager.AdjustPlayerMorale が呼ぶ窓口）。
            morale.ApplyMoraleDelta(6f, MoraleChangeSource.戦況イベント, "battle_heroics");

            Assert.IsFalse(morale.IsRouted, "イベントの +6 で敗走が解けていない");
            Assert.AreEqual(6f, morale.morale, 0.001f, "イベントぶんが士気に入っていない");
            Assert.AreEqual(0, CountOf(MoraleChangeSource.自然回復),
                "イベントぶんが自然回復として数えられている＝原因を取り違えている");

            Assert.IsTrue(MoraleAuditLog.TryGetLastRoutClear("QAイベント", out MoraleChangeRecord r),
                "敗走が解けた原因が記録されていない");
            Assert.AreEqual(MoraleChangeSource.戦況イベント, r.source, "解除の原因が会戦イベントとして残っていない");
            Assert.AreEqual("battle_heroics", r.detail, "どのイベントかが残っていない");
            Assert.AreEqual(6f, r.Delta, 0.001f);
            Assert.IsTrue(strength.IsAlive, "判定の瞬間に艦隊が死んでいる（空振り）");
        }

        // ===== 5. 実際の旗艦撃墜による高揚 =====

        /// <summary>
        /// ★<b>実際に敵旗艦を撃墜した</b>ときの高揚が、撃墜高揚として記録される。
        ///
        /// 結果を書かず、<see cref="FleetStrength.TakeDamage"/> で兵力を尽きさせて
        /// 本物の撃墜解決（<c>ResolveFlagshipDown</c> → <c>DestroyFlagship</c> →
        /// <see cref="MoraleShock"/>）を通す。量は <see cref="MoraleShockRules"/> の
        /// 距離減衰と <see cref="MoraleShock.EnemyElationRatio"/> で決まる。
        /// </summary>
        [UnityTest]
        public IEnumerator RealFlagshipKill_GivesRecordedElationToTheEnemy()
        {
            const float Distance = 4f;   // 波及半径 16 の内側かつ追撃判定 12 の内側

            GameObject victim = BuildFleet("QA撃沈される側", Faction.帝国, Vector2.zero, 0f);
            GameObject observer = BuildFleet("QA高揚する側", Faction.同盟, new Vector2(Distance, 0f), 0f);
            yield return null;

            var vs = victim.GetComponent<FleetStrength>();
            var os = observer.GetComponent<FleetStrength>();
            var om = observer.GetComponent<FleetMorale>();

            Assert.IsTrue(FactionRelations.IsHostile(vs.factionData, vs.faction, os.FactionData, os.Faction),
                "前提：観測側が撃沈される側の敵であること（敵でないと高揚しない）");

            // 観測側の士気を下げておく（上限に張り付いていると高揚が乗らず空振りになる）。
            for (int i = 0; i < 20; i++)
            {
                os.TakeDamage(RawHit);
                yield return null;
            }
            Assert.IsTrue(os.IsAlive, "観測側が先に死んでいる（空振り）");
            float before = om.morale;
            Assert.Less(before, om.maxMorale - 10f, "前提：高揚を受け取れる余地があること");

            MoraleAuditLog.Clear();

            // ★本物の撃墜：兵力を尽きさせる。配下艦0なので捨てがまりにならず撃墜される。
            int guard = 0;
            while (vs.IsAlive && guard < 500)
            {
                vs.TakeDamage(RawHit * 20);
                guard++;
                yield return null;
            }
            Assert.IsFalse(vs.IsAlive, "撃沈まで到達していない（空振り）");

            Assert.GreaterOrEqual(CountOf(MoraleChangeSource.撃墜高揚), 1,
                "実際の旗艦撃墜なのに撃墜高揚が記録されていない");

            float gained = om.morale - before;
            Assert.Greater(gained, 0f, "敵旗艦が落ちたのに観測側の士気が上がっていない");

            // ★量は MoraleShockRules の距離減衰 × 敵高揚比で説明できる。
            float expected = MoraleShockRules.ShockAt(MoraleEvent.旗艦撃墜, Distance, MoraleShock.Radius)
                             * MoraleShock.EnemyElationRatio;
            Assert.AreEqual(expected, GainFrom(MoraleChangeSource.撃墜高揚, "QA高揚する側"), 0.01f,
                "高揚の量が MoraleShockRules の距離減衰と合わない");
            Assert.AreEqual(0, CountOf(MoraleChangeSource.戦況イベント),
                "撃墜の高揚が会戦イベントとして数えられている＝原因を取り違えている");
            Assert.IsTrue(os.IsAlive, "判定の瞬間に観測側が死んでいる（空振り）");
        }
    }
}
