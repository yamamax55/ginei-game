using UnityEngine;

namespace Ginei
{
    /// <summary>連立政権の調整係数。</summary>
    public readonly struct CoalitionParams
    {
        /// <summary>過半数ライン（これを超えたら単独政権可・既定0.5）。</summary>
        public readonly float majorityThreshold;
        /// <summary>追加パートナー1党あたりの政策希釈の基礎係数。</summary>
        public readonly float dilutionPerPartner;
        /// <summary>思想幅ゼロでも党数だけで生じる希釈の下駄（合意コストの最低分）。</summary>
        public readonly float dilutionBase;
        /// <summary>拒否権が消えるとみなす余裕マージンの基準（連立がこの余裕を持てばキングメーカー不在）。</summary>
        public readonly float comfortMargin;
        /// <summary>ピボタル（抜けたら過半数割れ）な党が必ず持つ拒否権の下限。</summary>
        public readonly float pivotFloor;
        /// <summary>思想幅が結束を軋ませる強さ。</summary>
        public readonly float spreadStrain;
        /// <summary>外圧が結束を軋ませる強さ。</summary>
        public readonly float pressureStrain;
        /// <summary>結束が均衡値へ収束する速度（per dt）。</summary>
        public readonly float cohesionRate;
        /// <summary>キングメーカーの離脱誘惑が不安定さを増幅する倍率。</summary>
        public readonly float defectionBoost;

        public CoalitionParams(float majorityThreshold, float dilutionPerPartner, float dilutionBase,
                               float comfortMargin, float pivotFloor,
                               float spreadStrain, float pressureStrain, float cohesionRate,
                               float defectionBoost)
        {
            this.majorityThreshold = Mathf.Clamp01(majorityThreshold);
            this.dilutionPerPartner = Mathf.Max(0f, dilutionPerPartner);
            this.dilutionBase = Mathf.Max(0f, dilutionBase);
            this.comfortMargin = Mathf.Max(0.0001f, comfortMargin);
            this.pivotFloor = Mathf.Clamp01(pivotFloor);
            this.spreadStrain = Mathf.Max(0f, spreadStrain);
            this.pressureStrain = Mathf.Max(0f, pressureStrain);
            this.cohesionRate = Mathf.Max(0f, cohesionRate);
            this.defectionBoost = Mathf.Max(0f, defectionBoost);
        }

        /// <summary>既定＝過半数0.5・希釈0.2/党×(0.5+思想幅)・余裕基準0.25・拒否権下限0.25・思想軋み0.6/外圧0.4・収束0.2・誘惑倍率1.0。</summary>
        public static CoalitionParams Default
            => new CoalitionParams(0.5f, 0.2f, 0.5f, 0.25f, 0.25f, 0.6f, 0.4f, 0.2f, 1f);
    }

    /// <summary>
    /// 連立政権の純ロジック＝単独過半数なき議会で多党が組む政権の構造力学。
    /// <see cref="PartyRules"/> との分担：あちらは党勢から**誰が統べるか**（与党 RulingParty・首班 Premier・
    /// 政治任用）を決める。本クラスは単独過半数が無いとき**その政権がどう持ちこたえるか**＝
    /// 政策の薄まり・小党の拒否権・結束の摩耗・崩壊・ポスト配分を plain な数値引数で解く
    /// （`Party` 型は参照しない read-only 設計＝呼び出し側が議席シェアを渡す）。
    /// 核心は「**足し算の議席と掛け算の拒否権**」：議席は足し算で過半数を作るが、
    /// 過半数ぎりぎりの連立では各党の離脱カード1枚が政権を割る＝拒否権はシェアに比例しない。
    /// 政策は参加党数×思想幅で最小公倍数へ薄まり、閣僚ポストはガムソンの法則＝貢献議席に比例する。
    /// 乱数なしの決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class CoalitionRules
    {
        /// <summary>
        /// 連立が必要か。第一党のシェア（0..1）が過半数ラインを超えなければ true
        /// （ちょうど半分は過半数ではない＝連立が要る）。
        /// </summary>
        public static bool NeedsCoalition(float largestShare, CoalitionParams p)
        {
            return Mathf.Clamp01(largestShare) <= p.majorityThreshold;
        }

        public static bool NeedsCoalition(float largestShare)
            => NeedsCoalition(largestShare, CoalitionParams.Default);

        /// <summary>
        /// 政策の希釈（0..1）＝最小公倍数化。追加パートナー数（partnerCount−1）×係数×（下駄＋思想幅）。
        /// 党が増えるほど・思想幅が広いほど、全員が呑める政策は薄まる。単独政権（1党以下）は薄まらない。
        /// </summary>
        public static float PolicyDilution(int partnerCount, float ideologySpread, CoalitionParams p)
        {
            int extra = Mathf.Max(0, partnerCount - 1);
            float spread = Mathf.Clamp01(ideologySpread);
            return Mathf.Clamp01(extra * p.dilutionPerPartner * (p.dilutionBase + spread));
        }

        public static float PolicyDilution(int partnerCount, float ideologySpread)
            => PolicyDilution(partnerCount, ideologySpread, CoalitionParams.Default);

        /// <summary>
        /// 小党の拒否権（0..1）＝キングメーカー力。力の源泉はシェアの大きさではなく
        /// 「抜けたら過半数割れ」＝ partnerShare が連立の余裕 coalitionMargin（連立合計−過半数ライン）を
        /// 超えるかどうか。超えなければ抜けても政権は保つ＝拒否権ゼロ。超えれば（どんな小党でも）
        /// 余裕が薄いほど強い拒否権を持ち、ピボタルである限り下限 pivotFloor を割らない。
        /// </summary>
        public static float KingmakerPower(float partnerShare, float coalitionMargin, CoalitionParams p)
        {
            float share = Mathf.Clamp01(partnerShare);
            float margin = Mathf.Max(0f, coalitionMargin);
            if (share <= margin) return 0f; // 抜けても過半数維持＝離脱カードに価値なし
            float power = 1f - margin / p.comfortMargin;
            return Mathf.Clamp01(Mathf.Max(p.pivotFloor, power));
        }

        public static float KingmakerPower(float partnerShare, float coalitionMargin)
            => KingmakerPower(partnerShare, coalitionMargin, CoalitionParams.Default);

        /// <summary>
        /// 連立の結束（0..1）の1tick後。均衡値＝1−思想幅×軋み−外圧×軋み へ cohesionRate×dt で収束する。
        /// 思想幅が広いほど・外圧が強いほど結束は低い水準へ落ち着き、要因が消えれば時間とともに回復する。
        /// </summary>
        public static float StabilityTick(float stability, float ideologySpread, float externalPressure, float dt, CoalitionParams p)
        {
            float target = Mathf.Clamp01(1f - Mathf.Clamp01(ideologySpread) * p.spreadStrain
                                            - Mathf.Clamp01(externalPressure) * p.pressureStrain);
            return Mathf.MoveTowards(Mathf.Clamp01(stability), target, p.cohesionRate * Mathf.Max(0f, dt));
        }

        public static float StabilityTick(float stability, float ideologySpread, float externalPressure, float dt)
            => StabilityTick(stability, ideologySpread, externalPressure, dt, CoalitionParams.Default);

        /// <summary>
        /// 崩壊リスク（0..1）＝（1−結束）×（1＋離脱誘惑×倍率）。
        /// 誘惑は不安定さに**掛かる**（足されない）＝結束が完全なら誘惑があっても倒れず、
        /// 緩んだ連立ではキングメーカーの誘惑が同じ亀裂を倍に広げる。
        /// </summary>
        public static float CollapseRisk(float stability, float kingmakerDefectionTemptation, CoalitionParams p)
        {
            float instability = 1f - Mathf.Clamp01(stability);
            return Mathf.Clamp01(instability * (1f + Mathf.Clamp01(kingmakerDefectionTemptation) * p.defectionBoost));
        }

        public static float CollapseRisk(float stability, float kingmakerDefectionTemptation)
            => CollapseRisk(stability, kingmakerDefectionTemptation, CoalitionParams.Default);

        /// <summary>
        /// 閣僚ポストの配分比（0..1）＝ガムソンの法則：連立への貢献議席に比例して分け合う。
        /// partnerShare／coalitionTotal。連立合計が0以下なら0。
        /// </summary>
        public static float PortfolioAllocation(float partnerShare, float coalitionTotal)
        {
            float total = Mathf.Clamp01(coalitionTotal);
            if (total <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Clamp01(partnerShare) / total);
        }
    }
}
