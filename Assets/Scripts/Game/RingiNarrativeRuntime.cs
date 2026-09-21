using System;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>外部の文面生成器へ渡す読み取り専用の起案材料。状態を書き換えられる実体は渡さない。</summary>
    public readonly struct RingiDraftRequest
    {
        public readonly int decisionId;
        public readonly string title;
        public readonly string effectKey;
        public readonly DecisionSource source;
        public readonly DecisionSeverity severity;
        public readonly string proposerName;

        public RingiDraftRequest(PendingDecision d)
        {
            decisionId = d != null ? d.id : 0;
            title = d != null ? d.title ?? "" : "";
            effectKey = d != null ? d.effectKey ?? "" : "";
            source = d != null ? d.source : DecisionSource.システム;
            severity = d != null ? d.severity : DecisionSeverity.通常;
            proposerName = d != null ? d.proposerName ?? "" : "";
        }
    }

    /// <summary>外部の決裁判断器へ渡すスナップショット。ゲーム状態への参照は持たない。</summary>
    public readonly struct RingiAdjudicationRequest
    {
        public readonly int decisionId;
        public readonly string title;
        public readonly string effectKey;
        public readonly int choiceCount;
        public readonly int fallbackChoiceIndex;

        public RingiAdjudicationRequest(PendingDecision d)
        {
            decisionId = d != null ? d.id : 0;
            title = d != null ? d.title ?? "" : "";
            effectKey = d != null ? d.effectKey ?? "" : "";
            choiceCount = d != null && d.choices != null ? d.choices.Count : 0;
            fallbackChoiceIndex = d != null ? d.defaultChoiceIndex : 0;
        }
    }

    /// <summary>
    /// WHY（文面と判断候補）だけを返す任意プロバイダ。実装は Game/Data 側に置き、Core から参照しない。
    /// false・例外・空文面ならテンプレへ戻る。
    /// </summary>
    public interface IRingiNarrativeProvider
    {
        bool TryDraft(in RingiDraftRequest request, out string prose);
        bool TryAdjudicate(in RingiAdjudicationRequest request, out int choiceIndex, out string reasoning);
    }

    /// <summary>起案文（WHY）を作る。構造化された決定（WHAT）は一切変更しない。</summary>
    public sealed class RingiDrafter
    {
        private readonly IRingiNarrativeProvider provider;

        public RingiDrafter(IRingiNarrativeProvider provider = null) => this.provider = provider;

        public string Draft(PendingDecision decision, string fallback)
        {
            string safeFallback = fallback ?? "";
            if (decision == null || provider == null) return safeFallback;
            try
            {
                return provider.TryDraft(new RingiDraftRequest(decision), out string prose)
                    && !string.IsNullOrWhiteSpace(prose) ? prose.Trim() : safeFallback;
            }
            catch (Exception)
            {
                return safeFallback;
            }
        }
    }

    /// <summary>
    /// 決裁判断候補を受ける窓口。最終確定は必ず DecisionDeck.Resolve を通し、
    /// 選択範囲・権限・解決済み・効果レジストリを共通経路で再検証する。
    /// </summary>
    public sealed class RingiAdjudicator
    {
        private readonly IRingiNarrativeProvider provider;

        public RingiAdjudicator(IRingiNarrativeProvider provider = null) => this.provider = provider;

        public bool TryResolve(PendingDecision decision, out string reasoning)
        {
            reasoning = "規定の判断基準により自動処理";
            if (decision == null) return false;

            int choice = decision.defaultChoiceIndex;
            if (provider != null)
            {
                try
                {
                    if (provider.TryAdjudicate(new RingiAdjudicationRequest(decision), out int proposed,
                                               out string proposedReasoning))
                    {
                        choice = proposed;
                        if (!string.IsNullOrWhiteSpace(proposedReasoning)) reasoning = proposedReasoning.Trim();
                    }
                }
                catch (Exception)
                {
                    // 外部層の停止でゲームを止めず、Core が検証する既定選択へ戻す。
                }
            }

            // 範囲外を丸めない。共通窓口に拒否させ、外部判断が Core を迂回できないことを保つ。
            return DecisionDeck.Resolve(decision.id, choice);
        }
    }

    /// <summary>
    /// 生成文だけを置く実行時キャッシュ。CampaignSerializer の対象外なので保存されず、
    /// 再開時は PendingDecision.body の決定論テンプレで必ず遊べる。
    /// </summary>
    public static class RingiNarrativeRuntime
    {
        public const int Capacity = 32;
        private static readonly Dictionary<PendingDecision, string> proseByDecision = new Dictionary<PendingDecision, string>();
        private static readonly Queue<PendingDecision> insertionOrder = new Queue<PendingDecision>();
        private static IRingiNarrativeProvider provider;
        public static int CachedCount => proseByDecision.Count;

        public static void Configure(IRingiNarrativeProvider value)
        {
            provider = value;
            Clear();
        }

        public static void Prepare(PendingDecision decision)
        {
            if (!IsRingi(decision)) return;
            string prose = new RingiDrafter(provider).Draft(decision, decision.body);
            if (!string.IsNullOrEmpty(prose) && prose != decision.body)
            {
                if (!proseByDecision.ContainsKey(decision)) insertionOrder.Enqueue(decision);
                proseByDecision[decision] = prose;
                while (proseByDecision.Count > Capacity && insertionOrder.Count > 0)
                    proseByDecision.Remove(insertionOrder.Dequeue());
            }
            else
                proseByDecision.Remove(decision);
        }

        public static string TextFor(PendingDecision decision)
        {
            if (decision == null) return "";
            return IsRingi(decision) && proseByDecision.TryGetValue(decision, out string prose)
                ? prose : decision.body ?? "";
        }

        /// <summary>プレイヤー入力などの一時文面を保存対象へ書かずに表示する。</summary>
        public static void SetProse(PendingDecision decision, string prose)
        {
            if (!IsRingi(decision) || string.IsNullOrWhiteSpace(prose)) return;
            if (!proseByDecision.ContainsKey(decision)) insertionOrder.Enqueue(decision);
            proseByDecision[decision] = prose.Trim();
            while (proseByDecision.Count > Capacity && insertionOrder.Count > 0)
                proseByDecision.Remove(insertionOrder.Dequeue());
        }

        public static bool TryAdjudicate(PendingDecision decision, out string reasoning)
            => new RingiAdjudicator(provider).TryResolve(decision, out reasoning);

        public static void Clear()
        {
            proseByDecision.Clear();
            insertionOrder.Clear();
        }

        private static bool IsRingi(PendingDecision decision)
            => decision != null && (decision.source == DecisionSource.建白結果 || decision.source == DecisionSource.諮問);
    }
}
