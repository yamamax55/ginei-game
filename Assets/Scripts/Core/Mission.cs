using System.Collections.Generic;

namespace Ginei
{
    /// <summary>任務の種別（ミッションコマンド #任務戦術）。最小限に絞る。</summary>
    public enum MissionType { 星系攻略, 星系防衛, 哨戒 }

    /// <summary>動員候補の戦力単位（id＝戦略艦隊id・strength＝戦闘力）。`MissionCommandRules` の選抜入力。</summary>
    public struct MissionForce
    {
        public int id;
        public int strength;
        public MissionForce(int id, int strength) { this.id = id; this.strength = strength; }
    }

    /// <summary>
    /// 任務の解決結果（ミッションコマンド＝Auftragstaktik の「上は目標を示し、参謀本部が必要兵力を見積もって動員する」）。
    /// 必要兵力の見積もり・動員した戦力・梯団（艦隊/軍団/軍集団…）・割り当てた艦隊idを保持する純データ。
    /// </summary>
    public class MissionPlan
    {
        public int targetSystemId;
        public MissionType type;
        public Faction faction;
        public float requiredStrength;   // 参謀本部の見積もり兵力（必要規模）
        public float committedStrength;   // 実際に動員できた兵力
        public EchelonType echelon;       // 動員した規模に対応する梯団（艦隊⊂軍団⊂軍集団…）
        public bool feasible;             // 動員兵力 ≥ 必要兵力（不足＝過小動員のまま発動＝リスク）
        public readonly List<int> fleetIds = new List<int>(); // 動員した戦略艦隊id
    }
}
