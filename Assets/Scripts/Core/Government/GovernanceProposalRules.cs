namespace Ginei
{
    /// <summary>統治政策の上申を受け付けない理由（なし＝上申できる）。判定の順に並べる。</summary>
    public enum GovernanceProposalRejection
    {
        なし,
        /// <summary>対象の星系が地図に無い／カーソルの近くに星系が無い。</summary>
        星系なし,
        /// <summary>他勢力の星系。</summary>
        管轄外,
        /// <summary>星系に内政データ（Province）が無い。</summary>
        内政データなし,
        /// <summary>稟議機構（RingiDirector）が居ない。</summary>
        稟議機構なし,
        /// <summary>プレイヤー勢力の国家状態が無い。</summary>
        勢力状態なし,
        /// <summary>決裁待ちの件数が上限。</summary>
        決裁待ち上限,
        /// <summary>同じ星系への上申が未決で残っている。</summary>
        重複上申,
    }

    /// <summary>上申の見込み（対象・現在の政策・次の政策・受け付けない理由）。画面と実行で同じものを使う。</summary>
    public readonly struct GovernanceProposalPreview
    {
        public readonly int systemId;
        public readonly string systemName;
        public readonly GovernancePolicy current;
        public readonly GovernancePolicy next;
        public readonly GovernanceProposalRejection rejection;

        public GovernanceProposalPreview(int systemId, string systemName, GovernancePolicy current,
                                         GovernancePolicy next, GovernanceProposalRejection rejection)
        {
            this.systemId = systemId;
            this.systemName = systemName ?? "";
            this.current = current;
            this.next = next;
            this.rejection = rejection;
        }

        /// <summary>上申できるか。</summary>
        public bool CanSubmit => rejection == GovernanceProposalRejection.なし;
    }

    /// <summary>
    /// 星系別統治政策の上申の<b>受付判定</b>（#67/#109）。キー（Alt+T）と星系情報パネルのボタンが
    /// 同じ判定を通るよう、条件の順序と理由の文言をここに集める。上申そのもの（稟議→決裁→執行）は
    /// Game 層の RingiDirector が担い、ここは政策を変えない。決裁の権限は <see cref="DecisionAuthorityRules"/>。
    /// </summary>
    public static class GovernanceProposalRules
    {
        /// <summary>カーソルと星系の距離の最小許容（星系の当たり判定がこれより小さくても、ここまでは拾う）。</summary>
        public const float MinPickRadius = 1.2f;

        /// <summary>次に上申する政策（定義順に1つ進め、末尾の次は先頭）。</summary>
        public static GovernancePolicy NextPolicy(GovernancePolicy current)
        {
            int count = System.Enum.GetValues(typeof(GovernancePolicy)).Length;
            int i = (int)current;
            if (i < 0 || i >= count) i = 0;
            return (GovernancePolicy)((i + 1) % count);
        }

        /// <summary>
        /// カーソル位置の星系を上申の対象として拾えるか。星系情報（I キー）と同じく、
        /// 星系ごとの当たり判定（要塞は大きい）と <see cref="MinPickRadius"/> の大きいほうまで許す。
        /// </summary>
        public static bool AcceptsPointerDistance(float distance, float clickRadius)
        {
            if (float.IsNaN(distance) || distance < 0f) return false;
            float r = clickRadius > MinPickRadius ? clickRadius : MinPickRadius;
            return distance <= r;
        }

        /// <summary>受け付けない理由を判定の順に返す（最初に当たったもの）。</summary>
        public static GovernanceProposalRejection Evaluate(
            bool systemFound, Faction owner, Faction player, bool hasProvince,
            bool directorAvailable, bool hasPlayerState,
            int pendingCount, int maxConcurrent, bool duplicatePending)
        {
            if (!systemFound) return GovernanceProposalRejection.星系なし;
            if (owner != player) return GovernanceProposalRejection.管轄外;
            if (!hasProvince) return GovernanceProposalRejection.内政データなし;
            if (!directorAvailable) return GovernanceProposalRejection.稟議機構なし;
            if (!hasPlayerState) return GovernanceProposalRejection.勢力状態なし;
            if (duplicatePending) return GovernanceProposalRejection.重複上申;
            if (pendingCount >= maxConcurrent) return GovernanceProposalRejection.決裁待ち上限;
            return GovernanceProposalRejection.なし;
        }

        /// <summary>理由の文言（画面と通知にそのまま出す）。なしは空。</summary>
        public static string RejectionText(GovernanceProposalRejection r, string systemName)
        {
            string name = string.IsNullOrEmpty(systemName) ? "この星系" : systemName;
            switch (r)
            {
                case GovernanceProposalRejection.なし: return "";
                case GovernanceProposalRejection.星系なし: return "対象の星系がありません（星系にカーソルを合わせてください）";
                case GovernanceProposalRejection.管轄外: return $"{name} は管轄外のため統治政策を上申できません";
                case GovernanceProposalRejection.内政データなし: return $"{name} には内政データがないため上申できません";
                case GovernanceProposalRejection.稟議機構なし: return "稟議機構が利用できないため上申できません";
                case GovernanceProposalRejection.勢力状態なし: return "自勢力の国家状態がないため上申できません";
                case GovernanceProposalRejection.決裁待ち上限: return "決裁待ちが上限です（右下の決裁デスクを先に処理してください）";
                case GovernanceProposalRejection.重複上申: return $"{name} への統治政策の上申が決裁待ちです（決着するまで出せません）";
                default: return "上申できません";
            }
        }
    }
}
