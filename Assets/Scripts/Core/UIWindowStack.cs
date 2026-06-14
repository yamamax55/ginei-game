using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 重ねて開いたウィンドウを「最前面から1枚ずつ」ESCで閉じるための中央スタック（#ウィンドウESC）。
    /// 各シーンのESC所有者（会戦＝<see cref="PauseManager"/>／戦略＝<see cref="GalaxyView"/>）が
    /// <see cref="CloseTopmost"/> を呼び、閉じられる窓が無くなったらシステムメニューへフォールバックする。
    /// 観測オーバーレイ・各モーダルパネルは Awake/Build で <see cref="Register"/> し、<see cref="Unregister"/>
    /// を OnDestroy で呼ぶだけ（ESCの直読みをやめ「手前から閉じる」判定を二重実装しない＝唯一の窓口）。
    /// </summary>
    public static class UIWindowStack
    {
        private sealed class Entry
        {
            public Func<bool> isOpen;
            public Action close;
            public int order;   // z順（大きいほど手前）＝各ウィンドウの canvas.sortingOrder を渡す
            public long seq;     // 登録順（同 order のとき新しい登録ほど手前とみなす tie-break）
            public string name;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static long seqCounter;

        /// <summary>
        /// 閉じられるウィンドウを登録する。<paramref name="isOpen"/> が現在の開閉、<paramref name="close"/> が
        /// 1段閉じる操作（多段ナビの窓は1段戻すだけでもよい）、<paramref name="order"/> は z順（手前ほど大）。
        /// 戻り値のトークンを OnDestroy で <see cref="Unregister"/> へ渡す。
        /// </summary>
        public static object Register(Func<bool> isOpen, Action close, int order, string name = null)
        {
            if (isOpen == null || close == null) return null;
            var e = new Entry { isOpen = isOpen, close = close, order = order, seq = ++seqCounter, name = name };
            entries.Add(e);
            return e;
        }

        /// <summary><see cref="Register"/> が返したトークンを解除する（OnDestroy で呼ぶ）。</summary>
        public static void Unregister(object token)
        {
            if (token is Entry e) entries.Remove(e);
        }

        /// <summary>開いている登録ウィンドウが1つでもあるか。</summary>
        public static bool AnyOpen => FindTopmost() != null;

        /// <summary>
        /// 最前面の開いているウィンドウを1枚だけ閉じる。閉じたら true、開いている窓が無ければ false。
        /// false のとき呼び出し側はシステムメニュー等のフォールバックへ進む。
        /// </summary>
        public static bool CloseTopmost()
        {
            Entry top = FindTopmost();
            if (top == null) return false;
            try { top.close(); }
            catch { entries.Remove(top); } // 破棄済み等は黙って除去
            return true;
        }

        /// <summary>全登録を破棄（テスト/シーン全消去用）。通常は OnDestroy の Unregister で足りる。</summary>
        public static void Clear() => entries.Clear();

        /// <summary>登録数（テスト/デバッグ用）。</summary>
        public static int Count => entries.Count;

        // 開いている窓のうち最前面（order 最大・同 order は新しい登録）を返す。
        // 破棄漏れ（isOpen が例外）は走査ついでにプルーンする。
        private static Entry FindTopmost()
        {
            Entry best = null;
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                bool open;
                try { open = e.isOpen(); }
                catch { entries.RemoveAt(i); continue; } // 破棄済み（MissingReference 等）は除去
                if (!open) continue;
                if (best == null || e.order > best.order || (e.order == best.order && e.seq > best.seq))
                    best = e;
            }
            return best;
        }
    }
}
