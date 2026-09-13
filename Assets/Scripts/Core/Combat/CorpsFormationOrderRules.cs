using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Ginei
{
    /// <summary>軍団隊形命令の出所（誰がその隊形を決めたか）。手動＝プレイヤー命令、自動＝AI ドクトリン。</summary>
    public enum CorpsOrderSource
    {
        自動,
        手動,
    }

    /// <summary>
    /// 軍団ごとの隊形命令（純データ＝MonoBehaviour 非依存）。<b>軍団全体の隊形</b>＝軍団内で「艦隊をどう並べるか」を表す。
    /// 各艦隊内の配下艦の陣形（<see cref="Squadron"/> が持つ <see cref="Formation"/>）とは<b>別レイヤー</b>で、混同しない。
    /// </summary>
    public struct CorpsFormationOrder
    {
        public string corpsKey;            // 軍団キー（シーン＋勢力＋軍団名）
        public Formation formation;        // 軍団全体の隊形（艦隊の並べ方）
        public CorpsOrderSource source;    // 手動／自動
        public bool active;                // 有効な命令か

        public CorpsFormationOrder(string corpsKey, Formation formation, CorpsOrderSource source)
        {
            this.corpsKey = corpsKey;
            this.formation = formation;
            this.source = source;
            this.active = true;
        }
    }

    /// <summary>
    /// 軍団隊形<b>命令</b>の純ロジック（test-first）。「どの軍団の命令か（キー）」「手動と AI 自動のどちらを採るか」
    /// 「隷下艦隊をどのスロットに就けるか（軍団長基準の局所座標）」「隊形が整ったか」「前列交代の並び替え」を担う。
    ///
    /// <para><b>軍団キーはシーンを含む</b>：ウィンドウ化会戦（WIN-2）は会戦ごとに additive で別シーンへ載るため、
    /// 「勢力＋軍団名」だけでは別戦場の同名軍団と混ざる。必ず <see cref="MakeKey"/> でシーンキー込みのキーを作る。</para>
    ///
    /// <para><b>手動と AI 自動の優先順位（方針）</b>：手動命令が生きている限り AI の推奨隊形は<b>上書きしない</b>
    /// （＝AI の解決周期を跨いでも手動指定が意図せず戻らない）。手動が解けるのは「プレイヤーの明示解除」
    /// 「軍団の消滅（生存メンバー0）」「総退却の下令（生存優先で AI へ返す）」の3つだけ＝<see cref="ShouldReleaseManual"/>。</para>
    ///
    /// 幾何そのもの（スロット座標の生成）は <see cref="CorpsFormationRules"/> に委譲し、二重実装しない。
    /// </summary>
    public static class CorpsFormationOrderRules
    {
        /// <summary>軍団キーの区切り文字。</summary>
        public const string KeySeparator = "|";
        /// <summary>軍団名を持たない臨時編成のキーに付ける接頭辞（軍団長 id で一意化する）。</summary>
        public const string AdhocPrefix = "臨時軍団#";
        /// <summary>隊形が「整った」とみなす既定の許容ズレ（世界単位）。</summary>
        public const float DefaultFormTolerance = 8f;

        // ===== 軍団キー（シーン＋勢力＋軍団名）=====

        /// <summary>軍団キーを作る。<b>シーンキーを含める</b>ことで別戦場（additive の同名シーン）の同名軍団と混ざらない。</summary>
        public static string MakeKey(int sceneKey, string factionName, string corpsName)
        {
            string f = string.IsNullOrEmpty(factionName) ? "?" : factionName;
            string c = corpsName ?? "";
            return sceneKey.ToString(CultureInfo.InvariantCulture) + KeySeparator + f + KeySeparator + c;
        }

        /// <summary>軍団名を持たない臨時編成のキー（軍団長 id で一意＝別の臨時編成と混ざらない）。</summary>
        public static string MakeAdhocKey(int sceneKey, string factionName, long commanderId)
            => MakeKey(sceneKey, factionName, AdhocPrefix + commanderId.ToString(CultureInfo.InvariantCulture));

        /// <summary>キーからシーンキーを取り出す（不正なキーは int.MinValue）。</summary>
        public static int SceneKeyOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return int.MinValue;
            int i = key.IndexOf(KeySeparator, System.StringComparison.Ordinal);
            if (i <= 0) return int.MinValue;
            return int.TryParse(key.Substring(0, i), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)
                ? v : int.MinValue;
        }

        /// <summary>キーから勢力名を取り出す。</summary>
        public static string FactionOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string[] parts = key.Split(new[] { KeySeparator }, 3, System.StringSplitOptions.None);
            return parts.Length >= 2 ? parts[1] : "";
        }

        /// <summary>キーから軍団名を取り出す（表示用）。</summary>
        public static string CorpsNameOf(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string[] parts = key.Split(new[] { KeySeparator }, 3, System.StringSplitOptions.None);
            return parts.Length >= 3 ? parts[2] : "";
        }

        /// <summary>そのキーが臨時編成（軍団名なしの選択編成）か。</summary>
        public static bool IsAdhoc(string key)
            => CorpsNameOf(key).StartsWith(AdhocPrefix, System.StringComparison.Ordinal);

        /// <summary>表示用の軍団名（臨時編成は id を伏せて「臨時軍団」）。</summary>
        public static string DisplayName(string key)
        {
            string c = CorpsNameOf(key);
            if (string.IsNullOrEmpty(c)) return "臨時軍団";
            return c.StartsWith(AdhocPrefix, System.StringComparison.Ordinal) ? "臨時軍団" : c;
        }

        /// <summary>そのキーが指定シーンのものか（別戦場の軍団を弾く）。</summary>
        public static bool BelongsToScene(string key, int sceneKey) => SceneKeyOf(key) == sceneKey;

        /// <summary>2つのキーが同一軍団を指すか（null 安全・完全一致）。</summary>
        public static bool IsSameCorps(string a, string b) => !string.IsNullOrEmpty(a) && a == b;

        // ===== 手動 / AI 自動の優先順位 =====

        /// <summary>AI（ドクトリン）が軍団隊形を被せてよいか＝手動命令が無いときだけ。</summary>
        public static bool AiMayOverride(bool hasManualOrder) => !hasManualOrder;

        /// <summary>
        /// 実際に適用する軍団隊形を決める。手動命令があれば手動が勝ち（AI の周期で戻らない）、無ければ AI 推奨。
        /// </summary>
        public static Formation ResolveFormation(bool hasManualOrder, Formation manual, Formation aiRecommended,
                                                 out CorpsOrderSource applied)
        {
            if (hasManualOrder) { applied = CorpsOrderSource.手動; return manual; }
            applied = CorpsOrderSource.自動;
            return aiRecommended;
        }

        /// <summary>
        /// 手動命令を解除して AI 自動へ戻すべきか。解除条件は3つだけ＝明示解除／軍団消滅／総退却の下令。
        /// 敵接近・時間経過・AI の推奨変化では解除しない（手動指定を保つ）。
        /// </summary>
        public static bool ShouldReleaseManual(bool hasManualOrder, bool anyMemberAlive, bool retreatOrdered,
                                               bool explicitRelease)
        {
            if (!hasManualOrder) return false;
            return explicitRelease || !anyMemberAlive || retreatOrdered;
        }

        // ===== スロット割り当て（軍団長基準の局所座標）=====

        /// <summary>スロット群から軍団長スロットの局所座標を取り出す（無ければ原点）。</summary>
        public static Vector2 CommanderLocal(IReadOnlyList<CorpsSlot> slots)
        {
            if (slots == null) return Vector2.zero;
            for (int i = 0; i < slots.Count; i++) if (slots[i].commander) return slots[i].localPos;
            return Vector2.zero;
        }

        /// <summary>
        /// 隷下艦隊（軍団長を除く）ぶんのスロット局所座標を、<b>軍団長スロットを原点</b>として前→後の順に返す。
        /// MonoBehaviour 側はこれを軍団正面で回して軍団長の現在位置に足すだけ＝軍団が移動しても追従する。
        /// </summary>
        public static List<Vector2> SubordinateSlotOffsets(int fleetCount, Formation formation, float spacing)
        {
            var result = new List<Vector2>();
            if (fleetCount <= 1) return result;
            List<CorpsSlot> slots = CorpsFormationRules.ComputeSlots(fleetCount, formation, spacing);
            Vector2 cmd = CommanderLocal(slots);
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].commander) continue;
                result.Add(slots[i].localPos - cmd);
            }
            return result;
        }

        // ===== 隊形の整い具合 =====

        /// <summary>隊形が整ったか（最大スロットズレが許容内）。</summary>
        public static bool IsFormed(float maxSlotError, float tolerance)
            => maxSlotError <= Mathf.Max(0f, tolerance);

        /// <summary>形成の進み具合(0..1)。許容内なら1（完了）、離れるほど0へ漸近する。</summary>
        public static float FormationProgress(float maxSlotError, float tolerance)
        {
            float tol = Mathf.Max(0.0001f, tolerance);
            if (maxSlotError <= tol) return 1f;
            return Mathf.Clamp01(tol / maxSlotError);
        }

        // ===== 前列交代（ローテーション）=====

        /// <summary>その隊形が時間で自動ローテーションするか（方陣＝前列の消耗を分散する）。</summary>
        public static bool AutoRotates(Formation formation) => formation == Formation.方陣;

        /// <summary>
        /// 前列交代の並び替え順（前→後で並べた隷下の新しい並び）。前列1列を末尾（後方）へ回す。
        /// 列数は隊形ごとの幅（<see cref="CorpsFormationRules.ColumnsFor"/>）に従う＝二重実装しない。
        /// </summary>
        public static int[] RotateOrder(int combatCount, Formation formation)
        {
            int n = Mathf.Max(0, combatCount);
            if (n == 0) return new int[0];
            int cols = CorpsFormationRules.ColumnsFor(formation, n);
            return CorpsFormationRules.RotateFrontToBack(n, cols);
        }

        // ===== 表示 =====

        /// <summary>適用中の軍団命令の表示文字列（軍団名・隊形・手動/自動・形成中/完了）。</summary>
        public static string StatusLabel(string corpsName, Formation formation, bool manual, bool formed)
        {
            string name = string.IsNullOrEmpty(corpsName) ? "臨時軍団" : corpsName;
            return name + "：" + formation + "（" + (manual ? "手動" : "自動") + "・" + (formed ? "完了" : "形成中") + "）";
        }
    }
}
