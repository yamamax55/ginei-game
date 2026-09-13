namespace Ginei
{
    /// <summary>
    /// 会戦専用オーバーレイ（会戦シーンに自動生成される HUD・決裁デスク等）の
    /// <b>帰属シーン判定の唯一の窓口</b>（WIN-1/WIN-4 の残骸バグ対策）。
    ///
    /// ウィンドウ化会戦では Battle シーンが <b>additive</b> でロードされ、
    /// <b>アクティブシーンは Strategy のまま</b>である。素の <c>new GameObject(...)</c> は
    /// アクティブシーンに入るため、会戦用オブジェクトが Strategy 側に生まれ、
    /// Battle シーンをアンロードしても生き残って動き続ける（＝戦略画面に残骸が残る）。
    /// それを防ぐための「生成先を移すか／動いてよいか」の純判定をここに集約する。
    ///
    /// フルスクリーン会戦（Battle＝アクティブシーン）では <see cref="NeedsSceneMove"/> が false を返し、
    /// 従来どおり移動不要＝後方互換。
    /// </summary>
    public static class BattleOverlayScopeRules
    {
        /// <summary>会戦シーンの名前（`SceneLoader.LoadScene` / Build Settings と一致）。</summary>
        public const string BattleSceneName = "Battle";

        /// <summary>そのシーンに会戦専用オーバーレイを生成してよいか（＝会戦シーンか）。</summary>
        public static bool ShouldHost(string loadedSceneName)
            => loadedSceneName == BattleSceneName;

        /// <summary>
        /// 生成した GameObject を目的シーンへ移す必要があるか。
        /// 目的シーンが有効かつアクティブシーンでない（＝additive ロードされた会戦）ときだけ移す。
        /// </summary>
        public static bool NeedsSceneMove(bool targetSceneValid, bool targetIsActiveScene)
            => targetSceneValid && !targetIsActiveScene;

        /// <summary>
        /// 会戦専用オーバーレイが動作を続けてよいか。
        /// 自分の帰属シーンが会戦シーンであり、かつそのシーンがまだロードされている間だけ true。
        /// Strategy に取り残された個体（hostSceneName="Strategy"）や、アンロード済みのシーンに
        /// 属する個体はここで false になり、決裁カードを生み続けない。
        /// </summary>
        public static bool ShouldRun(string hostSceneName, bool hostSceneLoaded)
            => hostSceneLoaded && hostSceneName == BattleSceneName;
    }
}
