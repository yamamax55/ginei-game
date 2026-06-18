using UnityEngine;

namespace Ginei
{
    /// <summary>君主が臣下へ下す主命の種別（TKO-9・#2486）。武勲の規模が種別で変わる（出陣/占領＝大・巡察/練兵＝小）。</summary>
    public enum MandateKind { 出陣, 防衛, 占領, 鎮圧, 巡察, 練兵 }

    /// <summary>主命の状態（TKO-9）。拝命→遂行中→達成/失敗。失敗は期限超過か明示の失敗。</summary>
    public enum MandateStatus { 拝命, 遂行中, 達成, 失敗 }

    /// <summary>
    /// 君主から拝命した一件の主命（TKO-9・#2486・純データ）。発令者（<see cref="Person.isSovereign"/>）が臣下へ
    /// 期限つきで下し、達成で武勲（<see cref="MeritRecordRules"/>）と恩義（<see cref="PersonRelationRules"/>）を生む。
    /// 期限は<b>通算月</b>（暦の月境界で増える int・Game 層が `CalendarDispatcher` から供給）で持つ＝純ロジックは暦内部に依存しない。
    /// </summary>
    public class SovereignMandate
    {
        public int id;
        public Faction faction;
        public int issuerId;    // 発令した君主（ICharacter.id）
        public int assigneeId;  // 拝命した臣下（主人公）
        public MandateKind kind;
        public string objective = ""; // 対象（星系名等・表示用。純ロジックは未使用）
        public int issuedMonth; // 発令した通算月
        public int dueMonth;    // 期限（通算月）
        public MandateStatus status = MandateStatus.拝命;

        public SovereignMandate() { }
    }

    /// <summary>
    /// 君主からの主命の純ロジック（TKO-9・太閤立志伝V/信長の野望「主命」・#2486・唯一の窓口）。君主（<see cref="Person.isSovereign"/>）が
    /// 臣下へ主命を下し、<b>期限内の達成で武勲（<see cref="MeritRecordRules"/>）と恩義/引き立て（<see cref="PersonRelationRules"/>）を、
    /// 失敗で遺恨を生む</b>＝立身出世の駆動イベント。武勲・人脈の数値はそれぞれの窓口へ委譲する（並行系を作らない＝additive）。
    /// 期限は通算月（int）で持ち暦内部に依存しない。決定論・基準値非破壊・test-first。
    /// </summary>
    public static class SovereignMandateRules
    {
        /// <summary>主命の調整値。</summary>
        public readonly struct MandateParams
        {
            public readonly float magnitude出陣;  // 達成時の武勲規模（ExploitKind.任務達成 の magnitude）
            public readonly float magnitude防衛;
            public readonly float magnitude占領;
            public readonly float magnitude鎮圧;
            public readonly float magnitude巡察;
            public readonly float magnitude練兵;
            public readonly float favorOnSuccess;  // 達成時の恩義/親愛の増分（0..1）
            public readonly float grudgeOnFail;     // 失敗時の遺恨の増分（0..1）
            public readonly int durationMonths;     // 既定の期限（拝命からの猶予月数）

            public MandateParams(float m出陣, float m防衛, float m占領, float m鎮圧, float m巡察, float m練兵,
                float favorOnSuccess, float grudgeOnFail, int durationMonths)
            {
                this.magnitude出陣 = m出陣;
                this.magnitude防衛 = m防衛;
                this.magnitude占領 = m占領;
                this.magnitude鎮圧 = m鎮圧;
                this.magnitude巡察 = m巡察;
                this.magnitude練兵 = m練兵;
                this.favorOnSuccess = Mathf.Clamp01(favorOnSuccess);
                this.grudgeOnFail = Mathf.Clamp01(grudgeOnFail);
                this.durationMonths = Mathf.Max(1, durationMonths);
            }

            /// <summary>既定＝出陣20/防衛15/占領25/鎮圧12/巡察5/練兵4・達成恩義0.25・失敗遺恨0.2・猶予6ヶ月。</summary>
            public static MandateParams Default
                => new MandateParams(20f, 15f, 25f, 12f, 5f, 4f, 0.25f, 0.2f, 6);
        }

        /// <summary>達成時に積む武勲の規模（種別ごと）。</summary>
        public static float MeritMagnitudeFor(MandateKind kind, MandateParams prm)
        {
            switch (kind)
            {
                case MandateKind.出陣: return prm.magnitude出陣;
                case MandateKind.防衛: return prm.magnitude防衛;
                case MandateKind.占領: return prm.magnitude占領;
                case MandateKind.鎮圧: return prm.magnitude鎮圧;
                case MandateKind.巡察: return prm.magnitude巡察;
                default:               return prm.magnitude練兵;
            }
        }

        /// <summary>発令できるか＝発令者が君主・存命の臣下・同勢力・自分以外。</summary>
        public static bool CanIssue(Person issuer, Person assignee)
            => issuer != null && issuer.isSovereign
               && assignee != null && assignee.IsAvailable
               && issuer.faction == assignee.faction
               && issuer.id != assignee.id;

        /// <summary>主命を発令する（拝命・期限＝現通算月＋猶予月数）。資格が無ければ null。</summary>
        public static SovereignMandate Issue(int id, Person issuer, Person assignee, MandateKind kind,
            string objective, int currentMonth, MandateParams prm)
        {
            if (!CanIssue(issuer, assignee)) return null;
            return new SovereignMandate
            {
                id = id,
                faction = issuer.faction,
                issuerId = issuer.id,
                assigneeId = assignee.id,
                kind = kind,
                objective = objective ?? "",
                issuedMonth = currentMonth,
                dueMonth = currentMonth + prm.durationMonths,
                status = MandateStatus.拝命,
            };
        }

        /// <summary>
        /// 委譲発令＝直属の上官が主命を噛み砕いて部下へ下す（MBO カスケード・TKO-11 #2488）。君主ゲート（<see cref="CanIssue"/>）を
        /// 通さない＝権限はカスケード上流（君主）から指揮系統で委譲されている前提。発令者/勢力を直接受ける。
        /// 発令者・拝命者の id が無効（0/同一）なら null。<see cref="MandateCascadeRules.ToLeafMandate"/> が使う窓口。
        /// </summary>
        public static SovereignMandate IssueDelegated(int id, int issuerId, int assigneeId, Faction faction,
            MandateKind kind, string objective, int currentMonth, MandateParams prm)
        {
            if (issuerId == 0 || assigneeId == 0 || issuerId == assigneeId) return null;
            return new SovereignMandate
            {
                id = id,
                faction = faction,
                issuerId = issuerId,
                assigneeId = assigneeId,
                kind = kind,
                objective = objective ?? "",
                issuedMonth = currentMonth,
                dueMonth = currentMonth + prm.durationMonths,
                status = MandateStatus.拝命,
            };
        }

        /// <summary>拝命→遂行中（着手）。拝命状態でなければ何もしない。</summary>
        public static bool Begin(SovereignMandate m)
        {
            if (m == null || m.status != MandateStatus.拝命) return false;
            m.status = MandateStatus.遂行中;
            return true;
        }

        /// <summary>未決（拝命/遂行中）か。</summary>
        public static bool IsOpen(SovereignMandate m)
            => m != null && (m.status == MandateStatus.拝命 || m.status == MandateStatus.遂行中);

        /// <summary>期限超過か＝未決のまま現通算月が期限を過ぎた。</summary>
        public static bool IsOverdue(SovereignMandate m, int currentMonth)
            => IsOpen(m) && currentMonth > m.dueMonth;

        /// <summary>
        /// 達成を確定する：状態を達成にし、武勲を <paramref name="merit"/> へ <see cref="ExploitKind.任務達成"/> として積む
        /// （規模＝<see cref="MeritMagnitudeFor"/>）。恩義の増分（<see cref="MandateParams.favorOnSuccess"/>）を返す
        /// （関係グラフへの反映は <see cref="ApplyOutcomeFavor"/> で・Game層が呼ぶ）。未決でなければ何もせず0。
        /// </summary>
        public static float Complete(SovereignMandate m, MeritRecord merit, MeritRecordRules.MeritRecordParams meritPrm,
            MandateParams prm)
        {
            if (!IsOpen(m)) return 0f;
            m.status = MandateStatus.達成;
            if (merit != null)
                MeritRecordRules.Record(merit, ExploitKind.任務達成, MeritMagnitudeFor(m.kind, prm), meritPrm);
            return prm.favorOnSuccess;
        }

        /// <summary>失敗を確定する：状態を失敗にし、遺恨の増分（<see cref="MandateParams.grudgeOnFail"/>）を返す。未決でなければ0。</summary>
        public static float Fail(SovereignMandate m, MandateParams prm)
        {
            if (!IsOpen(m)) return 0f;
            m.status = MandateStatus.失敗;
            return prm.grudgeOnFail;
        }

        /// <summary>
        /// 主命の決着を関係グラフへ反映する：達成（delta&gt;0）＝臣下→君主の恩義・君主→臣下の親愛を <paramref name="delta"/> ぶん積み増す、
        /// 失敗（delta&lt;0）＝君主→臣下の遺恨を |delta| ぶん積み増す。強さは累積（既存に加算してクランプ）。
        /// 二者関係の窓口は <see cref="PersonRelationRules"/>（並行系を作らない）。
        /// </summary>
        public static void ApplyOutcomeFavor(PersonRelationGraph g, SovereignMandate m, float delta)
        {
            if (g == null || m == null || Mathf.Approximately(delta, 0f)) return;
            if (delta > 0f)
            {
                Reinforce(g, m.assigneeId, m.issuerId, RelationKind.恩義, delta);
                Reinforce(g, m.issuerId, m.assigneeId, RelationKind.親愛, delta);
            }
            else
            {
                Reinforce(g, m.issuerId, m.assigneeId, RelationKind.遺恨, -delta);
            }
        }

        // 既存の縁に強さを積み増す（なければ新規）。クランプ 0..1。
        private static void Reinforce(PersonRelationGraph g, int from, int to, RelationKind kind, float add)
        {
            var existing = g.Get(from, to, kind);
            float cur = existing != null ? existing.strength : 0f;
            g.Set(from, to, kind, Mathf.Clamp01(cur + add));
        }
    }
}
