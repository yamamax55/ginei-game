namespace Ginei
{
    /// <summary>
    /// 艦隊の陣形指定を<b>誰が出したか</b>。優先順位はこの並びで決まる（下ほど強い）。
    /// </summary>
    public enum FormationOrderSource
    {
        /// <summary>指定なし（＝保持していない）。</summary>
        なし,
        /// <summary>艦隊AIの自己判断（<c>FleetAI.UpdateFormationDoctrine</c>）。</summary>
        艦隊AI,
        /// <summary>軍団長の発令（<c>BattlefieldCommandManager</c>）。</summary>
        軍団AI,
        /// <summary>承諾された支援要請（系統外からの依頼を引き受けたもの）。</summary>
        支援要請,
        /// <summary>プレイヤーの直接命令。</summary>
        直接命令,
    }

    /// <summary>陣形指定を受け付けたかどうかと、断った理由。</summary>
    public enum FormationOrderResult
    {
        /// <summary>受理した（指定を更新した）。</summary>
        受理,
        /// <summary>手動保持中の指定があり、AI からは上書きできない。</summary>
        保持により拒否,
        /// <summary>その陣形を布く資格がない（軍神専用など）。</summary>
        資格不足,
        /// <summary>指揮スキルポイントが足りない。</summary>
        ポイント不足,
        /// <summary>撤退中は陣形を変えられない。</summary>
        撤退中,
        /// <summary>対象の艦隊がいない。</summary>
        対象なし,
    }

    /// <summary>
    /// 艦隊1隊ぶんの<b>陣形の保持</b>（確定仕様1）。
    /// 指定した陣形・出どころ・そのときの指揮系統を、移動や攻撃とは<b>独立に</b>持つ。
    /// </summary>
    public readonly struct FleetFormationHold
    {
        /// <summary>手動保持中か（＝AI が上書きできない）。</summary>
        public readonly bool held;
        /// <summary>保持している陣形。</summary>
        public readonly Formation formation;
        /// <summary>誰の指定か。</summary>
        public readonly FormationOrderSource source;
        /// <summary>指定を受けたときの指揮系統（軍団キー）。所属が変わったら保持を解く。</summary>
        public readonly string corpsKey;

        public FleetFormationHold(bool held, Formation formation, FormationOrderSource source, string corpsKey)
        {
            this.held = held;
            this.formation = formation;
            this.source = source;
            this.corpsKey = corpsKey ?? "";
        }

        /// <summary>保持していない状態。</summary>
        public static FleetFormationHold None => new FleetFormationHold(false, Formation.紡錘陣, FormationOrderSource.なし, "");
    }

    /// <summary>
    /// 陣形指定の<b>受理・拒否・保持・解除</b>の決まり（純ロジック・test-first・確定仕様1）。
    ///
    /// <b>優先順位</b>（強い順）
    /// <list type="number">
    ///   <item>敗走／総退却による<b>保持の解除</b></item>
    ///   <item>最後に受理した<b>明示指定</b>（直接命令 または 承諾済みの支援要請）</item>
    ///   <item>軍団AI</item>
    ///   <item>艦隊AI</item>
    /// </list>
    ///
    /// <b>約束</b>
    /// <list type="bullet">
    ///   <item>陣形の保持は<b>移動・攻撃と独立</b>＝移動や攻撃が終わっても、停止しても、倍速でも解けない。</item>
    ///   <item>断ったときは<b>旧指定も保持もそのまま</b>＝消費なし・自動再試行なし。</item>
    ///   <item>同じ陣形の指定は<b>無料</b>で保持設定できる（保持の継続にも費用はかからない）。</item>
    ///   <item>緊急（敗走・総退却）で解けるのは<b>陣形の保持だけ</b>
    ///         ＝直接の移動／攻撃命令はここでは何も止めない。</item>
    /// </list>
    /// </summary>
    public static class FleetFormationOrderRules
    {
        /// <summary>プレイヤーの意思による明示指定か（＝保持を作る／AI に上書きされない）。</summary>
        public static bool IsExplicit(FormationOrderSource source)
            => source == FormationOrderSource.直接命令 || source == FormationOrderSource.支援要請;

        /// <summary>
        /// 陣形の指定を受け付けられない<b>退却の状態</b>か。
        ///
        /// 「撤退中」は1つのフラグでは分からない：
        /// <list type="bullet">
        ///   <item><paramref name="withdrawing"/>＝戦場から離脱した（<c>FleetStrength.IsRetreating</c>。
        ///         これは<b>戦場端に着いてから</b>立つ）。</item>
        ///   <item><paramref name="routed"/>＝士気が尽きた。</item>
        ///   <item><paramref name="aiRetreating"/>＝AI が撤退行動中（<c>FleetAI.AIState.撤退</c>）。
        ///         <b>士気は正常なまま総退却で下がっている最中</b>はこれだけが立つ。</item>
        ///   <item><paramref name="corpsRetreatOrdered"/>＝軍団が総退却を発令中。
        ///         直接命令を維持していて撤退へ落とされない<b>例外の艦</b>も、
        ///         この間は新しい陣形保持を受け付けない。</item>
        /// </list>
        /// どれか1つでも当てはまれば受け付けない（受け付けると、次の軍団周期で解除されて
        /// <b>ちらつき＋スキルポイントの無駄遣い</b>になる）。
        /// </summary>
        public static bool IsRetreatingState(bool withdrawing, bool routed,
                                             bool aiRetreating, bool corpsRetreatOrdered)
            => withdrawing || routed || aiRetreating || corpsRetreatOrdered;

        /// <summary>
        /// その出どころが、いまの保持を上書きしてよいか。
        /// 保持していなければ誰でも指定できる。保持中は<b>明示指定だけ</b>が上書きできる
        /// （明示指定どうしは「最後に受理したもの」が勝つ＝直接命令と支援要請に上下はない）。
        /// </summary>
        public static bool CanAccept(in FleetFormationHold hold, FormationOrderSource incoming)
        {
            if (incoming == FormationOrderSource.なし) return false;
            if (!hold.held) return true;
            return IsExplicit(incoming);
        }

        /// <summary>
        /// 指定を受け付けるか決める。断った場合は呼び手が<b>何も変えない</b>こと
        /// （旧指定・保持・スキルポイントをそのまま残す）。
        /// </summary>
        /// <param name="sameFormation">いまの陣形と同じ指定か（同じなら無料）。</param>
        /// <param name="hasPoints">指揮スキルポイントが足りるか。</param>
        /// <param name="qualified">その陣形を布く資格があるか（軍神専用など）。</param>
        /// <param name="retreating">撤退中か。</param>
        public static FormationOrderResult Decide(in FleetFormationHold hold, FormationOrderSource incoming,
                                                  bool sameFormation, bool hasPoints, bool qualified, bool retreating)
        {
            if (!CanAccept(hold, incoming)) return FormationOrderResult.保持により拒否;
            if (retreating) return FormationOrderResult.撤退中;
            if (!qualified) return FormationOrderResult.資格不足;
            // 同一陣形は無料＝ポイントを見ない（保持だけ付け直せる）。
            if (!sameFormation && !hasPoints) return FormationOrderResult.ポイント不足;
            return FormationOrderResult.受理;
        }

        /// <summary>
        /// 受理したあとの保持状態。
        /// 明示指定なら<b>保持あり</b>になり、AI の指定は保持を作らない（従来どおり流れるだけ）。
        /// </summary>
        public static FleetFormationHold Apply(in FleetFormationHold hold, FormationOrderSource incoming,
                                               Formation formation, string corpsKey)
        {
            if (!IsExplicit(incoming))
            {
                // AI の指定：保持は作らない。すでに保持があるなら（本来ここへ来ないが）そのまま残す。
                return hold.held ? hold : FleetFormationHold.None;
            }
            return new FleetFormationHold(true, formation, incoming, corpsKey);
        }

        /// <summary>
        /// 緊急（敗走・総退却）で保持を解くべきか。<b>出どころに関係なく解く</b>
        /// ＝崩れた部隊に陣形を守らせない。解くのは<b>陣形の保持だけ</b>で、移動・攻撃命令は対象外。
        /// </summary>
        public static bool ShouldReleaseForEmergency(bool routed, bool corpsRetreatOrdered)
            => routed || corpsRetreatOrdered;

        /// <summary>
        /// 指揮系統が変わったので保持を解くべきか（配属・指揮移譲で旧系統の指定を持ち歩かない）。
        /// </summary>
        public static bool ShouldReleaseOnCorpsChange(in FleetFormationHold hold, string currentCorpsKey)
            => hold.held && hold.corpsKey != (currentCorpsKey ?? "");

        /// <summary>
        /// 保持中の陣形へ戻すべきか（AI などで陣形がずれたときの自動復帰）。
        /// <b>復帰は陣形だけ</b>＝移動や攻撃を中断しない。
        /// </summary>
        public static bool ShouldRestore(in FleetFormationHold hold, Formation current)
            => hold.held && hold.formation != current;

        // ===== 表示（最低限） =====

        /// <summary>出どころの短い表示。</summary>
        public static string SourceLabel(FormationOrderSource source)
        {
            switch (source)
            {
                case FormationOrderSource.直接命令: return "直接命令";
                case FormationOrderSource.支援要請: return "支援要請";
                case FormationOrderSource.軍団AI: return "軍団指示";
                case FormationOrderSource.艦隊AI: return "自律";
                default: return "初期";
            }
        }

        /// <summary>
        /// いまの保持状態の1行（HUD 用）。
        ///
        /// 保持していないときは<b>最後に陣形を決めた出どころ</b>を出す
        /// （<paramref name="lastSource"/>）＝軍団長の指示で布いているのか、
        /// 艦隊が自分で選んだのかを区別できるようにする。
        /// </summary>
        public static string HoldText(in FleetFormationHold hold, Formation current,
                                      FormationOrderSource lastSource)
        {
            if (!hold.held) return $"{current}（{SourceLabel(lastSource)}）";
            string mark = hold.formation == current ? "保持" : "復帰中";
            return $"{hold.formation}（{mark}・{SourceLabel(hold.source)}）";
        }

        /// <summary>断った理由の1行（変更されていないことが分かる言い方）。</summary>
        public static string ResultText(FormationOrderResult result, Formation requested, string fleetName)
        {
            string who = string.IsNullOrEmpty(fleetName) ? "艦隊" : fleetName;
            switch (result)
            {
                case FormationOrderResult.受理:
                    return $"{who} の陣形を {requested} に指定しました";
                case FormationOrderResult.保持により拒否:
                    return $"{who} は指定された陣形を保持中です（自動では変更しません）";
                case FormationOrderResult.資格不足:
                    return $"{who} は {requested} を布けません（資格がありません）。陣形は変更していません";
                case FormationOrderResult.ポイント不足:
                    return $"{who} は指揮スキルポイントが足りません。陣形は変更していません";
                case FormationOrderResult.撤退中:
                    return $"{who} は撤退中のため陣形を変更できません";
                default:
                    return $"{who} の陣形を変更できませんでした";
            }
        }

        /// <summary>保持を解いたときの1行。</summary>
        public static string ReleaseText(string fleetName, string reason)
        {
            string who = string.IsNullOrEmpty(fleetName) ? "艦隊" : fleetName;
            string why = string.IsNullOrEmpty(reason) ? "状況の変化" : reason;
            return $"{who} の陣形保持を解除しました（{why}）";
        }
    }
}
