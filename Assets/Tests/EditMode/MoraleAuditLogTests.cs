using NUnit.Framework;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// 士気の原因台帳（観測専用）。
    ///
    /// 実機の記録には「敗走 解除（士気 0.0→6.0）」のように<b>結果だけ</b>が残り、
    /// 何がその 6.0 を入れたのかが写らない。自然回復・会戦イベント・撃墜高揚は
    /// どれも士気を上げるので、値と時刻だけで原因を言い当てると推測になる。
    /// この台帳は<b>書いた側が名乗る</b>ことでそれを確定させる。
    /// </summary>
    public class MoraleAuditLogTests
    {
        [SetUp]
        public void SetUp()
        {
            MoraleAuditLog.Enabled = false;
            MoraleAuditLog.Clear();
            MoraleAuditLog.ResetSettings();   // ★Clear はしきい値を変えないので明示的に戻す（試験間の持ち越し防止）
        }

        [TearDown]
        public void TearDown()
        {
            MoraleAuditLog.Enabled = false;
            MoraleAuditLog.Clear();
            MoraleAuditLog.ResetSettings();   // ★Clear はしきい値を変えないので明示的に戻す（試験間の持ち越し防止）
        }

        private static void Rec(MoraleChangeSource source, float before, float after,
            bool routedBefore, bool routedAfter, string fleet = "QA艦隊", string detail = "")
        {
            MoraleAuditLog.Record(0f, fleet, source, detail, before, after,
                routedBefore, routedAfter, 0f, false);
        }

        // ===== 既定は無効（通常プレイで積まない） =====

        /// <summary>★既定は無効＝通常プレイでは1件も積まない（ログ洪水を作らない）。</summary>
        [Test]
        public void Disabled_RecordsNothing()
        {
            Assert.IsFalse(MoraleAuditLog.Enabled, "観測は既定で無効であること（オプトイン）");

            Rec(MoraleChangeSource.戦況イベント, 0f, 6f, true, false);
            Rec(MoraleChangeSource.被弾, 50f, 10f, false, false);

            Assert.AreEqual(0, MoraleAuditLog.Count, "無効なのに記録している");
        }

        /// <summary>無効なら敗走の境界をまたいでも積まない（無効は例外なし）。</summary>
        [Test]
        public void Disabled_IgnoresEvenRoutBoundary()
        {
            Assert.IsFalse(MoraleAuditLog.ShouldRecord(0f, 6f, routedBefore: true, routedAfter: false));
        }

        // ===== 有効時のしきい値 =====

        /// <summary>★微小な毎フレームの自然回復ティックは積まない（しきい値未満）。</summary>
        [Test]
        public void Enabled_SkipsTinyChanges()
        {
            MoraleAuditLog.Enabled = true;

            // 自然回復1ティック ≒ recoveryRate(0.5) × deltaTime(0.005) = 0.0025
            Rec(MoraleChangeSource.自然回復, 10f, 10.0025f, false, false);

            Assert.AreEqual(0, MoraleAuditLog.Count, "微小な増減まで積むとログが溢れる");
        }

        /// <summary>しきい値ちょうどは積む（境界）。</summary>
        [Test]
        public void Enabled_RecordsAtExactlyTheThreshold()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.自然回復, 10f, 10f + MoraleAuditLog.DefaultMinAbsDelta, false, false);

            Assert.AreEqual(1, MoraleAuditLog.Count, "しきい値ちょうどの増減が積まれていない");
        }

        /// <summary>減少側も同じしきい値で判断する（絶対値）。</summary>
        [Test]
        public void Enabled_ThresholdUsesAbsoluteValue()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.被弾, 10f, 9.999f, false, false);   // -0.001＝しきい値未満
            Assert.AreEqual(0, MoraleAuditLog.Count);

            Rec(MoraleChangeSource.被弾, 10f, 8f, false, false);       // -2＝十分大きい
            Assert.AreEqual(1, MoraleAuditLog.Count);
        }

        /// <summary>しきい値は差し替えられる（細かく見たいときのため）。</summary>
        [Test]
        public void MinAbsDelta_IsConfigurable()
        {
            MoraleAuditLog.Enabled = true;
            MoraleAuditLog.MinAbsDelta = 0f;

            Rec(MoraleChangeSource.自然回復, 10f, 10.0025f, false, false);

            Assert.AreEqual(1, MoraleAuditLog.Count, "しきい値0なら微小変化も積めること");
        }

        // ===== 敗走の境界は必ず残す =====

        /// <summary>
        /// ★<b>敗走の境界をまたぐ変化はしきい値に関係なく積む</b>。
        /// 「何が敗走を解いたか」はこの試験の目的そのものなので、取りこぼしてはいけない。
        /// 自然回復の1ティック（0.0025）で敗走が解ける実機の挙動が、まさにこれに当たる。
        /// </summary>
        [Test]
        public void RoutBoundary_IsAlwaysRecordedRegardlessOfSize()
        {
            MoraleAuditLog.Enabled = true;

            // 自然回復のごく小さな1ティックで敗走が解けた（実機で起きた形）。
            Rec(MoraleChangeSource.自然回復, 0f, 0.0025f, routedBefore: true, routedAfter: false);

            Assert.AreEqual(1, MoraleAuditLog.Count,
                "敗走が解けた瞬間を、増減が小さいという理由で捨てている");
            Assert.IsTrue(MoraleAuditLog.All[0].ClearedRout);
        }

        /// <summary>敗走が始まった瞬間も同じく必ず残す。</summary>
        [Test]
        public void RoutStart_IsAlwaysRecorded()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.被弾, 0.001f, 0f, routedBefore: false, routedAfter: true);

            Assert.AreEqual(1, MoraleAuditLog.Count);
            Assert.IsTrue(MoraleAuditLog.All[0].StartedRout);
            Assert.IsFalse(MoraleAuditLog.All[0].ClearedRout);
        }

        // ===== 記録の中身 =====

        /// <summary>記録は原因・内訳・前後の士気・敗走・最終交戦からの時間をそのまま保つ。</summary>
        [Test]
        public void Record_KeepsAllFields()
        {
            MoraleAuditLog.Enabled = true;

            MoraleAuditLog.Record(123.5f, "QA自軍2", MoraleChangeSource.戦況イベント, "battle_heroics",
                0f, 6f, routedBefore: true, routedAfter: false,
                secondsSinceCombat: 7.9f, moraleLocked: false);

            MoraleChangeRecord r = MoraleAuditLog.All[0];
            Assert.AreEqual(123.5f, r.gameTime, 1e-4f);
            Assert.AreEqual("QA自軍2", r.fleet);
            Assert.AreEqual(MoraleChangeSource.戦況イベント, r.source);
            Assert.AreEqual("battle_heroics", r.detail);
            Assert.AreEqual(0f, r.before, 1e-4f);
            Assert.AreEqual(6f, r.after, 1e-4f);
            Assert.AreEqual(6f, r.Delta, 1e-4f);
            Assert.IsTrue(r.ClearedRout);
            Assert.AreEqual(7.9f, r.secondsSinceCombat, 1e-4f);
            Assert.IsFalse(r.moraleLocked);
        }

        /// <summary>null の艦隊名・内訳でも壊れない（空文字に正規化）。</summary>
        [Test]
        public void Record_IsNullSafe()
        {
            MoraleAuditLog.Enabled = true;

            MoraleAuditLog.Record(0f, null, MoraleChangeSource.その他, null,
                0f, 6f, true, false, 0f, false);

            Assert.AreEqual("", MoraleAuditLog.All[0].fleet);
            Assert.AreEqual("", MoraleAuditLog.All[0].detail);
        }

        // ===== 有界（終盤ラグを作らない） =====

        /// <summary>★容量は有界＝古いものから捨て、捨てた件数を明示する（黙って切り詰めない）。</summary>
        [Test]
        public void Capacity_IsBoundedAndReportsDrops()
        {
            MoraleAuditLog.Enabled = true;

            for (int i = 0; i < MoraleAuditLog.Capacity + 25; i++)
                Rec(MoraleChangeSource.被弾, 50f, 40f, false, false, "QA艦隊" + i);

            Assert.AreEqual(MoraleAuditLog.Capacity, MoraleAuditLog.Count, "容量を超えて保持している");
            Assert.AreEqual(25, MoraleAuditLog.Dropped, "捨てた件数が数えられていない");
            Assert.AreEqual("QA艦隊25", MoraleAuditLog.All[0].fleet, "古いものから捨てていない");
        }

        /// <summary>消去で件数も捨てた件数も0に戻る。</summary>
        [Test]
        public void Clear_ResetsCountAndDropped()
        {
            MoraleAuditLog.Enabled = true;
            for (int i = 0; i < MoraleAuditLog.Capacity + 3; i++)
                Rec(MoraleChangeSource.被弾, 50f, 40f, false, false);

            MoraleAuditLog.Clear();

            Assert.AreEqual(0, MoraleAuditLog.Count);
            Assert.AreEqual(0, MoraleAuditLog.Dropped);
            Assert.IsTrue(MoraleAuditLog.Enabled, "Clear は有効/無効を変えないこと");
        }

        /// <summary>
        /// ★<b>消去はしきい値を変えない</b>。
        ///
        /// 観測の途中で区切るために消すのが主な使い方なので、
        /// 消した拍子に「細かく見る」設定が既定へ戻ると、
        /// <b>そのあとの自然回復の刻みを黙って取りこぼす</b>
        /// （revision2 の <c>Idle_OnlyNaturalRecovery_AndRateMatches</c> 失敗の原因）。
        /// </summary>
        [Test]
        public void Clear_DoesNotTouchTheThreshold()
        {
            MoraleAuditLog.Enabled = true;
            MoraleAuditLog.MinAbsDelta = 0f;      // 細かく見る設定
            Rec(MoraleChangeSource.被弾, 50f, 40f, false, false);

            MoraleAuditLog.Clear();

            Assert.AreEqual(0f, MoraleAuditLog.MinAbsDelta, 1e-6f,
                "消去でしきい値が戻ってしまう（細かい刻みを黙って落とす）");

            // 消したあとも、細かい刻みが積めること。
            Rec(MoraleChangeSource.自然回復, 10f, 10.0025f, false, false);
            Assert.AreEqual(1, MoraleAuditLog.Count, "消去後に設定が失われている");
        }

        /// <summary>しきい値を戻すのは明示的な操作（記録と有効/無効は変えない）。</summary>
        [Test]
        public void ResetSettings_RestoresThresholdOnly()
        {
            MoraleAuditLog.Enabled = true;
            MoraleAuditLog.MinAbsDelta = 0f;
            Rec(MoraleChangeSource.被弾, 50f, 40f, false, false);

            MoraleAuditLog.ResetSettings();

            Assert.AreEqual(MoraleAuditLog.DefaultMinAbsDelta, MoraleAuditLog.MinAbsDelta, 1e-4f);
            Assert.AreEqual(1, MoraleAuditLog.Count, "ResetSettings は記録を消さないこと");
            Assert.IsTrue(MoraleAuditLog.Enabled, "ResetSettings は有効/無効を変えないこと");
        }

        // ===== 「何が敗走を解いたか」の逆引き =====

        /// <summary>★最後に敗走を解いた記録を引ける（この試験の目的そのもの）。</summary>
        [Test]
        public void TryGetLastRoutClear_FindsTheCause()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.被弾, 3.6f, 0f, false, true, "QA自軍2");
            Rec(MoraleChangeSource.戦況イベント, 0f, 6f, true, false, "QA自軍2", "battle_heroics");
            Rec(MoraleChangeSource.被弾, 6f, 2.1f, false, false, "QA自軍2");

            Assert.IsTrue(MoraleAuditLog.TryGetLastRoutClear("QA自軍2", out MoraleChangeRecord r));
            Assert.AreEqual(MoraleChangeSource.戦況イベント, r.source);
            Assert.AreEqual("battle_heroics", r.detail);
            Assert.AreEqual(6f, r.after, 1e-4f);
        }

        /// <summary>艦隊名で絞り込める（他艦隊の解除を取り違えない）。</summary>
        [Test]
        public void TryGetLastRoutClear_FiltersByFleet()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.戦況イベント, 0f, 6f, true, false, "QA自軍2");
            Rec(MoraleChangeSource.撃墜高揚, 0f, 4.4f, true, false, "QA敵1");

            Assert.IsTrue(MoraleAuditLog.TryGetLastRoutClear("QA自軍2", out MoraleChangeRecord own));
            Assert.AreEqual(MoraleChangeSource.戦況イベント, own.source);

            Assert.IsTrue(MoraleAuditLog.TryGetLastRoutClear("QA敵1", out MoraleChangeRecord foe));
            Assert.AreEqual(MoraleChangeSource.撃墜高揚, foe.source);
        }

        /// <summary>艦隊名を省けば全体の最後の解除を返す。</summary>
        [Test]
        public void TryGetLastRoutClear_WithoutFleetReturnsLatest()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.戦況イベント, 0f, 6f, true, false, "QA自軍2");
            Rec(MoraleChangeSource.撃墜高揚, 0f, 4.4f, true, false, "QA敵1");

            Assert.IsTrue(MoraleAuditLog.TryGetLastRoutClear(null, out MoraleChangeRecord r));
            Assert.AreEqual(MoraleChangeSource.撃墜高揚, r.source);
        }

        /// <summary>解除が無ければ false（無い物を作らない）。</summary>
        [Test]
        public void TryGetLastRoutClear_ReturnsFalseWhenNone()
        {
            MoraleAuditLog.Enabled = true;
            Rec(MoraleChangeSource.被弾, 50f, 40f, false, false);

            Assert.IsFalse(MoraleAuditLog.TryGetLastRoutClear("QA艦隊", out _));
        }

        /// <summary>原因ごとの件数を数えられる（切り分けの集計用）。</summary>
        [Test]
        public void CountBySource_Counts()
        {
            MoraleAuditLog.Enabled = true;

            Rec(MoraleChangeSource.自然回復, 0f, 1f, false, false);
            Rec(MoraleChangeSource.自然回復, 1f, 2f, false, false);
            Rec(MoraleChangeSource.戦況イベント, 2f, 8f, false, false);

            Assert.AreEqual(2, MoraleAuditLog.CountBySource(MoraleChangeSource.自然回復));
            Assert.AreEqual(1, MoraleAuditLog.CountBySource(MoraleChangeSource.戦況イベント));
            Assert.AreEqual(0, MoraleAuditLog.CountBySource(MoraleChangeSource.撃墜高揚));
        }
    }
}
