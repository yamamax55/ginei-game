namespace Ginei
{
    public partial class GalaxyView
    {
        /// <summary>試験用：イベント決裁の本番経路へ、復元済みのエンジンと文脈を差し込む。</summary>
        public void BindPolicyEventForQa(EventEngine engine, EventContext context)
        {
            policyEngine = engine;
            policyCtx = context;
            DecisionDeck.Resolved -= OnPolicyDecisionResolved;
            DecisionDeck.Resolved += OnPolicyDecisionResolved;
        }

        /// <summary>試験用：本番と同じ決裁カード生成経路を呼ぶ。</summary>
        public void EnqueuePolicyEventForQa(GameEventDef def) => ShowPolicyEvent(def);

        /// <summary>試験用：無効なGameObjectではOnDestroyが呼ばれない場合があるため購読を明示解除する。</summary>
        public void UnbindPolicyEventForQa() => DecisionDeck.Resolved -= OnPolicyDecisionResolved;
    }
}
