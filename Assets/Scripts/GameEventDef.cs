using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// イベントが参照するコンテキスト（#116）。条件評価・効果適用に渡す（星系/勢力/戦況など）。
    /// 横断基盤なので具体は持たず、汎用の参照だけを運ぶ（内政=Province、戦略=CampaignState 等を <see cref="payload"/> へ）。
    /// </summary>
    public class EventContext
    {
        public Faction faction;
        public int systemId = -1;
        public object payload; // 任意（Province/CampaignState/ResourceStockpile 等を渡す）

        public EventContext() { }
        public EventContext(Faction faction, int systemId = -1, object payload = null)
        {
            this.faction = faction;
            this.systemId = systemId;
            this.payload = payload;
        }
    }

    /// <summary>イベントの選択肢（#116）。ラベルと、選んだときに適用される効果（デリゲート）。</summary>
    public class EventChoice
    {
        public string label;
        public Action<EventContext> effect;

        public EventChoice(string label, Action<EventContext> effect = null)
        {
            this.label = label ?? "";
            this.effect = effect;
        }

        public void Apply(EventContext ctx) => effect?.Invoke(ctx);
    }

    /// <summary>
    /// 1イベントの定義（#116・データ駆動）。発火条件／本文（タイトル・説明）／選択肢（1〜数個）／重み／
    /// 一回限り or 繰り返し／クールダウン。効果は選択肢のデリゲートで適用（拡張可能・<b>1イベントは小さく</b>）。
    /// 評価・選択・適用は <see cref="EventRules"/>、駆動・キューは <see cref="EventEngine"/>。ScriptableObject 化は後段の作者UI。
    /// </summary>
    public class GameEventDef
    {
        public string id;
        public string title;
        public string body;
        public List<EventChoice> choices = new List<EventChoice>();

        [Tooltip("発火の重み（同時に複数が条件を満たしたときの抽選比率）")]
        public float weight = 1f;
        [Tooltip("繰り返し発火できるか（false＝一回限り）")]
        public bool repeatable = false;
        [Tooltip("繰り返し時の最小間隔（秒・戦略/会戦時間）")]
        public float cooldown = 0f;

        /// <summary>発火条件（null＝常に真）。コンテキストを見て真なら発火候補。</summary>
        public Func<EventContext, bool> condition;

        public GameEventDef() { }
        public GameEventDef(string id, string title, string body)
        {
            this.id = id;
            this.title = title ?? "";
            this.body = body ?? "";
        }

        /// <summary>選択肢を追加する（流れるように定義できる）。</summary>
        public GameEventDef AddChoice(string label, Action<EventContext> effect = null)
        {
            choices.Add(new EventChoice(label, effect));
            return this;
        }

        /// <summary>条件を設定する（流れるように定義できる）。</summary>
        public GameEventDef When(Func<EventContext, bool> cond) { condition = cond; return this; }

        /// <summary>通知イベントか（選択肢が0〜1個＝確認のみ）。</summary>
        public bool IsNotification => choices.Count <= 1;
    }

    /// <summary>イベントごとの実行時状態（#116）。発火回数と最終発火時刻（一回限り・クールダウン判定用）。</summary>
    public class EventRuntimeState
    {
        public int fireCount;
        public float lastFireTime = float.NegativeInfinity;
    }
}
