using System.Collections.Generic;

namespace Ginei
{
    /// <summary>決裁1件がマクロメーター（<see cref="CampaignMeters"/>）に与える増減ベクトル（満額執行時）。実適用は ×magnitude。</summary>
    public readonly struct MeterDelta
    {
        public readonly float military;
        public readonly float support;
        public readonly float treasury;
        public readonly float control;
        public readonly float hegemony;

        public MeterDelta(float military, float support, float treasury, float control, float hegemony)
        {
            this.military = military;
            this.support = support;
            this.treasury = treasury;
            this.control = control;
            this.hegemony = hegemony;
        }

        public static readonly MeterDelta Zero = new MeterDelta(0f, 0f, 0f, 0f, 0f);
    }

    /// <summary>
    /// 決裁の effectKey → マクロメーター増減（<see cref="MeterDelta"/>）のレジストリ（決裁ゲーム MVP）。
    /// <see cref="PetitionEffects"/>（effectKey→FactionState＝裏のシム側）と<b>同じキー</b>を、こちらは勝敗メーター側へ写す
    /// ＝1つの決裁が「世界（税率/民心）」と「勝敗メーター」の両方を同じキーで動かす（ハイブリッドの要）。
    /// すべての決裁はトレードオフ（複数メーターを±）＝Reigns 型に「無料の決裁」を作らない。
    /// 新決裁は <see cref="Register"/> で1行足す（並行新設しない）。純データ（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisionMeterEffects
    {
        private static readonly Dictionary<string, MeterDelta> registry = BuildDefaults();

        private static Dictionary<string, MeterDelta> BuildDefaults()
        {
            // 各 delta は満額執行時の増減（おおむね ±0.04〜0.15）。すべて複数メーターを動かすトレードオフ。
            return new Dictionary<string, MeterDelta>
            {
                // --- 内政（既存 PetitionEffects と同キー） ---
                ["tax.cut"]         = new MeterDelta(0f,    +0.08f, -0.10f, 0f,    0f),    // 減税：民心↑ 国庫↓
                ["tax.hike"]        = new MeterDelta(0f,    -0.08f, +0.10f, 0f,    0f),    // 増税：国庫↑ 民心↓
                ["welfare.up"]      = new MeterDelta(0f,    +0.10f, -0.10f, 0f,    0f),    // 社会保障：民心↑ 国庫↓
                ["reform.inclusive"]= new MeterDelta(0f,    +0.08f, 0f,     -0.06f,0f),    // 包摂：民心↑ 統制↓
                ["order.tighten"]   = new MeterDelta(0f,    -0.08f, 0f,     +0.10f,0f),    // 治安強化：統制↑ 民心↓

                // --- 軍事・外交（決裁ゲーム MVP の新キー＝戦略レイヤーへ効く） ---
                ["mil.mobilize"]    = new MeterDelta(+0.12f,-0.05f, -0.08f, 0f,    0f),    // 動員令：軍事↑ 国庫↓ 民心↓
                ["mil.offensive"]   = new MeterDelta(-0.10f,0f,     -0.05f, 0f,    +0.15f),// 前線攻勢：覇権↑↑ 軍事↓ 国庫↓
                ["mil.defend"]      = new MeterDelta(+0.04f,0f,     -0.06f, +0.10f,0f),    // 防衛強化：統制↑ 軍事↑ 国庫↓
                ["diplo.ceasefire"] = new MeterDelta(+0.03f,+0.12f, 0f,     0f,    -0.10f),// 講和：民心↑ 軍事回復↑ 覇権↓
                ["purge"]           = new MeterDelta(0f,    -0.10f, 0f,     +0.12f,+0.04f),// 粛清：統制↑ 民心↓ 覇権↑少
                ["amnesty"]         = new MeterDelta(0f,    +0.12f, 0f,     -0.08f,0f),    // 恩赦：民心↑ 統制↓
            };
        }

        /// <summary>キーが登録済みか。</summary>
        public static bool Has(string effectKey)
            => !string.IsNullOrEmpty(effectKey) && registry.ContainsKey(effectKey);

        /// <summary>キーの増減ベクトル（未登録は <see cref="MeterDelta.Zero"/>）。</summary>
        public static MeterDelta For(string effectKey)
            => Has(effectKey) ? registry[effectKey] : MeterDelta.Zero;

        public static bool TryGet(string effectKey, out MeterDelta delta)
        {
            if (Has(effectKey)) { delta = registry[effectKey]; return true; }
            delta = MeterDelta.Zero; return false;
        }

        /// <summary>増減を登録/上書きする（新決裁を1行で足す窓口）。</summary>
        public static void Register(string effectKey, MeterDelta delta)
        {
            if (string.IsNullOrEmpty(effectKey)) return;
            registry[effectKey] = delta;
        }
    }
}
