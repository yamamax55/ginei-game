using System.Globalization;
using System.Text;

namespace Ginei
{
    /// <summary>
    /// 省内職位の人事で問う1件の内容（どの省の誰を、どうするか）。稟議の効果キー（<see cref="CivilServiceRingiRules.Encode"/>）に
    /// そのまま直列化できる<b>構造化された決定（WHAT）だけ</b>を持つ＝表示文面・人物名は入れない（純データ・決定論）。
    /// </summary>
    public readonly struct CivilServicePostRequest
    {
        public readonly CivilServiceAction action;
        /// <summary>対象の省（<see cref="Ministry.id"/>）。</summary>
        public readonly int ministryId;
        /// <summary>対象の官僚（<see cref="Person.id"/>）。</summary>
        public readonly int personId;
        /// <summary>
        /// 承認を問う段。配属＝一般官僚／昇任・降任＝就ける段／異動・解任＝起票時の現職の段
        /// （<see cref="CivilServiceRingiRules.ResolveApprovalGrade"/> が解決する）。
        /// </summary>
        public readonly BureaucratGrade targetGrade;

        public CivilServicePostRequest(CivilServiceAction action, int ministryId, int personId, BureaucratGrade targetGrade)
        {
            this.action = action;
            this.ministryId = ministryId;
            this.personId = personId;
            this.targetGrade = targetGrade;
        }

        /// <summary>省・人物・行為・段がすべて既知の範囲にあるか。</summary>
        public bool IsValid => ministryId >= 0 && personId >= 0
                               && System.Enum.IsDefined(typeof(CivilServiceAction), action)
                               && System.Enum.IsDefined(typeof(BureaucratGrade), targetGrade);
    }

    /// <summary>人事の申し出がどう扱われたか（直接実行／上申／受け付けない）。</summary>
    public enum CivilServiceRequestOutcome
    {
        /// <summary>権限があったので即時に台帳へ反映した。</summary>
        実行,
        /// <summary>権限外だったので決裁待ちへ載せた（裁可されるまで台帳は動かない）。</summary>
        上申,
        /// <summary>受け付けない（理由つき・台帳は動かない）。</summary>
        却下,
    }

    /// <summary>
    /// 人事の申し出の結果（呼出側＝操作画面が「直接効いたのか・上申されたのか・断られたのか」を1つで判別するための小さな返り値）。
    /// 純データ。
    /// </summary>
    public readonly struct CivilServiceRequestResult
    {
        public readonly CivilServiceRequestOutcome outcome;
        /// <summary>画面にそのまま出す1行（許可の根拠／上申先／拒否の理由）。</summary>
        public readonly string reason;
        /// <summary>上申したときの決裁カード id（それ以外は -1）。</summary>
        public readonly int decisionId;
        /// <summary>上申先の人物 id（それ以外は -1）。</summary>
        public readonly int addresseeId;
        /// <summary>上申したときの効果キー（それ以外は空）。</summary>
        public readonly string effectKey;

        public CivilServiceRequestResult(CivilServiceRequestOutcome outcome, string reason,
            int decisionId = -1, int addresseeId = -1, string effectKey = "")
        {
            this.outcome = outcome;
            this.reason = reason ?? "";
            this.decisionId = decisionId;
            this.addresseeId = addresseeId;
            this.effectKey = effectKey ?? "";
        }

        public static CivilServiceRequestResult Executed(string reason)
            => new CivilServiceRequestResult(CivilServiceRequestOutcome.実行, reason);

        public static CivilServiceRequestResult Petitioned(int decisionId, int addresseeId, string effectKey, string reason)
            => new CivilServiceRequestResult(CivilServiceRequestOutcome.上申, reason, decisionId, addresseeId, effectKey);

        public static CivilServiceRequestResult Rejected(string reason)
            => new CivilServiceRequestResult(CivilServiceRequestOutcome.却下, reason);

        /// <summary>台帳が実際に動いたか。</summary>
        public bool DidExecute => outcome == CivilServiceRequestOutcome.実行;
        /// <summary>決裁待ちへ載ったか。</summary>
        public bool DidPetition => outcome == CivilServiceRequestOutcome.上申;
        /// <summary>受け付けられたか（実行 or 上申）。</summary>
        public bool Accepted => outcome != CivilServiceRequestOutcome.却下;
    }

    /// <summary>
    /// 省内職位の人事（<see cref="CivilServicePostRules"/>・#141）を<b>既存の稟議・決裁へ載せるための橋渡し</b>（純ロジック・test-first）。
    ///
    /// <para><b>持つもの</b>＝①保存できる ASCII の効果キーの相互変換（<see cref="Encode"/>/<see cref="TryDecode"/>）
    /// ②承認を問う段の解決（<see cref="ResolveApprovalGrade"/>）③カードの文面と理由の出し入れ（<see cref="Describe"/>/
    /// <see cref="ComposeBody"/>/<see cref="ExtractReason"/>）だけ。</para>
    ///
    /// <para><b>持たないもの</b>＝人事の可否・資格・空席・在職年・承認権限は <see cref="CivilServicePostRules.Check"/>／
    /// <see cref="CivilServicePostRules.ApprovalAuthority"/>／<see cref="CivilServicePostRules.Execute"/> が唯一の窓口
    /// （ここでは一切判定しない＝二重実装しない）。稟議の状態遷移は <see cref="WorkflowRules"/>/<see cref="RingiPipeline"/>、
    /// 決裁の一度きりの適用は <see cref="DecisionResolutionRules"/> に委ねる。</para>
    ///
    /// <para><b>効果キー</b>＝<c>civilservice.post:1:{action}:{ministryId}:{personId}:{grade}</c>（数字と ':' だけの ASCII・版番号つき）。
    /// 人物名や文面はキーに入れない（保存して読み戻しても同じ人事へ復元できる・表示が変わってもキーは変わらない）。
    /// 復元は<b>正規形と完全一致するものだけ</b>を通す＝未知の版・範囲外の値・余分な項目・符号や空白つきの数字は拒否する。</para>
    /// </summary>
    public static class CivilServiceRingiRules
    {
        /// <summary>効果キーの接頭辞（<see cref="Petition.effectKey"/>／<see cref="PendingDecision.effectKey"/> 用）。</summary>
        public const string EffectPrefix = "civilservice.post";

        /// <summary>効果キーの版（構造を変えたら上げる。古い版は復元せず拒否する）。</summary>
        public const int KeyVersion = 1;

        /// <summary>効果キーの項目数（接頭辞・版・行為・省・人物・段）。</summary>
        public const int KeyFieldCount = 6;

        /// <summary>理由の見出し（カード本文の最終行＝<see cref="ExtractReason"/> が読む目印）。</summary>
        public const string ReasonLabel = "理由：";

        /// <summary>カードへ載せる理由の最大長（画面と履歴を長文で埋めない）。</summary>
        public const int MaxReasonLength = 120;

        // ===== 効果キー =====

        /// <summary>人事の効果キーを組み立てる（保存・決裁カード・稟議で同じ1本を使う）。</summary>
        public static string Encode(in CivilServicePostRequest req)
            => EffectPrefix
               + ":" + KeyVersion.ToString(CultureInfo.InvariantCulture)
               + ":" + ((int)req.action).ToString(CultureInfo.InvariantCulture)
               + ":" + req.ministryId.ToString(CultureInfo.InvariantCulture)
               + ":" + req.personId.ToString(CultureInfo.InvariantCulture)
               + ":" + ((int)req.targetGrade).ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// 効果キーから人事の内容を復元する。<b>正規形（<see cref="Encode"/> の出力）と完全一致するものだけ</b>を通す
        /// ＝接頭辞違い・未知の版・項目数違い・範囲外の行為/段・負数・符号や空白つきの数字はすべて false（内容は既定値のまま）。
        /// </summary>
        public static bool TryDecode(string effectKey, out CivilServicePostRequest req)
        {
            req = default;
            if (string.IsNullOrEmpty(effectKey)) return false;
            string[] parts = effectKey.Split(':');
            if (parts.Length != KeyFieldCount) return false;
            if (!string.Equals(parts[0], EffectPrefix, System.StringComparison.Ordinal)) return false;
            if (!TryNonNegative(parts[1], out int version) || version != KeyVersion) return false;
            if (!TryNonNegative(parts[2], out int action)) return false;
            if (!System.Enum.IsDefined(typeof(CivilServiceAction), action)) return false;
            if (!TryNonNegative(parts[3], out int ministryId)) return false;
            if (!TryNonNegative(parts[4], out int personId)) return false;
            if (!TryNonNegative(parts[5], out int grade)) return false;
            if (!System.Enum.IsDefined(typeof(BureaucratGrade), grade)) return false;

            var decoded = new CivilServicePostRequest((CivilServiceAction)action, ministryId, personId, (BureaucratGrade)grade);
            // 正規形だけを受け入れる（"…:007" のような別表記を同じ人事として通さない＝キーの一意性を保つ）
            if (!string.Equals(Encode(decoded), effectKey, System.StringComparison.Ordinal)) return false;
            req = decoded;
            return true;
        }

        /// <summary>人事の効果キーか（どの経路が扱うかの振り分け用・中身の妥当性は <see cref="TryDecode"/> が見る）。</summary>
        public static bool IsCivilServiceKey(string effectKey)
            => !string.IsNullOrEmpty(effectKey)
               && effectKey.StartsWith(EffectPrefix + ":", System.StringComparison.Ordinal);

        /// <summary>数字だけの非負整数か（符号・空白・桁区切りを許さない＝キーを culture に依らせない）。</summary>
        private static bool TryNonNegative(string text, out int value)
            => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value >= 0;

        // ===== 承認を問う段 =====

        /// <summary>
        /// その人事で<b>承認を問う段</b>（<see cref="CivilServicePostRules.ApprovalAuthority"/> へ渡す段）。
        /// 配属＝一般官僚（入省の段）／昇任・降任＝就ける段／異動・解任＝台帳の現職の段（在任していなければ指定の段のまま）。
        /// 台帳は読むだけ（<see cref="CivilServicePostRules.FindServing"/> 経由）＝可否の判定はしない。
        /// </summary>
        public static BureaucratGrade ResolveApprovalGrade(CivilServiceState st, CivilServiceAction action,
            int personId, BureaucratGrade targetGrade)
        {
            switch (action)
            {
                case CivilServiceAction.配属:
                    return BureaucratGrade.一般官僚;
                case CivilServiceAction.昇任:
                case CivilServiceAction.降任:
                    return targetGrade;
                default: // 異動・解任は段を変えない＝いま就いている段を問う
                    CivilServiceRecord cur = CivilServicePostRules.FindServing(st, personId);
                    return cur != null ? cur.grade : targetGrade;
            }
        }

        // ===== 文面（理由の出し入れ） =====

        /// <summary>理由が空のときに使う既定文（黙って空欄にしない）。</summary>
        public static string DefaultReason(CivilServiceAction action) => "所定の人事（" + action + "）";

        /// <summary>
        /// カード本文と人事履歴へ残す理由を整える：空白のみ／null は <see cref="DefaultReason"/>、改行は空白へ畳み、
        /// <see cref="MaxReasonLength"/> で切り詰め、見出しと紛れる "理由：" は書き換える（最終行の目印を壊さない）。
        /// </summary>
        public static string SafeReason(CivilServiceAction action, string reason)
        {
            if (string.IsNullOrEmpty(reason)) return DefaultReason(action);
            string t = reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (t.Length == 0) return DefaultReason(action);
            t = t.Replace(ReasonLabel, "理由 ");
            if (t.Length > MaxReasonLength) t = t.Substring(0, MaxReasonLength);
            return t;
        }

        /// <summary>決裁カードの題名（人事の中身を1行で）。名前はキーに入れず<b>ここでだけ</b>使う。</summary>
        public static string Describe(in CivilServicePostRequest req, string ministryName, string personLabel,
            BureaucratGrade grade)
        {
            string who = string.IsNullOrEmpty(personLabel) ? "人物#" + req.personId : personLabel;
            string post = CivilServicePostRules.GradeTitle(ministryName, grade);
            switch (req.action)
            {
                case CivilServiceAction.配属: return "［人事］" + who + " を " + post + " へ配属";
                case CivilServiceAction.異動: return "［人事］" + who + " を " + post + " へ異動";
                case CivilServiceAction.昇任: return "［人事］" + who + " を " + post + " へ昇任";
                case CivilServiceAction.降任: return "［人事］" + who + " を " + post + " へ降任";
                default: return "［人事］" + post + " " + who + " を解任";
            }
        }

        /// <summary>
        /// 決裁カードの本文（提案者・決裁者・権限の根拠・対象・理由）。
        /// <b>理由は必ず最終行</b>に置く＝裁可の時に <see cref="ExtractReason"/> が同じ理由を人事履歴へ持って行ける
        /// （保存形式に新しい項目を足さずにカード本文へ持たせる）。
        /// </summary>
        public static string ComposeBody(string description, string targetLabel, string proposerLabel,
            string addresseeLabel, string authorityBasis, string reason)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(description) ? "省内職位の人事" : description);
            sb.Append('\n').Append("対象：").Append(string.IsNullOrEmpty(targetLabel) ? "（不明）" : targetLabel);
            sb.Append('\n').Append("提案：").Append(string.IsNullOrEmpty(proposerLabel) ? "（不明）" : proposerLabel)
              .Append(" ／ 決裁：").Append(string.IsNullOrEmpty(addresseeLabel) ? "（所管）" : addresseeLabel);
            if (!string.IsNullOrEmpty(authorityBasis)) sb.Append('\n').Append("権限：").Append(authorityBasis);
            sb.Append('\n').Append(ReasonLabel).Append(reason ?? "");
            return sb.ToString();
        }

        /// <summary>カード本文から理由を取り出す（最終行の <see cref="ReasonLabel"/> 以降。無ければ空）。</summary>
        public static string ExtractReason(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            int at = body.LastIndexOf(ReasonLabel, System.StringComparison.Ordinal);
            if (at < 0) return "";
            string tail = body.Substring(at + ReasonLabel.Length);
            int nl = tail.IndexOf('\n');
            if (nl >= 0) tail = tail.Substring(0, nl);
            return tail.Trim();
        }
    }
}
