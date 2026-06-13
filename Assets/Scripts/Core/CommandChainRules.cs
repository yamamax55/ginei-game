using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 指揮系統の別（ゴールドウォーター゠ニコルズ法の二系統分離・MILGOV-US §3-A）。
    /// <b>作戦</b>＝部隊を動かして戦争を遂行する（大統領→国防長官→統合軍司令官＝<see cref="OrderOfBattle"/> の梯団司令）。
    /// <b>管理</b>＝organize-train-equip（各軍長官→参謀総長＝兵を集め・訓練し・装備するが戦争では指揮しない＝<see cref="Office"/> 軍事所掌役職）。
    /// 要点＝戦力を「育てる者」と「使う者」を別人にし、一人に「兵・銭・命令」を集中させない＝クーデターの母体を分断する。
    /// </summary>
    public enum CommandChain { 作戦, 管理 }

    /// <summary>
    /// 指揮の二系統分離（ゴールドウォーター゠ニコルズ法・MILGOV-US §3-A・純ロジック・test-first・唯一の窓口）。
    /// 既存の作戦系統（<see cref="OrderOfBattle"/> の梯団ツリー）と管理系統（<see cref="Office"/> の軍事所掌役職）から
    /// <b>指揮の集中度／分断度</b>を導く＝「兵を育てる者≠使う者」を表現し、§3-E のクーデターリスク駆動因
    /// （指揮分断度）へ供給する。数値ロジックは持つが状態は変えない（read-only）。係数は #106 パイプライン想定。
    /// 識別子は <c>int</c>（提督=<see cref="AdmiralData"/> と文官=<see cref="ICharacter"/> で型が異なるため型非依存）。
    /// </summary>
    public static class CommandChainRules
    {
        /// <summary>頂点が空席であることを表す識別子（実在の保持者 id とは衝突しない番兵）。</summary>
        public const int Vacant = int.MinValue;

        // 集中度の重み（作戦＋管理＋予算＝1.0）。作戦指揮が最も危険、次に軍政、予算統制。
        public const float OperationalWeight = 0.45f;
        public const float AdministrativeWeight = 0.35f;
        public const float BudgetWeight = 0.20f;

        // 分断度の配点（両頂点が別人＝0.6・予算が両頂点から独立＝0.4＝完全分離で1.0）。
        public const float ApexSplitShare = 0.6f;
        public const float BudgetIndependentShare = 0.4f;

        /// <summary>
        /// その役職がどちらの指揮系統か。軍事所掌は <see cref="Office.commandChain"/>、非軍事所掌は作戦指揮を含意しないので管理を返す。
        /// </summary>
        public static CommandChain ChainOf(Office o)
        {
            if (o == null) return CommandChain.管理;
            return o.domain == OfficeDomain.軍事 ? o.commandChain : CommandChain.管理;
        }

        // ===== 構造の頂点（既存資産から抽出） =====

        /// <summary>作戦系統の頂点＝最上位 echelon の梯団（同段が複数なら最初）。空集合は null。</summary>
        public static MilitaryFormation OperationalApexFormation(IEnumerable<MilitaryFormation> formations)
        {
            MilitaryFormation best = null;
            if (formations == null) return null;
            foreach (var f in formations)
            {
                if (f == null) continue;
                if (best == null || (int)f.echelon > (int)best.echelon) best = f;
            }
            return best;
        }

        /// <summary><see cref="OrderOfBattle"/> から勢力の作戦系統頂点梯団を取る。</summary>
        public static MilitaryFormation OperationalApexFormation(Faction faction)
            => OperationalApexFormation(OrderOfBattle.AllFormations(faction));

        /// <summary>
        /// 管理系統の頂点役職＝国家スコープの軍事所掌役職のうち最高 <see cref="Office.requiredTier"/>（同点なら最初）。
        /// 参謀総長級（軍政の頂点）に相当する。該当なしは null。
        /// </summary>
        public static Office AdministrativeApexOffice(IEnumerable<Office> offices)
        {
            Office best = null;
            if (offices == null) return null;
            foreach (var o in offices)
            {
                if (o == null || o.domain != OfficeDomain.軍事 || o.scope != OfficeScope.国家) continue;
                if (best == null || o.requiredTier > best.requiredTier) best = o;
            }
            return best;
        }

        // ===== 集中／分断 =====

        /// <summary>
        /// 一人の指揮官が握る権限の束から指揮集中度（0..1）。作戦頂点 <see cref="OperationalWeight"/>＋
        /// 管理頂点 <see cref="AdministrativeWeight"/>＋軍事予算 <see cref="BudgetWeight"/> の重み付き。3つ全部＝1.0。
        /// </summary>
        public static float Concentration(bool operationalApex, bool administrativeApex, bool budgetAuthority)
        {
            float c = 0f;
            if (operationalApex) c += OperationalWeight;
            if (administrativeApex) c += AdministrativeWeight;
            if (budgetAuthority) c += BudgetWeight;
            return Mathf.Clamp01(c);
        }

        /// <summary>一人が作戦・管理の両頂点を兼ねるか（＝ゴールドウォーター゠ニコルズ違反＝集中＝危険）。</summary>
        public static bool ConcentratesCommand(bool operationalApex, bool administrativeApex)
            => operationalApex && administrativeApex;

        /// <summary>
        /// 二系統が別人に割れているか（GN準拠）。両頂点が埋まり（≠<see cref="Vacant"/>）かつ別人のときだけ true。
        /// 空席は「分断」でなく統制不在なので false（強度の低下は別途）。
        /// </summary>
        public static bool IsUnifiedCommandSeparated(int operationalApexHolderId, int administrativeApexHolderId)
            => operationalApexHolderId != Vacant
               && administrativeApexHolderId != Vacant
               && operationalApexHolderId != administrativeApexHolderId;

        /// <summary>
        /// 指揮分断度（0..1）＝§3-E のクーデターリスク駆動因（指揮二系統の分断＝<c>commandSeparation</c>）。
        /// 両頂点が別人＝<see cref="ApexSplitShare"/>＋予算が両頂点から独立（文民/議会が握る＝power of the purse）＝
        /// <see cref="BudgetIndependentShare"/>。完全分離で1.0・一人に集中で0。空席は分断に数えない（統制不在）。
        /// </summary>
        public static float CommandSeparation(int operationalApexHolderId, int administrativeApexHolderId, int budgetHolderId)
        {
            float s = 0f;
            if (IsUnifiedCommandSeparated(operationalApexHolderId, administrativeApexHolderId))
                s += ApexSplitShare;
            bool budgetIndependent = budgetHolderId != Vacant
                                     && budgetHolderId != operationalApexHolderId
                                     && budgetHolderId != administrativeApexHolderId;
            if (budgetIndependent) s += BudgetIndependentShare;
            return Mathf.Clamp01(s);
        }
    }
}
