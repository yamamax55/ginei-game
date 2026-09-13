using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 固定会戦QAの3プリセット（退却／不退転／陣形変更）を<b>明示の数値</b>で組み立てる窓口（純ロジック）。
    ///
    /// ★ここで決めるのは<b>初期条件と命令の段取り</b>だけ。結果（撤退した・敗走しなかった等）は書かない。
    /// ★製品の調整値・計算式には触れない（撤退しきい値などは既存の Core 定数を<b>読むだけ</b>）。
    /// </summary>
    public static class BattleQaPresetCatalog
    {
        /// <summary>既定 seed（メニューの初期値）。</summary>
        public const int DefaultSeed = 20260913;

        /// <summary>seed 区別の確認用に切り替える別値。</summary>
        public const int AlternateSeed = 7;

        /// <summary>開始後の時間倍率（等速）。</summary>
        public const float DefaultTimeScale = 1f;

        // ── 退却 ──

        /// <summary>
        /// 退却プリセットの軍団艦艇数比（現在/定数）。
        /// 軍団総退却のしきい値（統率50・功名心50＝<see cref="CorpsRetreatRules.DefaultRetreatStrengthRatio"/>）は<b>下回り</b>、
        /// 個艦の自主撤退比（FleetAI.retreatRatio 既定 0.3）は<b>上回る</b>値＝撤退の原因を軍団総退却に切り分ける。
        /// </summary>
        public const float RetreatCorpsShipRatio = 0.32f;

        /// <summary>退却プリセットの定数（最大艦艇数）。</summary>
        public const int RetreatMaxShips = 10000;

        /// <summary>退却プリセットで敵を置く距離（初期位置で射程外）。</summary>
        public const float RetreatEnemyDistance = 50f;

        // ── 不退転 ──

        /// <summary>不退転プリセットの艦艇数（効果中に失われない大きさ）。</summary>
        public const int MoraleLockShips = 100000;

        /// <summary>
        /// 不退転プリセットの初期士気（効果中に<b>実被弾で</b>下限へ届く初期条件）。
        /// 根拠（fix1・a2 実測）：初期30では効果8秒間の正味低下が 21=18.4／22=13.8 で下限に届かなかった。
        /// 遅い方（22 の約1.7/秒）でも 8→1（正味7）は約4秒＝効果終了前に届く余裕を取る。
        /// 0 より大きい＝発令前に敗走していない（下限へは被弾でしか届かない）。製品の効果時間・被弾・士気式には触れない。
        /// </summary>
        public const float MoraleLockInitialMorale = 8f;

        /// <summary>撃ち手と標的の距離（射程 10 の内側）。</summary>
        public const float MoraleLockShooterDistance = 8f;

        /// <summary>被弾継続組と被弾停止組の横間隔（射程外で干渉しない）。</summary>
        public const float MoraleLockLaneSeparation = 40f;

        // ── 陣形変更 ──

        /// <summary>陣形プリセットの隷下1隊の艦艇数。</summary>
        public const int FormationFleetShips = 10000;

        /// <summary>陣形プリセットの敵艦艇数（軍団AIが劣勢＝方陣を推奨する規模）。</summary>
        public const int FormationEnemyShips = 200000;

        /// <summary>陣形プリセットで敵を置く距離（交戦しない）。</summary>
        public const float FormationEnemyDistance = 90f;

        /// <summary>初期士気（既定の最大士気）。</summary>
        public const float DefaultMorale = 100f;

        /// <summary>軍団長の統率・功名心（撤退しきい値を既定どおりにする中立値）。</summary>
        public const int NeutralStat = 50;

        public const string RetreatCorps = "QA退却軍団";
        public const string OtherCorps = "QA別軍団";
        public const string FormationCorps = "QA陣形軍団";

        /// <summary>種別からプリセットを作る。</summary>
        public static BattleQaPreset Create(BattleQaPresetKind kind, int seed)
        {
            switch (kind)
            {
                case BattleQaPresetKind.退却: return Retreat(seed);
                case BattleQaPresetKind.不退転: return MoraleLock(seed);
                default: return FormationChange(seed);
            }
        }

        /// <summary>
        /// 退却：同一軍団3隊（軍団長1＋隷下2）が損耗済み／別軍団2隊と独立1隊は無傷／敵1隊。
        /// 命令＝QAからは出さない（軍団長AIの総退却判断と、その後の実移動を観測する）。
        /// </summary>
        public static BattleQaPreset Retreat(int seed)
        {
            int damaged = Mathf.RoundToInt(RetreatMaxShips * RetreatCorpsShipRatio);
            var f = new List<BattleQaFleetSpec>
            {
                new BattleQaFleetSpec(1, "退却軍団長", Faction.同盟, RetreatCorps, BattleQaCommandRole.軍団長,
                    damaged, RetreatMaxShips, "QA退却軍団長", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(0f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(2, "退却隷下A", Faction.同盟, RetreatCorps, BattleQaCommandRole.隷下,
                    damaged, RetreatMaxShips, "QA退却隷下A", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(-8f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(3, "退却隷下B", Faction.同盟, RetreatCorps, BattleQaCommandRole.隷下,
                    damaged, RetreatMaxShips, "QA退却隷下B", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(8f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(4, "独立艦隊", Faction.同盟, "", BattleQaCommandRole.独立,
                    RetreatMaxShips, RetreatMaxShips, "QA独立", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(-30f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(5, "別軍団長", Faction.同盟, OtherCorps, BattleQaCommandRole.軍団長,
                    RetreatMaxShips, RetreatMaxShips, "QA別軍団長", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(30f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(6, "別軍団隷下", Faction.同盟, OtherCorps, BattleQaCommandRole.隷下,
                    RetreatMaxShips, RetreatMaxShips, "QA別軍団隷下", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(38f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(11, "敵艦隊", Faction.帝国, "", BattleQaCommandRole.敵,
                    RetreatMaxShips, RetreatMaxShips, "QA敵", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(0f, RetreatEnemyDistance), 180f, Formation.紡錘陣, true),
            };
            return new BattleQaPreset(BattleQaPresetKind.退却, "退却", seed, DefaultTimeScale, true,
                "QA命令なし：軍団長AI（BattlefieldCommandManager）の総退却判断→全隷下の撤退状態と退却方向への実変位を観測。" +
                "所属外（独立・別軍団）が巻き込まれないことも観測", f);
        }

        /// <summary>
        /// 不退転：味方2隊に同じ不退転を発令し、それぞれ背後の敵1隊が撃つ。
        /// ★標的は撃ち手に<b>背を向けて置く</b>（撃ち返さない＝撃ち手が被弾で敗走して被弾が途切れる、を初期条件で防ぐ）。
        /// 被弾継続組（21←31）は撃たせ続け、被弾停止組（22←32）は効果終了で撃ち手を射撃管制（ROE）へ切り替える。
        /// 全艦 AI 停止＝AI の特殊指揮が混ざらない。
        /// </summary>
        public static BattleQaPreset MoraleLock(int seed)
        {
            float half = MoraleLockLaneSeparation * 0.5f;
            var f = new List<BattleQaFleetSpec>
            {
                new BattleQaFleetSpec(21, "不退転・被弾継続", Faction.同盟, "", BattleQaCommandRole.独立,
                    MoraleLockShips, MoraleLockShips, "QA不退転継続", NeutralStat, NeutralStat, MoraleLockInitialMorale,
                    new Vector2(-half, 0f), 180f, Formation.紡錘陣, false),
                new BattleQaFleetSpec(22, "不退転・被弾停止", Faction.同盟, "", BattleQaCommandRole.独立,
                    MoraleLockShips, MoraleLockShips, "QA不退転停止", NeutralStat, NeutralStat, MoraleLockInitialMorale,
                    new Vector2(half, 0f), 180f, Formation.紡錘陣, false),
                new BattleQaFleetSpec(31, "撃ち手・継続", Faction.帝国, "", BattleQaCommandRole.敵,
                    MoraleLockShips, MoraleLockShips, "QA撃ち手継続", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(-half, MoraleLockShooterDistance), 180f, Formation.紡錘陣, false),
                new BattleQaFleetSpec(32, "撃ち手・停止", Faction.帝国, "", BattleQaCommandRole.敵,
                    MoraleLockShips, MoraleLockShips, "QA撃ち手停止", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(half, MoraleLockShooterDistance), 180f, Formation.紡錘陣, false),
            };
            return new BattleQaPreset(BattleQaPresetKind.不退転, "不退転", seed, DefaultTimeScale, false,
                "開始時に21・22へ不退転を発令（ActiveCommandState.Issue）→効果中は被弾させる→効果終了後、" +
                "32の交戦規定を射撃管制へ（被弾停止）、31は撃ち続ける（被弾継続）→終了後の被弾・敗走を観測", f);
        }

        /// <summary>
        /// 陣形変更：同一軍団3隊（軍団長1＋隷下2）＋遠方の敵1隊（AI停止・交戦しない）。
        /// 隷下A（2）へ直接命令で円陣を保持させ、軍団AIの発令を跨いで保持を観測→軍団AIの指定が拒否されるか確認→保持解除→軍団AIへ戻るか観測。
        /// </summary>
        public static BattleQaPreset FormationChange(int seed)
        {
            var f = new List<BattleQaFleetSpec>
            {
                new BattleQaFleetSpec(1, "陣形軍団長", Faction.同盟, FormationCorps, BattleQaCommandRole.軍団長,
                    FormationFleetShips, FormationFleetShips, "QA陣形軍団長", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(0f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(2, "陣形隷下A（保持）", Faction.同盟, FormationCorps, BattleQaCommandRole.隷下,
                    FormationFleetShips, FormationFleetShips, "QA陣形隷下A", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(-10f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(3, "陣形隷下B（対照）", Faction.同盟, FormationCorps, BattleQaCommandRole.隷下,
                    FormationFleetShips, FormationFleetShips, "QA陣形隷下B", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(10f, 0f), 0f, Formation.紡錘陣, true),
                new BattleQaFleetSpec(11, "遠方の敵", Faction.帝国, "", BattleQaCommandRole.敵,
                    FormationEnemyShips, FormationEnemyShips, "QA遠方敵", NeutralStat, NeutralStat, DefaultMorale,
                    new Vector2(0f, FormationEnemyDistance), 180f, Formation.紡錘陣, false),
            };
            return new BattleQaPreset(BattleQaPresetKind.陣形変更, "陣形変更", seed, DefaultTimeScale, true,
                "開始時に隷下A（2）へ直接命令で円陣（保持）→軍団AIの周期を跨いで観測→軍団AI名義の指定を当てて優先順位を確認→" +
                "保持解除→軍団AIの陣形へ戻るか観測。費用・失敗理由UI（仕様2）は扱わない", f);
        }
    }
}
