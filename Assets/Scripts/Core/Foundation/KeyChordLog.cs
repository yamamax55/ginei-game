using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Ginei
{
    /// <summary>
    /// キー押下を<b>入力イベントの順に</b>、その瞬間の修飾（Ctrl/Alt）と一緒に残す短い記録（#107）。
    ///
    /// フレーム末の状態（押下中・押した・離した）だけでは「Alt を離す→P を押す」と「Alt を押したまま P→Alt を離す」が
    /// 同じフレームに収まったとき区別できない（前者は素の P、後者は Alt+P）。そこで Game 層の記録係が
    /// 入力イベントを1件ずつ見て「キーが上がった→下がった瞬間に修飾が押されていたか」をここへ積み、
    /// <see cref="GameInput.WasPressed"/> がそのフレームの記録で判定する。
    ///
    /// 純データ（キー読み取りなし・フレーム番号は呼び出し側が渡す）。古いフレームの記録は積むときに捨て、
    /// 1フレームの件数は <see cref="Capacity"/> で打ち切る（打ち切った件数は <see cref="DroppedCount"/> に残す）。
    /// </summary>
    public sealed class KeyChordLog
    {
        /// <summary>1フレームに残す押下の既定上限（通常の打鍵では届かない量）。</summary>
        public const int DefaultCapacity = 64;

        private readonly struct Entry
        {
            public readonly Key key;
            public readonly bool ctrl;
            public readonly bool alt;
            public readonly int frame;

            public Entry(Key key, bool ctrl, bool alt, int frame)
            {
                this.key = key;
                this.ctrl = ctrl;
                this.alt = alt;
                this.frame = frame;
            }
        }

        private readonly List<Entry> entries = new List<Entry>();

        /// <summary>1フレームに残す押下の上限（1 未満は 1）。</summary>
        public int Capacity { get; }

        /// <summary>上限を超えて捨てた押下の累計（黙って切り捨てない＝記録係が警告に使う）。</summary>
        public int DroppedCount { get; private set; }

        /// <summary>いま残っている記録の件数（直近フレームぶん）。</summary>
        public int Count => entries.Count;

        public KeyChordLog(int capacity = DefaultCapacity)
        {
            Capacity = capacity < 1 ? 1 : capacity;
        }

        /// <summary>
        /// キーが押された瞬間を記録する。<paramref name="ctrl"/>/<paramref name="alt"/>＝その押下を含むイベントを
        /// 適用した後に修飾が押されていたか。<paramref name="frame"/> より古い記録はここで捨てる。
        /// 記録できたら true（上限超過で捨てたら false）。
        /// </summary>
        public bool Record(Key key, bool ctrl, bool alt, int frame)
        {
            Prune(frame);
            if (entries.Count >= Capacity)
            {
                DroppedCount++;
                return false;
            }
            entries.Add(new Entry(key, ctrl, alt, frame));
            return true;
        }

        /// <summary>そのフレームにそのキーの押下記録があるか（修飾は問わない）。</summary>
        public bool HasPress(Key key, int frame)
        {
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].frame == frame && entries[i].key == key) return true;
            return false;
        }

        /// <summary>そのフレームに、そのキーが<b>ちょうどその修飾で</b>押された記録があるか。</summary>
        public bool PressedWith(Key key, bool ctrl, bool alt, int frame)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (e.frame == frame && e.key == key && e.ctrl == ctrl && e.alt == alt) return true;
            }
            return false;
        }

        /// <summary>i 番目の記録（診断表示用）。範囲外なら false。</summary>
        public bool TryGet(int index, out Key key, out bool ctrl, out bool alt, out int frame)
        {
            if (index < 0 || index >= entries.Count)
            {
                key = Key.None; ctrl = false; alt = false; frame = 0;
                return false;
            }
            Entry e = entries[index];
            key = e.key; ctrl = e.ctrl; alt = e.alt; frame = e.frame;
            return true;
        }

        /// <summary><paramref name="frame"/> より前のフレームの記録を捨てる。</summary>
        public void Prune(int frame)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
                if (entries[i].frame < frame) entries.RemoveAt(i);
        }

        /// <summary>全記録を捨てる（打ち切り件数は残す）。</summary>
        public void Clear() => entries.Clear();
    }
}
