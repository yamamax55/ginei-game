using UnityEngine;

namespace Ginei
{
    /// <summary>結末の類型（#1061）。覇道/王道の統一・共和革命・割拠均衡・滅亡・隠遁。</summary>
    public enum EndingType
    {
        覇道統一,
        王道統一,
        共和革命,
        割拠均衡,
        滅亡,
        隠遁,
    }

    /// <summary>
    /// 結末分岐の1条件（純データ・#1061）。ある結末タイプへ到達するための必要評判・必要同盟数・必須イベント
    /// （経験した出来事のIDすべて）・優先度（大きいほど特別＝先に選ばれる）。配列は手書きループで評価する。
    /// </summary>
    public class EndingCondition
    {
        /// <summary>この条件が指し示す結末。</summary>
        public readonly EndingType ending;
        /// <summary>到達に要する評判の下限（0..1）。</summary>
        public readonly float requiredReputation;
        /// <summary>到達に要する同盟国数の下限（負はクランプ）。</summary>
        public readonly int requiredAllianceCount;
        /// <summary>到達に必須の経験イベントID（すべて経験している必要がある・null/空＝不問）。</summary>
        public readonly string[] requiredEvents;
        /// <summary>優先度（大きいほど特別な結末＝条件を満たす中で優先して選ばれる）。</summary>
        public readonly int priority;

        public EndingCondition(EndingType ending, float requiredReputation, int requiredAllianceCount,
            string[] requiredEvents, int priority)
        {
            this.ending = ending;
            this.requiredReputation = Mathf.Clamp01(requiredReputation);
            this.requiredAllianceCount = Mathf.Max(0, requiredAllianceCount);
            this.requiredEvents = requiredEvents ?? new string[0];
            this.priority = priority;
        }
    }

    /// <summary>結末分岐の調整係数（#1061）。</summary>
    public readonly struct EndingBranchParams
    {
        /// <summary>近さ（EndingProximity）で評判ギャップに掛ける重み。</summary>
        public readonly float reputationWeight;
        /// <summary>近さで同盟ギャップ（不足1あたり）に掛ける重み。</summary>
        public readonly float allianceWeight;
        /// <summary>近さで必須イベント未経験1件あたりに掛ける重み。</summary>
        public readonly float eventWeight;
        /// <summary>王道/覇道の分岐で王道へ振れる道の閾値（これ以上で王道側）。</summary>
        public readonly float wangDaoThreshold;

        public EndingBranchParams(float reputationWeight, float allianceWeight, float eventWeight, float wangDaoThreshold)
        {
            this.reputationWeight = Mathf.Max(0f, reputationWeight);
            this.allianceWeight = Mathf.Max(0f, allianceWeight);
            this.eventWeight = Mathf.Max(0f, eventWeight);
            this.wangDaoThreshold = Mathf.Clamp(wangDaoThreshold, -1f, 1f);
        }

        /// <summary>既定＝評判重み1.0・同盟重み0.5・イベント重み0.3・王道閾値0.0。</summary>
        public static EndingBranchParams Default => new EndingBranchParams(1f, 0.5f, 0.3f, 0f);
    }

    /// <summary>
    /// エンディング分岐の純ロジック（Almagest・#1061）。ゲームの結末を、プレイ中に積み上げた
    /// <b>評判（0..1）・同盟国数・経験した出来事</b>の条件で分岐させる＝マルチエンディングの条件評価。
    /// 「結末はプレイの積み重ねで分岐する＝評判・同盟・経験が運命を決める」を式に出す＝条件を満たす結末の中から
    /// <b>優先度最高（＝最も特別な結末）</b>を選び（<see cref="SelectEnding"/>）、足りない要素を近さ
    /// （<see cref="EndingProximity"/>）でプレイヤーへヒントとして返す。全条件達成の隠し結末は真エンディング
    /// （<see cref="IsTrueEnding"/>）。<b>DisclosureRules</b>（開示連鎖）は秘史の連鎖開示、<b>WangDaoRules</b>
    /// （王道覇道）は統治の道、<b>ReputationRules</b>（評判）は名声の増減＝こちらはそれらの積み上げを入力に取り、
    /// <b>最終分岐の条件スコアリングだけ</b>を担う（同じ統一でも王道/覇道は <see cref="PathDivergence"/> が分ける）。
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class EndingBranchRules
    {
        /// <summary>経験済みイベントの中に id があるか（配列は手書きループ）。</summary>
        private static bool HasEvent(string[] events, string id)
        {
            if (events == null || string.IsNullOrEmpty(id)) return false;
            for (int i = 0; i < events.Length; i++)
                if (events[i] == id) return true;
            return false;
        }

        /// <summary>
        /// 結末条件を満たすか（評判・同盟数・必須イベントの<b>全充足</b>）。評判は下限以上、同盟数は下限以上、
        /// 必須イベントはすべて経験済み＝積み上げが揃って初めてその結末への扉が開く。
        /// </summary>
        public static bool MeetsCondition(EndingCondition condition, float reputation, int allianceCount, string[] experiencedEvents)
        {
            if (condition == null) return false;
            float rep = Mathf.Clamp01(reputation);
            int allies = Mathf.Max(0, allianceCount);
            if (rep < condition.requiredReputation) return false;
            if (allies < condition.requiredAllianceCount) return false;
            for (int i = 0; i < condition.requiredEvents.Length; i++)
                if (!HasEvent(experiencedEvents, condition.requiredEvents[i])) return false;
            return true;
        }

        /// <summary>
        /// 到達可能な結末一覧（条件を満たす条件をそのまま並べる）。満たす条件が無ければ空配列。
        /// </summary>
        public static EndingCondition[] EligibleEndings(EndingCondition[] conditions, float reputation, int allianceCount, string[] experiencedEvents)
        {
            if (conditions == null || conditions.Length == 0) return new EndingCondition[0];
            int count = 0;
            for (int i = 0; i < conditions.Length; i++)
                if (MeetsCondition(conditions[i], reputation, allianceCount, experiencedEvents)) count++;
            var result = new EndingCondition[count];
            int k = 0;
            for (int i = 0; i < conditions.Length; i++)
                if (MeetsCondition(conditions[i], reputation, allianceCount, experiencedEvents))
                    result[k++] = conditions[i];
            return result;
        }

        /// <summary>
        /// 最終的に選ばれる結末（条件を満たす中で<b>優先度最高＝最も特別な結末を優先</b>）。
        /// 同優先度は配列の先頭（先に定義した方）を採る＝決定論。満たす条件が無ければ滅亡（既定の凡庸な結末）。
        /// </summary>
        public static EndingType SelectEnding(EndingCondition[] conditions, float reputation, int allianceCount, string[] experiencedEvents)
        {
            EndingCondition best = null;
            if (conditions != null)
            {
                for (int i = 0; i < conditions.Length; i++)
                {
                    var c = conditions[i];
                    if (!MeetsCondition(c, reputation, allianceCount, experiencedEvents)) continue;
                    if (best == null || c.priority > best.priority) best = c;
                }
            }
            return best != null ? best.ending : EndingType.滅亡;
        }

        /// <summary>
        /// 結末への近さ（0..1＝1で到達・あと何が足りないか）。不足分を評判ギャップ・同盟不足・未経験イベント数で
        /// 加重したペナルティの裏返し＝<b>プレイヤーへのヒント</b>。すでに満たしていれば1。
        /// </summary>
        public static float EndingProximity(EndingCondition condition, float reputation, int allianceCount, string[] experiencedEvents, EndingBranchParams p)
        {
            if (condition == null) return 0f;
            float rep = Mathf.Clamp01(reputation);
            int allies = Mathf.Max(0, allianceCount);

            float repGap = Mathf.Max(0f, condition.requiredReputation - rep);                 // 0..1
            int allyGap = Mathf.Max(0, condition.requiredAllianceCount - allies);
            int missingEvents = 0;
            for (int i = 0; i < condition.requiredEvents.Length; i++)
                if (!HasEvent(experiencedEvents, condition.requiredEvents[i])) missingEvents++;

            float penalty = repGap * p.reputationWeight
                          + allyGap * p.allianceWeight
                          + missingEvents * p.eventWeight;
            return Mathf.Clamp01(1f - penalty);
        }

        public static float EndingProximity(EndingCondition condition, float reputation, int allianceCount, string[] experiencedEvents)
            => EndingProximity(condition, reputation, allianceCount, experiencedEvents, EndingBranchParams.Default);

        /// <summary>
        /// 王道/覇道の分岐（統治スタイルが結末を分ける）。同じ統一でも道（wangDaoValue −1..1、<b>WangDaoRules</b>の出力）が
        /// 閾値以上なら王道統一、未満なら覇道統一＝徳で治めたか力で押さえたかが最後の一字を変える。reputation は将来の
        /// 微分岐用に受けるが現状は道のみで決める（評判は MeetsCondition 側で既に効いている）。
        /// </summary>
        public static EndingType PathDivergence(float reputation, float wangDaoValue, EndingBranchParams p)
        {
            float dao = Mathf.Clamp(wangDaoValue, -1f, 1f);
            return dao >= p.wangDaoThreshold ? EndingType.王道統一 : EndingType.覇道統一;
        }

        public static EndingType PathDivergence(float reputation, float wangDaoValue)
            => PathDivergence(reputation, wangDaoValue, EndingBranchParams.Default);

        /// <summary>
        /// 真エンディングの判定（全条件を満たした隠し結末＝最高難度の到達）。allConditionsMet＝定義された
        /// すべての結末条件を満たしたか＝積み上げを取りこぼさず完走したときだけ開く。覇道/王道の統一に限り真結末を許す
        /// （滅亡・隠遁・割拠は途中放棄/敗北の結末ゆえ真結末ではない）。
        /// </summary>
        public static bool IsTrueEnding(EndingType ending, bool allConditionsMet)
        {
            if (!allConditionsMet) return false;
            return ending == EndingType.王道統一 || ending == EndingType.覇道統一;
        }
    }
}
