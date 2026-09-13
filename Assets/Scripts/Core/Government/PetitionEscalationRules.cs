using UnityEngine;

namespace Ginei
{
    /// <summary>上申した案件が、決裁権者のところでどうなったか。</summary>
    public enum EscalationOutcome
    {
        /// <summary>まだ返事が来ていない（審査中）。</summary>
        審査中,
        /// <summary>決裁権者が承認した。</summary>
        承認,
        /// <summary>決裁権者が却下した。</summary>
        却下,
        /// <summary>決裁権者が不在になった（死亡・離任）＝差し戻し。</summary>
        決裁者不在,
    }

    /// <summary>上申の審査にかかる時間と、受諾しやすさの調整値。</summary>
    public readonly struct PetitionEscalationParams
    {
        /// <summary>決裁権者が返事をするまでの game-秒。</summary>
        public readonly float reviewSeconds;
        /// <summary>この値以上の「賛意」で承認する（0..1）。</summary>
        public readonly float approveThreshold;

        public PetitionEscalationParams(float reviewSeconds, float approveThreshold)
        {
            this.reviewSeconds = Mathf.Max(0f, reviewSeconds);
            this.approveThreshold = Mathf.Clamp01(approveThreshold);
        }

        /// <summary>既定＝30 game秒で返事・賛意0.5以上で承認。</summary>
        public static PetitionEscalationParams Default => new PetitionEscalationParams(30f, 0.5f);
    }

    /// <summary>
    /// <b>上申の往復</b>（GitHub #67）：権限が無い者の案件を、適任者が受理し・審査し・可否を決め、
    /// 結果が起案者へ戻るまでを扱う。
    ///
    /// <b>これが要る理由</b>：権限外を拒否して終わりでは「上申」ではない。
    /// 決裁権者が実在の人物として受け取り、時間をかけて判断し、その結果で<b>実際に効果が出る（一度だけ）</b>
    /// ところまで通って初めて往復になる。
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>審査の可否は<b>決裁権者の資質</b>から決める（乱数ではない＝同じ状況なら同じ答え）。</item>
    ///   <item>決裁権者が居なくなったら<b>差し戻し</b>＝勝手に代理で承認しない。</item>
    ///   <item>承認されたら効果は<b>決裁権者の権限で</b>1回だけ執行される
    ///   （実行は <see cref="DecisionResolutionRules.ClaimForApply"/> が守る）。</item>
    /// </list>
    ///
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class PetitionEscalationRules
    {
        /// <summary>
        /// 審査が終わったか。<paramref name="elapsedSeconds"/>＝上申してからの game-秒。
        /// </summary>
        public static bool IsReviewDone(float elapsedSeconds, in PetitionEscalationParams p)
            => elapsedSeconds >= p.reviewSeconds;

        /// <summary>
        /// 決裁権者の<b>賛意</b>（0..1・決定論）。
        /// 運営（実務能力）と情報（判断材料の読み）が高いほど、筋の通った案件を通す。
        /// 案件の切迫度（<paramref name="urgency"/>）が高いほど通りやすい。
        /// </summary>
        public static float Favor(int operation, int intelligence, float urgency)
        {
            float ability = Mathf.Clamp01((operation + intelligence) / 200f); // 0..1
            return Mathf.Clamp01(ability * 0.5f + Mathf.Clamp01(urgency) * 0.5f);
        }

        /// <summary>
        /// 上申の結果を判定する。
        /// <paramref name="deciderPresent"/>＝決裁権者がまだ在任・存命か。
        /// </summary>
        public static EscalationOutcome Review(bool deciderPresent, float elapsedSeconds, float favor,
                                               in PetitionEscalationParams p)
        {
            if (!deciderPresent) return EscalationOutcome.決裁者不在;
            if (!IsReviewDone(elapsedSeconds, p)) return EscalationOutcome.審査中;
            return favor >= p.approveThreshold ? EscalationOutcome.承認 : EscalationOutcome.却下;
        }

        /// <summary>結果を知らせる1行（起案者へ戻る返事）。</summary>
        public static string ResultText(EscalationOutcome outcome, string deciderName, string title)
        {
            string who = string.IsNullOrEmpty(deciderName) ? "決裁権者" : deciderName;
            switch (outcome)
            {
                case EscalationOutcome.承認: return $"［上申の裁可］{who} が {title} を承認しました";
                case EscalationOutcome.却下: return $"［上申の却下］{who} が {title} を却下しました";
                case EscalationOutcome.決裁者不在: return $"［差し戻し］{title} は決裁権者が不在のため戻されました";
                default: return $"［上申中］{title} は {who} が審査しています";
            }
        }

        /// <summary>上申カードの説明（誰へ上げて、いつ返事が来るか）。</summary>
        public static string PendingText(string deciderName, float elapsedSeconds, in PetitionEscalationParams p)
        {
            string who = string.IsNullOrEmpty(deciderName) ? "所管" : deciderName;
            float left = Mathf.Max(0f, p.reviewSeconds - elapsedSeconds);
            return $"{who} へ上申中（返事まで およそ {left:0} 秒）";
        }
    }
}
