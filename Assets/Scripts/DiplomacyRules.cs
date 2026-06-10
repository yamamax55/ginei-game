using UnityEngine;
using DS = Ginei.DiplomacyState;

namespace Ginei
{
    /// <summary>
    /// 外交の純ロジック（外交EPIC #189・DIP-1 関係と態度／DIP-3 戦争と講和の入口・唯一の窓口）。
    /// <b>関係値(opinion)の修正子合算とドリフト</b>・<b>外交状態の遷移</b>（締結/破棄/宣戦/講和）・
    /// <b>外交状態→敵対(<see cref="FactionRelations.IsHostile(FactionData, Faction, FactionData, Faction)"/>)への写像</b>を集約する。
    /// 状態は <see cref="DiplomacyState"/>。並行する敵対システムを作らない（IsHostile を駆動するだけ）。
    /// 細かい数値管理は持たず（タイクン化回避）、高位の決断（同盟する/裏切る/講和する）と帰結（信義毀損・険悪化）を扱う。test-first。
    /// </summary>
    public static class DiplomacyRules
    {
        public const float OpinionMin = -100f;
        public const float OpinionMax = 100f;

        /// <summary>外交の調整値（マジックナンバー禁止＝集約）。</summary>
        public readonly struct DiplomacyParams
        {
            public readonly float allyOpinionThreshold;   // 同盟を提案できる関係値の下限
            public readonly float warOpinionThreshold;    // この関係値を下回ると宣戦が起こりやすい（AI判断の素）
            public readonly float declareWarOpinionHit;   // 宣戦布告による関係毀損
            public readonly float breakTreatyOpinionHit;  // 条約破棄による信義毀損
            public readonly float opinionDriftRate;       // 目標関係値への 1ターンあたり接近量
            public readonly float ideologyWeight;         // 思想親和の寄与（×opinion レンジ）
            public readonly float tradeWeight;            // 交易の寄与
            public readonly float borderPenalty;          // 国境接触の負の寄与
            public readonly float betrayalPenalty;        // 過去の裏切りの負の寄与
            public readonly float marriageBonus;          // 王室婚姻の正の寄与

            public DiplomacyParams(float allyOpinionThreshold, float warOpinionThreshold,
                float declareWarOpinionHit, float breakTreatyOpinionHit, float opinionDriftRate,
                float ideologyWeight, float tradeWeight, float borderPenalty, float betrayalPenalty, float marriageBonus)
            {
                this.allyOpinionThreshold = allyOpinionThreshold;
                this.warOpinionThreshold = warOpinionThreshold;
                this.declareWarOpinionHit = Mathf.Max(0f, declareWarOpinionHit);
                this.breakTreatyOpinionHit = Mathf.Max(0f, breakTreatyOpinionHit);
                this.opinionDriftRate = Mathf.Max(0f, opinionDriftRate);
                this.ideologyWeight = ideologyWeight;
                this.tradeWeight = tradeWeight;
                this.borderPenalty = borderPenalty;
                this.betrayalPenalty = betrayalPenalty;
                this.marriageBonus = marriageBonus;
            }

            /// <summary>既定＝同盟+50/開戦-50・宣戦-40/破棄-30・ドリフト5／思想40・交易20・国境-15・裏切り-40・婚姻+20。</summary>
            public static DiplomacyParams Default => new DiplomacyParams(
                50f, -50f, 40f, 30f, 5f,
                40f, 20f, 15f, 40f, 20f);
        }

        /// <summary>opinion を [-100,100] に丸める。</summary>
        public static float Clamp(float opinion) => Mathf.Clamp(opinion, OpinionMin, OpinionMax);

        // ===== 外交状態 → 敵対への写像（FactionRelations を駆動） =====

        /// <summary>外交状態から敵対を決める。null＝平時＝従来の enum/FactionData 判定にフォールバック（後方互換の核）。</summary>
        public static bool? HostileOverride(DS.DiplomaticStatus status)
        {
            switch (status)
            {
                case DS.DiplomaticStatus.交戦: return true;
                case DS.DiplomaticStatus.同盟:
                case DS.DiplomaticStatus.不可侵:
                case DS.DiplomaticStatus.属国: return false;
                default: return null; // 平時
            }
        }

        /// <summary>勢力ペアの敵対を外交状態から判定。レコード無し/平時は null（フォールバック）。</summary>
        public static bool? IsHostile(DiplomacyState state, string a, string b)
        {
            if (state == null) return null;
            return HostileOverride(state.Status(a, b));
        }

        // ===== 関係値（opinion）の修正子と更新 =====

        /// <summary>opinion 修正子の素（実効値パターン＝各軸の生入力。重みは Params）。</summary>
        public readonly struct OpinionFactors
        {
            public readonly float ideologyAffinity;  // 思想親和 -1..1（#117/#843）
            public readonly float tradeVolume;       // 交易量 0..1（#92/#179）
            public readonly bool sharedBorder;       // 国境接触（緊張）
            public readonly float pastBetrayal;      // 過去の裏切り 0..1（#817 寝返り/歴史）
            public readonly bool marriageTie;        // 王室婚姻（#188）

            public OpinionFactors(float ideologyAffinity, float tradeVolume, bool sharedBorder, float pastBetrayal, bool marriageTie)
            {
                this.ideologyAffinity = Mathf.Clamp(ideologyAffinity, -1f, 1f);
                this.tradeVolume = Mathf.Clamp01(tradeVolume);
                this.sharedBorder = sharedBorder;
                this.pastBetrayal = Mathf.Clamp01(pastBetrayal);
                this.marriageTie = marriageTie;
            }
        }

        /// <summary>修正子から目標 opinion を合算（思想/交易/国境/裏切り/婚姻）。[-100,100] に丸める。</summary>
        public static float TargetOpinion(OpinionFactors f, DiplomacyParams p)
        {
            float v = 0f;
            v += f.ideologyAffinity * p.ideologyWeight;
            v += f.tradeVolume * p.tradeWeight;
            if (f.sharedBorder) v -= p.borderPenalty;
            v -= f.pastBetrayal * p.betrayalPenalty;
            if (f.marriageTie) v += p.marriageBonus;
            return Clamp(v);
        }

        /// <summary>opinion を delta ぶん増減（信義毀損・贈与など）。レコードを生成して反映。</summary>
        public static void AdjustOpinion(DiplomacyState state, string a, string b, float delta)
        {
            var e = state?.GetEntry(a, b, create: true);
            if (e == null) return;
            e.opinion = Clamp(e.opinion + delta);
        }

        /// <summary>現在 opinion を目標へ 1ターンぶん（driftRate）近づける。緩やかな関係変化。</summary>
        public static void DriftOpinion(DiplomacyState state, string a, string b, float target, float dt, DiplomacyParams p)
        {
            var e = state?.GetEntry(a, b, create: true);
            if (e == null || dt <= 0f) return;
            e.opinion = Clamp(Mathf.MoveTowards(e.opinion, Clamp(target), p.opinionDriftRate * dt));
        }

        // ===== 外交状態の遷移 =====

        /// <summary>条約締結（同盟/不可侵/属国）。交戦中からは締結不可（先に講和が要る）＝false。</summary>
        public static bool SignTreaty(DiplomacyState state, string a, string b, DS.DiplomaticStatus treaty)
        {
            var e = state?.GetEntry(a, b, create: true);
            if (e == null) return false;
            if (e.status == DS.DiplomaticStatus.交戦) return false;
            if (treaty != DS.DiplomaticStatus.同盟 && treaty != DS.DiplomaticStatus.不可侵 && treaty != DS.DiplomaticStatus.属国)
                return false;
            e.status = treaty;
            return true;
        }

        /// <summary>条約破棄＝平時へ。信義毀損で opinion 低下（破棄者の評判は呼び出し側で別途扱う）。交戦中は対象外＝false。</summary>
        public static bool BreakTreaty(DiplomacyState state, string a, string b, DiplomacyParams p)
        {
            var e = state?.GetEntry(a, b, create: true);
            if (e == null) return false;
            if (e.status == DS.DiplomaticStatus.交戦 || e.status == DS.DiplomaticStatus.平時) return false;
            e.status = DS.DiplomaticStatus.平時;
            e.opinion = Clamp(e.opinion - p.breakTreatyOpinionHit);
            return true;
        }

        /// <summary>宣戦布告＝交戦へ。関係毀損（declareWarOpinionHit）。すでに交戦中なら false。</summary>
        public static bool DeclareWar(DiplomacyState state, string a, string b, DiplomacyParams p)
        {
            var e = state?.GetEntry(a, b, create: true);
            if (e == null) return false;
            if (e.status == DS.DiplomaticStatus.交戦) return false;
            e.status = DS.DiplomaticStatus.交戦;
            e.opinion = Clamp(e.opinion - p.declareWarOpinionHit);
            return true;
        }

        /// <summary>講和＝交戦から平時へ。交戦中でなければ false。</summary>
        public static bool MakePeace(DiplomacyState state, string a, string b)
        {
            var e = state?.GetEntry(a, b);
            if (e == null || e.status != DS.DiplomaticStatus.交戦) return false;
            e.status = DS.DiplomaticStatus.平時;
            return true;
        }

        // ===== 判断の素（提案/AI が読む） =====

        /// <summary>同盟を提案できるか＝交戦中でなく opinion が閾値以上。</summary>
        public static bool CanProposeAlliance(DiplomacyState state, string a, string b, DiplomacyParams p)
        {
            if (state == null) return false;
            return state.Status(a, b) != DS.DiplomaticStatus.交戦
                && state.Opinion(a, b) >= p.allyOpinionThreshold;
        }
    }
}
