using NUnit.Framework;
using UnityEngine.InputSystem;
using Ginei;

namespace Ginei.Tests
{
    /// <summary>
    /// イベント順の押下記録（<see cref="KeyChordLog"/>）と <see cref="GameInput.ChordMatches"/> の純判定を固定する。
    /// 「Alt を離してから P を押した」が1フレームに収まっても Alt+P にしないこと、
    /// 記録が無いキーはフレーム状態の判定へ戻すこと（hasRecord=false）、古いフレーム・上限の扱い。
    /// 実際の Input System イベントでの確認は PlayMode の GameInputChordPlayModeTests。
    /// </summary>
    public class KeyChordLogTests
    {
        private const int Frame = 10;

        [Test]
        public void Record_KeepsModifierAtPressMoment()
        {
            var log = new KeyChordLog();
            Assert.IsTrue(log.Record(Key.T, ctrl: false, alt: true, frame: Frame));
            Assert.IsTrue(log.HasPress(Key.T, Frame));
            Assert.IsTrue(log.PressedWith(Key.T, false, true, Frame));
            Assert.IsFalse(log.PressedWith(Key.T, false, false, Frame), "Alt 付きで押した T は素の T ではない");
            Assert.IsFalse(log.HasPress(Key.P, Frame));
        }

        [Test]
        public void AltReleasedThenBareP_SameFrame_IsPersonNotProduction()
        {
            // イベント順：Alt↑（前フレームから押していた）→ P↓ ＝ P を押した瞬間の Alt は離れている
            var log = new KeyChordLog();
            log.Record(Key.P, ctrl: false, alt: false, frame: Frame);

            Assert.IsTrue(GameInput.TryGetBinding(GameAction.人物名鑑切替, out InputBinding person));
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.生産観測切替, out InputBinding production));

            Assert.IsTrue(GameInput.ChordMatches(person, InputContext.戦略, log, Frame, out bool r1));
            Assert.IsTrue(r1);
            Assert.IsFalse(GameInput.ChordMatches(production, InputContext.戦略, log, Frame, out bool r2),
                "Alt を離した後の P は生産観測（Alt+P）にしない");
            Assert.IsTrue(r2, "記録があるのでフレーム状態の判定へ戻さない");

            // 対照：フレーム状態だけの判定は同じ状況を Alt+P と見なす（これを記録で上書きする）
            var releasedThisFrame = new ModifierSample(held: false, releasedThisFrame: true);
            Assert.IsTrue(GameInput.BindingMatches(production, InputContext.戦略, true, ModifierSample.Up, releasedThisFrame));
        }

        [Test]
        public void AltHeldThenP_ThenAltReleased_SameFrame_IsProduction()
        {
            var log = new KeyChordLog();
            log.Record(Key.P, ctrl: false, alt: true, frame: Frame); // P↓ の時点で Alt は押下中
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.人物名鑑切替, out InputBinding person));
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.生産観測切替, out InputBinding production));

            Assert.IsTrue(GameInput.ChordMatches(production, InputContext.戦略, log, Frame, out _));
            Assert.IsFalse(GameInput.ChordMatches(person, InputContext.戦略, log, Frame, out _));
        }

        [Test]
        public void BareP_ThenAltP_SameFrame_FiresBoth_EachOnce()
        {
            // 1フレームに2回押した（P、続けて Alt+P）＝それぞれのアクションが出る
            var log = new KeyChordLog();
            log.Record(Key.P, false, false, Frame);
            log.Record(Key.LeftAlt, false, true, Frame);
            log.Record(Key.P, false, true, Frame);
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.人物名鑑切替, out InputBinding person));
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.生産観測切替, out InputBinding production));
            Assert.IsTrue(GameInput.ChordMatches(person, InputContext.戦略, log, Frame, out _));
            Assert.IsTrue(GameInput.ChordMatches(production, InputContext.戦略, log, Frame, out _));
        }

        [Test]
        public void ChordMatches_NoRecord_ReportsNoRecord()
        {
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.統治政策上申, out InputBinding gov));
            Assert.IsFalse(GameInput.ChordMatches(gov, InputContext.戦略, null, Frame, out bool nullLog));
            Assert.IsFalse(nullLog, "記録係が無ければフレーム状態の判定へ戻す");

            var log = new KeyChordLog();
            log.Record(Key.P, false, true, Frame);
            Assert.IsFalse(GameInput.ChordMatches(gov, InputContext.戦略, log, Frame, out bool other));
            Assert.IsFalse(other, "T の記録が無い");
        }

        [Test]
        public void ChordMatches_WrongContext_DoesNotFire_ButHasRecord()
        {
            var log = new KeyChordLog();
            log.Record(Key.T, false, true, Frame);
            Assert.IsTrue(GameInput.TryGetBinding(GameAction.統治政策上申, out InputBinding gov));
            Assert.IsFalse(GameInput.ChordMatches(gov, InputContext.会戦, log, Frame, out bool rec));
            Assert.IsTrue(rec);
            Assert.IsTrue(GameInput.ChordMatches(gov, InputContext.戦略, log, Frame, out _));
        }

        [Test]
        public void OlderFrames_AreIgnoredAndPruned()
        {
            var log = new KeyChordLog();
            log.Record(Key.T, false, true, Frame);
            Assert.IsFalse(log.HasPress(Key.T, Frame + 1), "次のフレームに持ち越さない");

            log.Record(Key.P, false, false, Frame + 1); // 積むときに古いフレームを捨てる
            Assert.AreEqual(1, log.Count);
            Assert.IsFalse(log.HasPress(Key.T, Frame));
            Assert.IsTrue(log.TryGet(0, out Key k, out bool c, out bool a, out int f));
            Assert.AreEqual(Key.P, k);
            Assert.IsFalse(c);
            Assert.IsFalse(a);
            Assert.AreEqual(Frame + 1, f);
            Assert.IsFalse(log.TryGet(1, out _, out _, out _, out _));
            Assert.IsFalse(log.TryGet(-1, out _, out _, out _, out _));
        }

        [Test]
        public void Capacity_DropsAndCounts_NotSilently()
        {
            var log = new KeyChordLog(capacity: 2);
            Assert.AreEqual(2, log.Capacity);
            Assert.IsTrue(log.Record(Key.A, false, false, Frame));
            Assert.IsTrue(log.Record(Key.B, false, false, Frame));
            Assert.IsFalse(log.Record(Key.C, false, false, Frame));
            Assert.AreEqual(1, log.DroppedCount);
            Assert.AreEqual(2, log.Count);

            Assert.AreEqual(1, new KeyChordLog(0).Capacity, "0 以下は 1 に丸める");
            Assert.AreEqual(KeyChordLog.DefaultCapacity, new KeyChordLog().Capacity);

            log.Clear();
            Assert.AreEqual(0, log.Count);
            Assert.AreEqual(1, log.DroppedCount, "Clear は打ち切り件数を消さない");
        }
    }
}
