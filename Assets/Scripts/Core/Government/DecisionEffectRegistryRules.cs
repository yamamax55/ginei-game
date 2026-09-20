namespace Ginei
{
    /// <summary>
    /// その決裁の効果が<b>実装されているか</b>を判定する唯一の窓口。
    ///
    /// <b>なぜ要るか</b>：効果キーの解釈は3系統に分かれている
    /// （<see cref="PetitionEffects"/> の国家値、<see cref="DecisionMeterEffects"/> の勝敗メーター、
    /// <see cref="PetitionActionRules"/> の盤面まで届く執行、そして <c>fleet.*</c> の編制）。
    /// どれにも登録が無いキーは<b>裁可しても何も起きない</b>のに「裁可」通知だけが出ていた
    /// （実機で見つかった <c>treaty.sign</c>）。
    ///
    /// <b>方針</b>：文面と違う効果へ勝手に結び替えない（条約と見せて福祉を実行しない）。
    /// 未実装は<b>理由付きで実行不可</b>にして、実装されていないことを画面で分かるようにする。
    /// </summary>
    public static class DecisionEffectRegistryRules
    {
        /// <summary>その効果キーに実際の効果があるか（いずれかのレジストリが知っているか）。</summary>
        public static bool IsImplemented(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return false;   // 空キー＝効果なし
            if (PetitionEffects.Has(effectKey)) return true;      // 国家値（税・民心など）
            if (PetitionActionRules.IsActionKey(effectKey)) return true;   // 盤面まで届く執行
            if (DecisionMeterEffects.Has(effectKey)) return true;          // 勝敗メーター
            // 編制（艦隊の設立・解散）は "fleet.establish:12000" のように引数付き＝前方一致で判定する。
            if (effectKey.StartsWith(FleetEstablishmentRules.EffectEstablish, System.StringComparison.Ordinal)
                || effectKey.StartsWith(FleetEstablishmentRules.EffectDisband, System.StringComparison.Ordinal))
                return true;
            // 星系別統治政策の上申（"governance.policy.{systemId}.{policy}"）＝RingiDirector が Province へ執行する。
            if (GovernanceRules.TryParsePolicyPetitionKey(effectKey, out _, out _)) return true;
            // 省内職位の人事（"civilservice.post:1:{行為}:{省}:{人物}:{段}"・#141）＝RingiDirector が
            // CivilServicePostRules.Execute へ流す。復元できるキーだけを実装済みとみなす（壊れたキーは未実装として理由を出す）。
            if (CivilServiceRingiRules.TryDecode(effectKey, out _)) return true;
            // EventEngineの選択肢効果。Game層のGalaxyViewがイベントIDを復号し、登録済み定義を一度だけ解決する。
            if (EventDecisionRules.TryDecode(effectKey, out _)) return true;
            return false;
        }

        /// <summary>未実装の理由（画面にそのまま出す）。実装済みなら空文字。</summary>
        public static string NotImplementedText(string effectKey)
        {
            if (IsImplemented(effectKey)) return "";
            return string.IsNullOrEmpty(effectKey)
                ? "この決裁には効果が設定されていません（実装待ち）"
                : $"この決裁（{effectKey}）の効果はまだ実装されていません";
        }
    }
}
