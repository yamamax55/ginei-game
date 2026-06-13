using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 専用旗艦名の解決（#旗艦名・愛着の基盤）。特定の提督が乗艦すると必ず決まった名の旗艦になる
    /// （例：ヤン→ヒューベリオン、ビュコック→リオグランデ）＝プレイヤーが「あの提督のあの艦」と愛着を持てる土台。
    /// 解決の優先順位：①提督アセットの明示指定 `AdmiralData.signatureShipName` → ②既定表（提督名キー） → ③なし("")。
    /// 既定表はデザイナーが <see cref="Register"/> で増やせる（基盤＝データで愛着を仕込む）。
    /// 払い出し（重複なし・永久欠番）は <see cref="ShipNameRegistry"/> が担う＝ここは「誰がどの名か」だけを解く。
    /// 名前体系は勢力非依存。純ロジック・test-first。
    /// </summary>
    public static class SignatureShipRegistry
    {
        // 提督名→専用旗艦名の既定表（銀英伝風の象徴的な組み合わせ。提督アセット側の明示指定があればそちらが優先）。
        private static readonly Dictionary<string, string> defaults = BuildDefaults();

        private static Dictionary<string, string> BuildDefaults()
        {
            return new Dictionary<string, string>
            {
                { "ヤン・ウェンリー", "ヒューベリオン" },
                { "ヤン",             "ヒューベリオン" },
                { "ビュコック",       "リオグランデ" },
                { "ラインハルト",     "ブリュンヒルト" },
                { "キルヒアイス",     "バルバロッサ" },
                { "ミッターマイヤー", "ベイオウルフ" },
                { "ロイエンタール",   "トリスタン" },
                { "ビッテンフェルト", "ケーニヒスティーゲル" },
                { "ミュラー",         "パーツィバル" },
                { "ワーレン",         "サラマンドル" },
                { "ファーレンハイト", "アースグリム" },
                { "アッテンボロー",   "トリグラフ" },
            };
        }

        /// <summary>既定表へ専用旗艦名を登録（提督名キー・上書き可）。空キー/空名は無視。</summary>
        public static void Register(string admiralName, string shipName)
        {
            if (string.IsNullOrEmpty(admiralName) || string.IsNullOrEmpty(shipName)) return;
            defaults[admiralName] = shipName;
        }

        /// <summary>既定表をビルド直後の状態へ戻す（テスト・戦役の作り直し用）。</summary>
        public static void ResetToDefaults()
        {
            defaults.Clear();
            foreach (var kv in BuildDefaults()) defaults[kv.Key] = kv.Value;
        }

        /// <summary>提督名から専用旗艦名を解く（既定表のみ。無ければ ""）。</summary>
        public static string ResolveByName(string admiralName)
            => (!string.IsNullOrEmpty(admiralName) && defaults.TryGetValue(admiralName, out string s)) ? s : "";

        /// <summary>提督名に専用旗艦名が定義されているか（既定表のみ）。</summary>
        public static bool HasSignature(string admiralName) => !string.IsNullOrEmpty(ResolveByName(admiralName));

        /// <summary>
        /// 提督の専用旗艦名を解く。①アセットの明示指定 → ②既定表（admiralName/ShortName/FullName のいずれか一致）→ ③""。
        /// </summary>
        public static string Resolve(AdmiralData admiral)
        {
            if (admiral == null) return "";
            if (!string.IsNullOrEmpty(admiral.signatureShipName)) return admiral.signatureShipName;
            string byName = ResolveByName(admiral.admiralName);
            if (!string.IsNullOrEmpty(byName)) return byName;
            string byShort = ResolveByName(admiral.ShortName);
            if (!string.IsNullOrEmpty(byShort)) return byShort;
            return ResolveByName(admiral.FullName);
        }
    }
}
