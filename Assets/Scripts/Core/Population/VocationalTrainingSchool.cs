namespace Ginei
{
    /// <summary>
    /// 職業訓練 institution の種類（SKILL-4・#2034・POP 大衆の職業技能を育てる受け皿）。
    /// 既存の教育チェーン（小〜大学/士官学校/科挙#155-157＝ネームド供給）とは別系統＝大衆向け職業技能。<b>宇宙設定固有</b>を含む。
    /// </summary>
    public enum TrainingInstitutionType
    {
        公共職業訓練校,       // ポリテク＝失業者/若年へ実務技能
        職業能力開発校,       // 中堅技能の底上げ
        企業内訓練,           // 企業#1022 が自社工員を育てる
        徒弟見習い,           // 現場での技能伝承
        軍技能訓練,           // 軍属#96 の保安・整備・運転技能
        航宙士養成所,         // 宇宙設定固有＝宇宙船操縦士#622
        テラフォーミング訓練所, // 宇宙設定固有＝テラフォ技師#092
        軌道作業訓練           // 宇宙設定固有＝採掘#692・荷役#702
    }

    /// <summary>
    /// 職業訓練校（SKILL-4・#2034・純データ）。教育チェーン（`HighSchool`/`University`等）と同じ流儀で、対応する JSOC小分類へ技能を供給。
    /// 定員×質×期間で輩出。勢力ごとに保有。POP のシミュ状態でなく institution の定義（lookup＋輩出ロジックは <see cref="VocationalTrainingRules"/>）。
    /// </summary>
    [System.Serializable]
    public class VocationalTrainingSchool
    {
        public int schoolId;
        public TrainingInstitutionType type;
        public Faction faction;
        public int capacity;            // 定員（年間の受入枠）
        public float quality;           // 教育の質（0..1）
        public string targetMinorCode;  // 供給する JSOC小分類（JsocMinorClassification）

        public VocationalTrainingSchool() { }

        public VocationalTrainingSchool(int schoolId, TrainingInstitutionType type, Faction faction, int capacity, float quality, string targetMinorCode)
        {
            this.schoolId = schoolId;
            this.type = type;
            this.faction = faction;
            this.capacity = capacity;
            this.quality = quality;
            this.targetMinorCode = targetMinorCode;
        }
    }
}
