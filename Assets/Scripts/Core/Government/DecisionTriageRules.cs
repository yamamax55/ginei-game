using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>決裁トリアージの調整係数（決裁デスク DESK-2/3 #1630/#1631）。締切は game-秒。</summary>
    public readonly struct DecisionTriageParams
    {
        /// <summary>通常決裁の締切（超で最小化）。</summary>
        public readonly float normalDeadline;
        /// <summary>重要決裁の締切（通常より長い）。</summary>
        public readonly float importantDeadline;
        /// <summary>最小化からさらにこの猶予を超えると AI が機械的に自動解決。</summary>
        public readonly float autoResolveGrace;

        public DecisionTriageParams(float normalDeadline, float importantDeadline, float autoResolveGrace)
        {
            this.normalDeadline = normalDeadline;
            this.importantDeadline = importantDeadline;
            this.autoResolveGrace = autoResolveGrace;
        }

        public static DecisionTriageParams Default => new DecisionTriageParams(20f, 45f, 30f);
    }

    /// <summary>
    /// 決裁トリアージの唯一の窓口（決裁デスク DESK-2/3 #1630/#1631）。<b>イベント/決裁で時間を止めない</b>を実装する：
    /// 重要度で締切と挙動を決め（<see cref="PausesClock"/>＝重大のみ時間停止）、締切超で最小化、さらに猶予超で
    /// AI が <see cref="PendingDecision.defaultChoiceIndex"/> を<b>機械的に</b>採択（自動解決＝放置の代償が創発）。
    /// 重大は対象外＝必ず人を待つ（<see cref="ClockShouldStop"/> が <see cref="GameClock"/>/`PauseManager` を止める）。
    /// 賢く最適化しない・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisionTriageRules
    {
        /// <summary>時間を止める重要度か＝重大のみ（本当にやばいやつ）。</summary>
        public static bool PausesClock(DecisionSeverity s) => s == DecisionSeverity.重大;

        /// <summary>重要度別の締切（重大は無期限＝人を待つ／重要＞通常・情報）。</summary>
        public static float DeadlineFor(DecisionSeverity s, DecisionTriageParams p)
        {
            switch (s)
            {
                case DecisionSeverity.重大: return float.PositiveInfinity;
                case DecisionSeverity.重要: return p.importantDeadline;
                default: return p.normalDeadline; // 情報/通常
            }
        }

        public static float DeadlineFor(DecisionSeverity s) => DeadlineFor(s, DecisionTriageParams.Default);

        /// <summary>クロックを止めるべきか＝活性な重大決裁が1件でもある（重大は人が決めるまで進まない）。</summary>
        public static bool ClockShouldStop(DecisionQueue q)
        {
            if (q == null) return false;
            for (int i = 0; i < q.items.Count; i++)
            {
                var d = q.items[i];
                if (d == null) continue;
                if (d.severity == DecisionSeverity.重大 && d.status != DecisionStatus.決裁済)
                    return true;
            }
            return false;
        }

        /// <summary>AI が機械的に自動解決してよいか（重大以外・未解決・締切＋猶予を超過）。</summary>
        public static bool AutoResolvable(PendingDecision d, DecisionTriageParams p)
        {
            if (d == null || d.severity == DecisionSeverity.重大) return false;
            if (d.status == DecisionStatus.決裁済 || d.status == DecisionStatus.自動解決) return false;
            return d.elapsed >= DeadlineFor(d.severity, p) + p.autoResolveGrace;
        }

        public static bool AutoResolvable(PendingDecision d) => AutoResolvable(d, DecisionTriageParams.Default);

        /// <summary>
        /// dt 進める：締切超で<b>最小化</b>、さらに猶予超で AI が<b>既定選択を機械的に採択</b>（自動解決）。
        /// 重大は対象外（人を待つ）。自動解決した決裁を返す＝呼び出し側が effectKey を適用する。
        /// </summary>
        public static List<PendingDecision> Tick(DecisionQueue q, float dt, DecisionTriageParams p)
        {
            var resolved = new List<PendingDecision>();
            if (q == null || dt <= 0f) return resolved;

            for (int i = 0; i < q.items.Count; i++)
            {
                var d = q.items[i];
                if (d == null || d.severity == DecisionSeverity.重大) continue;
                if (d.status == DecisionStatus.決裁済 || d.status == DecisionStatus.自動解決) continue;

                d.elapsed += dt;
                float dl = DeadlineFor(d.severity, p);

                if (d.elapsed >= dl + p.autoResolveGrace)
                {
                    // 規定時間を過ぎても放置 → AI が既定選択を淡々と採択（賢くない＝放置の代償）
                    d.chosenIndex = d.defaultChoiceIndex;
                    d.status = DecisionStatus.自動解決;
                    resolved.Add(d);
                }
                else if (d.elapsed >= dl &&
                         (d.status == DecisionStatus.新着 || d.status == DecisionStatus.提示中))
                {
                    // 規定時間に選択がない → 右下へ最小化
                    d.status = DecisionStatus.最小化;
                }
            }

            return resolved;
        }

        public static List<PendingDecision> Tick(DecisionQueue q, float dt) => Tick(q, dt, DecisionTriageParams.Default);
    }
}
