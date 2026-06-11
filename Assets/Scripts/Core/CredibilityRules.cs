using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>信認の調整係数（MEYASU-2 #1298）。</summary>
    public readonly struct CredibilityParams
    {
        /// <summary>未接触の箱の初期信認（中立）。</summary>
        public readonly float defaultValue;
        /// <summary>実効傾聴がこれ未満なら壁紙化（その箱は陳情を読まない）。</summary>
        public readonly float wallpaperThreshold;
        /// <summary>不使用で収束する中庸値。</summary>
        public readonly float neutral;
        /// <summary>中庸へ寄る速度/秒。</summary>
        public readonly float decayRate;

        public CredibilityParams(float defaultValue, float wallpaperThreshold, float neutral, float decayRate)
        {
            this.defaultValue = defaultValue;
            this.wallpaperThreshold = wallpaperThreshold;
            this.neutral = neutral;
            this.decayRate = decayRate;
        }

        public static CredibilityParams Default => new CredibilityParams(0.5f, 0.15f, 0.5f, 0.05f);
    }

    /// <summary>
    /// 目安箱の「信認」の唯一の窓口（MEYASU-2 #1298）。箱（国王/政治家/地方）ごとの信認を読み書きする。
    /// 信認は借り物の権威（<see cref="ConsentRules"/>）＝唯一のリミッター（人工マナを置かない）。
    /// <b>実効傾聴＝箱の信認 × <see cref="BoxCredibility.globalDeference"/></b>。これが
    /// <see cref="CredibilityParams.wallpaperThreshold"/> を割ると壁紙化（その箱は陳情を読まない＝オラクル失墜）。
    /// 中央箱（国王/政治家）は勢力につき各1、地方箱は regionKey ごとに疎。基準値非破壊・実効値パターン。test-first。
    /// </summary>
    public static class CredibilityRules
    {
        /// <summary>箱の辞書キー。中央箱（国王/政治家）はスコープ無し、地方箱は regionKey ごと（疎）。</summary>
        public static string Key(BoxKind box, string regionKey = "")
            => box == BoxKind.地方 ? "地方/" + (regionKey ?? "") : box.ToString();

        /// <summary>箱の信認（未記録は既定値）。</summary>
        public static float Of(BoxCredibility c, BoxKind box, string regionKey = "")
            => Of(c, box, CredibilityParams.Default, regionKey);

        public static float Of(BoxCredibility c, BoxKind box, CredibilityParams prm, string regionKey = "")
        {
            if (c == null) return prm.defaultValue;
            return c.entries.TryGetValue(Key(box, regionKey), out float v) ? v : prm.defaultValue;
        }

        /// <summary>信認を delta だけ動かす（未記録なら既定値から・0..1にクランプ）。reason は呼び出し側のログ用。</summary>
        public static void Adjust(BoxCredibility c, BoxKind box, float delta, string regionKey = "")
            => Adjust(c, box, delta, CredibilityParams.Default, regionKey);

        public static void Adjust(BoxCredibility c, BoxKind box, float delta, CredibilityParams prm, string regionKey = "")
        {
            if (c == null) return;
            float cur = Of(c, box, prm, regionKey);
            c.entries[Key(box, regionKey)] = Mathf.Clamp01(cur + delta);
        }

        /// <summary>国家規模の傾聴度を動かす（オラクル失墜/回復・0..1）。</summary>
        public static void AdjustGlobal(BoxCredibility c, float delta)
        {
            if (c == null) return;
            c.globalDeference = Mathf.Clamp01(c.globalDeference + delta);
        }

        /// <summary>実効傾聴＝箱の信認 × 国家規模の傾聴度（建白の通過率に掛ける係数 0..1）。</summary>
        public static float Heed(BoxCredibility c, BoxKind box, string regionKey = "")
            => Heed(c, box, CredibilityParams.Default, regionKey);

        public static float Heed(BoxCredibility c, BoxKind box, CredibilityParams prm, string regionKey = "")
        {
            float g = c == null ? 1f : c.globalDeference;
            return Mathf.Clamp01(Of(c, box, prm, regionKey) * g);
        }

        /// <summary>壁紙化＝実効傾聴が閾値割れ（その箱はもう陳情を読まない）。</summary>
        public static bool IsWallpapered(BoxCredibility c, BoxKind box, CredibilityParams prm, string regionKey = "")
            => Heed(c, box, prm, regionKey) < prm.wallpaperThreshold;

        public static bool IsWallpapered(BoxCredibility c, BoxKind box, string regionKey = "")
            => IsWallpapered(c, box, CredibilityParams.Default, regionKey);

        /// <summary>不使用の信認を中庸へ寄せる（dt 進める）。極端な評価が時間で薄れる。</summary>
        public static void Decay(BoxCredibility c, float dt, CredibilityParams prm)
        {
            if (c == null || dt <= 0f) return;
            // 列挙中に書き換えるためキーを退避してから更新
            var keys = new List<string>(c.entries.Keys);
            foreach (var k in keys)
                c.entries[k] = Mathf.MoveTowards(c.entries[k], prm.neutral, prm.decayRate * dt);
        }

        public static void Decay(BoxCredibility c, float dt) => Decay(c, dt, CredibilityParams.Default);
    }
}
