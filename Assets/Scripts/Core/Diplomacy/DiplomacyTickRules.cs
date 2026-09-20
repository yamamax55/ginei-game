namespace Ginei
{
    /// <summary>1年の外交で起きた出来事（DIPLO-5・#2119・通知用）。</summary>
    public enum DiplomacyEvent { なし, 宣戦布告, 講和, 同盟締結 }

    /// <summary>
    /// 外交の暦境界オーケストレータ（DIPLO-5・#2119・純ロジック）。1ペアの外交を1年ぶん回す薄い窓口。
    /// 各段は DIP-1（関係/状態遷移）・DIPLO-2（AI判断）・DIPLO-3（戦争追跡）へ委譲する。test-first。
    /// </summary>
    public static class DiplomacyTickRules
    {
        /// <summary>
        /// 判断を伴わず、関係値の自然増減だけを1回進める。プレイヤー絡みのペアもAIに操作させず
        /// 主義・交易・国境などの関係変化を続けるための共通入口。
        /// </summary>
        public static float DriftPair(DiplomacyState state, string a, string b,
            DiplomacyRules.OpinionFactors factors, float dt, DiplomacyRules.DiplomacyParams dp)
        {
            if (state == null) return 0f;
            float target = DiplomacyRules.TargetOpinion(factors, dp);
            DiplomacyRules.DriftOpinion(state, a, b, target, dt, dp);
            return state.Opinion(a, b);
        }

        /// <summary>
        /// ①目標関係へドリフト→②交戦中は戦争tickし講和受諾なら講和→③非交戦は低関係×国力優位なら宣戦／高関係なら同盟。
        /// 発生イベントを返す（通知用）。同盟/属国とは開戦しない。
        /// </summary>
        public static DiplomacyEvent TickPair(DiplomacyState state, string a, string b,
            DiplomacyRules.OpinionFactors factors, float ownStrength, float theirStrength, int year,
            DiplomacyRules.DiplomacyParams dp, DiplomacyAiRules.DiploAiParams ai, WarGoalRules.WarGoalParams wp)
        {
            if (state == null) return DiplomacyEvent.なし;

            // ① 関係を目標へドリフト
            DriftPair(state, a, b, factors, 1f, dp);

            var status = state.Status(a, b);
            float opinion = state.Opinion(a, b);

            // ② 交戦中＝戦争を進め、講和受諾度が高ければ講和
            if (status == DiplomacyState.DiplomaticStatus.交戦)
            {
                var w = WarLedger.GetOrCreate(a, b);
                WarStateRules.Tick(w, 1);
                float pa = WarStateRules.PeaceAcceptanceFor(w, true, wp); // A 視点（簡易）
                if (DiplomacyAiRules.ShouldMakePeace(pa, ai) && DiplomacyRules.MakePeace(state, a, b))
                {
                    WarLedger.Remove(a, b);
                    return DiplomacyEvent.講和;
                }
                return DiplomacyEvent.なし;
            }

            // ③ 非交戦＝開戦 or 同盟
            bool protectedByPact = status == DiplomacyState.DiplomaticStatus.同盟 || status == DiplomacyState.DiplomaticStatus.属国;
            if (!protectedByPact && DiplomacyAiRules.ShouldDeclareWar(opinion, ownStrength, theirStrength, ai))
            {
                if (DiplomacyRules.DeclareWar(state, a, b, dp))
                {
                    var w = WarLedger.GetOrCreate(a, b);
                    w.turnsAtWar = 0;
                    return DiplomacyEvent.宣戦布告;
                }
            }
            else if (status == DiplomacyState.DiplomaticStatus.平時 && DiplomacyAiRules.ShouldProposeAlliance(opinion, ai))
            {
                if (DiplomacyRules.SignTreaty(state, a, b, DiplomacyState.DiplomaticStatus.同盟))
                    return DiplomacyEvent.同盟締結;
            }
            return DiplomacyEvent.なし;
        }
    }
}
