using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 士気を動かした<b>原因</b>の種別（観測用・#士気原因の切り分け）。
    ///
    /// ★これは<b>記録するための札</b>であって、士気の計算には一切関与しない
    /// （ここを増やしても製品の挙動は変わらない）。
    /// 推測でラベルを貼らないために、<b>書き込んだ側が自分で名乗る</b>形にしてある。
    /// </summary>
    public enum MoraleChangeSource
    {
        その他,
        初期化,
        自然回復,
        交戦低下,
        被弾,
        威圧,
        不退転下限,
        戦況イベント,
        撃墜高揚,
        敗走衝撃,
        捨てがまり高揚,
    }

    /// <summary>士気が動いた1件の記録（観測用・不変）。</summary>
    public readonly struct MoraleChangeRecord
    {
        /// <summary>ゲーム時刻（記録側が渡す＝Core は時計を持たない）。</summary>
        public readonly float gameTime;
        /// <summary>対象艦隊の表示名。</summary>
        public readonly string fleet;
        /// <summary>原因の種別。</summary>
        public readonly MoraleChangeSource source;
        /// <summary>原因の内訳（会戦イベントID・士気波及の事象名など）。無ければ空。</summary>
        public readonly string detail;
        /// <summary>変化前の士気。</summary>
        public readonly float before;
        /// <summary>変化後の士気。</summary>
        public readonly float after;
        /// <summary>変化前に敗走していたか。</summary>
        public readonly bool routedBefore;
        /// <summary>変化後に敗走しているか。</summary>
        public readonly bool routedAfter;
        /// <summary>最後に交戦へ触れてからの秒数（立ち直りの待ち時間と突き合わせるため）。</summary>
        public readonly float secondsSinceCombat;
        /// <summary>そのとき不退転が効いていたか。</summary>
        public readonly bool moraleLocked;

        public MoraleChangeRecord(float gameTime, string fleet, MoraleChangeSource source, string detail,
            float before, float after, bool routedBefore, bool routedAfter,
            float secondsSinceCombat, bool moraleLocked)
        {
            this.gameTime = gameTime;
            this.fleet = fleet;
            this.source = source;
            this.detail = detail;
            this.before = before;
            this.after = after;
            this.routedBefore = routedBefore;
            this.routedAfter = routedAfter;
            this.secondsSinceCombat = secondsSinceCombat;
            this.moraleLocked = moraleLocked;
        }

        /// <summary>増減量（正で上昇）。</summary>
        public float Delta => after - before;

        /// <summary>この変化で敗走が解けたか。</summary>
        public bool ClearedRout => routedBefore && !routedAfter;

        /// <summary>この変化で敗走が始まったか。</summary>
        public bool StartedRout => !routedBefore && routedAfter;
    }

    /// <summary>
    /// 士気の増減を<b>原因つきで</b>残す観測台帳（#士気原因の切り分け・純ロジック・test-first）。
    ///
    /// <b>なぜ要るか</b>：実機の記録には「敗走 解除（士気 0.0→6.0）」のように
    /// <b>結果だけ</b>が写り、何がその 6.0 を入れたのかが残らない。
    /// 自然回復・会戦イベント・撃墜高揚は<b>どれも士気を上げる</b>ので、
    /// 値と時刻だけから原因を言い当てると推測になる。
    /// そこで<b>書き込んだ側が名乗る</b>台帳をここに置き、観測で原因を確定できるようにする。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item><b>既定は無効</b>（<see cref="Enabled"/> が false）＝通常プレイでは1件も積まない。</item>
    ///   <item>有効でも<b>微小な毎フレームの増減は積まない</b>（<see cref="MinAbsDelta"/> 未満は捨てる）。
    ///         ただし<b>敗走の境界をまたいだ変化は大きさに関係なく残す</b>
    ///         ＝「何が敗走を解いたか」を取りこぼさない。</item>
    ///   <item>容量は有界（<see cref="Capacity"/> の環状バッファ）＝終盤ラグ・青天井の履歴を作らない。</item>
    ///   <item>士気の計算には関与しない（<b>観測専用</b>・製品の回復仕様は変えない）。</item>
    /// </list>
    /// </summary>
    public static class MoraleAuditLog
    {
        /// <summary>保持できる最大件数（環状バッファ＝古いものから捨てる）。</summary>
        public const int Capacity = 400;

        /// <summary>既定の記録しきい値（この大きさ未満の増減は積まない）。</summary>
        public const float DefaultMinAbsDelta = 0.01f;

        /// <summary>
        /// 記録するか。<b>既定 false</b>＝通常プレイでは何も積まない（観測はオプトイン）。
        /// </summary>
        public static bool Enabled = false;

        /// <summary>
        /// この大きさ未満の増減は積まない（自然回復の毎フレームのごく小さなティックで溢れさせない）。
        /// 敗走の境界をまたぐ変化はこのしきい値に関係なく積む。
        /// </summary>
        public static float MinAbsDelta = DefaultMinAbsDelta;

        private static readonly List<MoraleChangeRecord> entries = new List<MoraleChangeRecord>();
        private static int dropped;

        /// <summary>いま保持している件数。</summary>
        public static int Count => entries.Count;

        /// <summary>容量あふれで捨てた件数（黙って切り詰めない＝打ち切りを明示する）。</summary>
        public static int Dropped => dropped;

        /// <summary>保持している記録（古い順）。</summary>
        public static IReadOnlyList<MoraleChangeRecord> All => entries;

        /// <summary>
        /// 記録を消す（<b>それだけ</b>）。<see cref="Enabled"/> も <see cref="MinAbsDelta"/> も変えない。
        ///
        /// ★以前はここで <see cref="MinAbsDelta"/> を既定へ戻していたが、
        /// 「観測の途中で区切るために消す」のが主な使い方なので、
        /// <b>消した拍子に設定が戻る</b>のは落とし穴だった
        /// （細かく見る設定にしていても、区切った瞬間に既定へ戻って刻みを取りこぼす）。
        /// 設定を戻したいときは <see cref="ResetSettings"/> を明示的に呼ぶ。
        /// </summary>
        public static void Clear()
        {
            entries.Clear();
            dropped = 0;
        }

        /// <summary>しきい値を既定へ戻す（記録と <see cref="Enabled"/> は変えない）。</summary>
        public static void ResetSettings()
        {
            MinAbsDelta = DefaultMinAbsDelta;
        }

        /// <summary>
        /// この変化を積むべきか。無効なら false、
        /// 敗走の境界をまたぐなら大きさに関係なく true、そうでなければしきい値で判断する。
        /// </summary>
        public static bool ShouldRecord(float before, float after, bool routedBefore, bool routedAfter)
        {
            if (!Enabled) return false;
            if (routedBefore != routedAfter) return true;       // 敗走が動いた＝原因を必ず残す
            float d = after - before;
            if (d < 0f) d = -d;
            return d >= Mathf.Max(0f, MinAbsDelta);
        }

        /// <summary>
        /// 1件を積む（<see cref="ShouldRecord"/> を満たすときだけ）。
        /// 無効・微小変化のときは何もしない＝呼び出し側は条件分岐を書かなくてよい。
        /// </summary>
        public static void Record(float gameTime, string fleet, MoraleChangeSource source, string detail,
            float before, float after, bool routedBefore, bool routedAfter,
            float secondsSinceCombat, bool moraleLocked)
        {
            if (!ShouldRecord(before, after, routedBefore, routedAfter)) return;

            if (entries.Count >= Capacity)
            {
                entries.RemoveAt(0);
                dropped++;
            }
            entries.Add(new MoraleChangeRecord(gameTime, fleet ?? "", source, detail ?? "",
                before, after, routedBefore, routedAfter, secondsSinceCombat, moraleLocked));
        }

        /// <summary>
        /// 敗走を解いた記録のうち<b>最後のもの</b>を返す（見つからなければ false）。
        /// 「何が敗走を解いたか」を1件で答えるための窓口。
        /// </summary>
        public static bool TryGetLastRoutClear(string fleet, out MoraleChangeRecord found)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (!entries[i].ClearedRout) continue;
                if (!string.IsNullOrEmpty(fleet) && entries[i].fleet != fleet) continue;
                found = entries[i];
                return true;
            }
            found = default;
            return false;
        }

        /// <summary>指定の原因の件数（絞り込み用）。</summary>
        public static int CountBySource(MoraleChangeSource source)
        {
            int n = 0;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].source == source) n++;
            return n;
        }
    }
}
