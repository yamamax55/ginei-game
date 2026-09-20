using System;

namespace Ginei
{
    /// <summary>EventEngineのイベントを決裁デスクへ写す共通規約（DESK-6 #1634）。</summary>
    public static class EventDecisionRules
    {
        public const string EffectPrefix = "event:";

        public static string EffectKey(string eventId)
            => EffectPrefix + (eventId ?? "");

        public static bool TryDecode(string effectKey, out string eventId)
        {
            eventId = "";
            if (string.IsNullOrEmpty(effectKey)
                || !effectKey.StartsWith(EffectPrefix, StringComparison.Ordinal)) return false;
            eventId = effectKey.Substring(EffectPrefix.Length);
            return !string.IsNullOrEmpty(eventId);
        }

        public static PendingDecision Create(GameEventDef def, int decisionId,
            DecisionSeverity severity = DecisionSeverity.通常, int defaultChoiceIndex = 0)
        {
            if (def == null) return null;
            var d = new PendingDecision(decisionId, def.title, severity, DecisionSource.イベント,
                EffectKey(def.id), defaultChoiceIndex, def.body);
            if (def.choices != null)
                for (int i = 0; i < def.choices.Count; i++)
                    d.choices.Add(def.choices[i] != null ? def.choices[i].label : "");
            if (d.choices.Count == 0) d.choices.Add("確認");
            if (d.defaultChoiceIndex < 0 || d.defaultChoiceIndex >= d.choices.Count)
                d.defaultChoiceIndex = d.choices.Count - 1;
            return d;
        }
    }
}
