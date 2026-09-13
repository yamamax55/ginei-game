using System.Collections.Generic;

namespace Ginei
{
    /// <summary>その案件に対して、その人物が実際にできること。</summary>
    public enum DecisionAuthority
    {
        /// <summary>自分の権限で裁可・執行できる。</summary>
        裁可,
        /// <summary>権限外だが、適任者へ上申・要請はできる。</summary>
        上申,
        /// <summary>権限外で、上申先も居ない（＝いまはどうにもならない）。</summary>
        権限外,
    }

    /// <summary>権限判定の結果（何ができるか・なぜか・誰へ上げるか）。</summary>
    public readonly struct DecisionAuthorityResult
    {
        public readonly DecisionAuthority authority;
        /// <summary>判断の根拠（役職名・所掌・範囲、または不足している要件）。画面にそのまま出す。</summary>
        public readonly string basis;
        /// <summary>上申先の人物ID（<see cref="DecisionAuthority.上申"/> のときのみ有効・0=箱宛て）。</summary>
        public readonly int addresseeId;
        /// <summary>上申先の名前（表示用・空なら不明）。</summary>
        public readonly string addresseeName;

        public DecisionAuthorityResult(DecisionAuthority authority, string basis,
                                       int addresseeId = -1, string addresseeName = "")
        {
            this.authority = authority;
            this.basis = basis ?? "";
            this.addresseeId = addresseeId;
            this.addresseeName = addresseeName ?? "";
        }

        /// <summary>自分で決められるか。</summary>
        public bool CanDecide => authority == DecisionAuthority.裁可;
    }

    /// <summary>
    /// <b>誰が何を決められるか</b>（GitHub #67 の採用方針・稟議完成の共通基盤）。
    ///
    /// <b>前提</b>：プレイヤーは<b>いち人物</b>であって全能の裁可者ではない。
    /// 右下にカードが出たからといって何でも裁可できる、という扱いにしない。
    ///
    /// <b>判定の順序</b>（既存の窓口へ委譲し、新しい権限体系を作らない）
    /// <list type="number">
    ///   <item><b>役職 scope × domain</b>（<see cref="OfficeRules.CanPropose"/>）＝これが主。
    ///   国家の軍事所掌なら軍事案件を、星系総督なら自分の星系のことを決められる。</item>
    ///   <item><b>文民統制</b>（<see cref="CivilianControlRules"/>）＝軍人が政治案件（内政・外交・財政）を
    ///   決められるかは政体しだい。これは役職とは<b>独立の制約</b>で、役職があっても覆せない。</item>
    ///   <item><b>階級</b>は補助＝役職が無いときに「どの規模まで扱えるか」の目安にのみ使う。
    ///   <b>高い階級だから全権</b>にはしない。</item>
    /// </list>
    ///
    /// 権限が無ければ<b>上申</b>（<see cref="PersonRingiRules.CanRaiseTo"/> の相手＝上官／所掌の役職者）へ回す。
    /// 適任者が居なければ理由つきで不可（勝手な代行を作らない）。
    ///
    /// 乱数なし・決定論。純ロジック（非 MonoBehaviour・test-first）。
    /// </summary>
    public static class DecisionAuthorityRules
    {
        /// <summary>効果キー→所掌。未知は内政（最も内向きの扱い）。</summary>
        public static OfficeDomain DomainOf(string effectKey)
        {
            if (string.IsNullOrEmpty(effectKey)) return OfficeDomain.内政;
            if (effectKey.StartsWith("tax.", System.StringComparison.Ordinal)) return OfficeDomain.財政;
            if (effectKey.StartsWith("mil.", System.StringComparison.Ordinal)) return OfficeDomain.軍事;
            if (effectKey.StartsWith("fleet.", System.StringComparison.Ordinal)) return OfficeDomain.軍事;
            if (effectKey.StartsWith("diplo.", System.StringComparison.Ordinal)) return OfficeDomain.外交;
            return OfficeDomain.内政;
        }

        /// <summary>その所掌が「政治案件」か（軍人が触れるかを文民統制が決める領域）。</summary>
        public static bool IsPolitical(OfficeDomain domain)
            => domain == OfficeDomain.内政 || domain == OfficeDomain.外交 || domain == OfficeDomain.財政;

        /// <summary>
        /// その人物がその案件を裁可できるか／上申すべきか。
        ///
        /// <paramref name="offices"/>＝その人物が就いている役職（<see cref="GovernmentRegistry.GetOffices"/>）。
        /// <paramref name="findAddressee"/>＝上申先を探す関数（所掌を渡すと適任者を返す。null 可）。
        /// </summary>
        public static DecisionAuthorityResult Evaluate(
            ICharacter actor, string effectKey, OfficeScope scope,
            IEnumerable<Office> offices, CivilianControlType control,
            System.Func<OfficeDomain, ICharacter> findAddressee = null)
        {
            if (actor == null)
                return new DecisionAuthorityResult(DecisionAuthority.権限外, "決裁する人物がいません");

            OfficeDomain domain = DomainOf(effectKey);

            // ① 文民統制＝役職より先に効く独立の制約。軍人が政治案件を決められない政体では、
            //    どんな役職を持っていても裁可させない（役職で覆せない）。
            if (actor.IsMilitary && IsPolitical(domain)
                && !CivilianControlRules.MilitaryMayHoldPoliticalOffice(control))
            {
                ICharacter civil = findAddressee?.Invoke(domain);
                return civil != null
                    ? new DecisionAuthorityResult(DecisionAuthority.上申,
                        $"文民統制（{control}）：軍人は{domain}の案件を決裁できません。所管へ上申します",
                        civil.Id, civil.CharacterName)
                    : new DecisionAuthorityResult(DecisionAuthority.権限外,
                        $"文民統制（{control}）：軍人は{domain}の案件を決裁できず、所管の適任者もいません");
            }

            // ② 役職 scope × domain＝主たる判定。
            if (OfficeRules.CanPropose(offices, domain, scope))
                return new DecisionAuthorityResult(DecisionAuthority.裁可,
                    $"{OfficeBasis(offices, domain, scope)}（{domain}・{scope}）");

            // ③ 権限が無い＝上申する。上申先は所掌の役職者。
            ICharacter to = findAddressee?.Invoke(domain);
            if (to != null)
                return new DecisionAuthorityResult(DecisionAuthority.上申,
                    $"{domain}・{scope} を決裁する役職に就いていません。所管へ上申します", to.Id, to.CharacterName);

            return new DecisionAuthorityResult(DecisionAuthority.権限外,
                $"{domain}・{scope} を決裁する権限が無く、上申できる適任者もいません");
        }

        /// <summary>判定の根拠になった役職名（見つからなければ「役職」）。</summary>
        private static string OfficeBasis(IEnumerable<Office> offices, OfficeDomain domain, OfficeScope scope)
        {
            if (offices == null) return "役職";
            foreach (Office o in offices)
            {
                if (o == null) continue;
                bool domainOk = (o.domain == domain) || (o.domain == OfficeDomain.元首);
                if (domainOk && OfficeRules.CoversScope(o.scope, scope))
                    return string.IsNullOrEmpty(o.officeName) ? "役職" : o.officeName;
            }
            return "役職";
        }

        // ===== 会戦の指揮系統（#67：自系統は直接操作・他系統は要請） =====

        /// <summary>
        /// その艦隊を<b>直接動かせる</b>か（会戦の操作感を保つための判定）。
        ///
        /// 自分の指揮系統の内側＝直接操作（毎回の稟議は要らない）。
        /// 外側＝直接命令を通さず、相手へ<b>支援要請</b>にする。
        ///
        /// <paramref name="actorCorpsId"/>＝操作している人物が指揮する軍団（-1＝軍団を持たない）。
        /// <paramref name="actorCommandsAll"/>＝艦隊全体の指揮権（総司令官）を持つか。
        /// <paramref name="targetCorpsId"/>／<paramref name="targetCommanderId"/>＝対象艦隊の所属と司令。
        /// <paramref name="actorPersonId"/>＝操作している人物。自分が司令の艦隊は当然動かせる。
        /// </summary>
        public static bool CanCommandDirectly(int actorPersonId, int actorCorpsId, bool actorCommandsAll,
                                              int targetCorpsId, int targetCommanderId)
        {
            if (actorCommandsAll) return true;                       // 総司令官＝全系統が自系統
            if (targetCommanderId >= 0 && targetCommanderId == actorPersonId) return true; // 自分の艦隊
            if (actorCorpsId >= 0 && targetCorpsId == actorCorpsId) return true;           // 自分の軍団の隷下
            return false;
        }

        /// <summary>直接命令できない相手への説明（要請に切り替える理由）。</summary>
        public static string OutOfChainText(string targetName)
            => string.IsNullOrEmpty(targetName)
                ? "指揮系統外のため直接命令できません（支援要請になります）"
                : $"{targetName} は指揮系統外のため直接命令できません（支援要請になります）";

        /// <summary>
        /// 混在選択の説明＝直接動かせる隊と、要請にしかできない隊を分けて示す。
        /// 色だけに頼らず<b>文章で</b>出すための1行。
        /// </summary>
        public static string MixedSelectionText(int directCount, int requestCount)
        {
            if (requestCount <= 0) return "";
            if (directCount <= 0) return $"選択した {requestCount} 隊はすべて指揮系統外です（支援要請になります）";
            return $"直接命令 {directCount} 隊／指揮系統外 {requestCount} 隊は支援要請になります";
        }
    }
}
