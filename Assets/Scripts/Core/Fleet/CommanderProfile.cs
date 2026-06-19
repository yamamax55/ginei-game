namespace Ginei
{
    /// <summary>
    /// 軍自動編成（<see cref="ArmyOrganizationRules"/>）に渡す<b>指揮官候補の軽量プロファイル</b>（純データ・読み取り専用 struct）。
    /// id ＋階級 tier ＋6能力（<see cref="AdmiralData"/> の実効値を写し取る想定＝統率/攻撃/防御/機動/運営/情報）を持ち、
    /// 役割への<b>適性スコア</b>を派生で計算する。指揮可能規模の判定は <see cref="CommandCapacityRules"/> へ委譲（rankTier を渡す）。
    /// <see cref="CommanderSlot"/>（id＋tier だけ）の上位互換＝こちらは能力で「向き不向き」を測れる。
    /// </summary>
    public readonly struct CommanderProfile
    {
        /// <summary>人物 id（ロスター解決の鍵・-1=無効）。</summary>
        public readonly int id;
        /// <summary>階級 tier（<see cref="RankSystem"/> 既定ラダー・大きいほど上位＝指揮可能規模が広い）。</summary>
        public readonly int rankTier;
        /// <summary>統率。</summary>
        public readonly int leadership;
        /// <summary>攻撃。</summary>
        public readonly int attack;
        /// <summary>防御。</summary>
        public readonly int defense;
        /// <summary>機動。</summary>
        public readonly int mobility;
        /// <summary>運営（兵站・編制）。</summary>
        public readonly int operation;
        /// <summary>情報（索敵・諜報）。</summary>
        public readonly int intelligence;

        public CommanderProfile(int id, int rankTier,
            int leadership, int attack, int defense, int mobility, int operation, int intelligence)
        {
            this.id = id;
            this.rankTier = rankTier;
            this.leadership = leadership;
            this.attack = attack;
            this.defense = defense;
            this.mobility = mobility;
            this.operation = operation;
            this.intelligence = intelligence;
        }

        /// <summary>戦闘艦適性＝統率・攻撃・防御の平均（前線で殴り合う将に効く三能力）。</summary>
        public float CombatFitness => (leadership + attack + defense) / 3f;

        /// <summary>偵察適性＝情報・機動の平均（先んじて探り素早く動く将）。</summary>
        public float ReconFitness => (intelligence + mobility) / 2f;

        /// <summary>兵站適性＝運営（輸送・補給を捌く将）。</summary>
        public float LogisticsFitness => operation;

        /// <summary>役割に対する適性スコア（戦闘艦→戦闘／偵察艦→偵察／輸送艦・入植艦→兵站）。</summary>
        public float FitnessFor(ShipRole role)
        {
            switch (role)
            {
                case ShipRole.偵察艦: return ReconFitness;
                case ShipRole.輸送艦:
                case ShipRole.入植艦: return LogisticsFitness;
                default: return CombatFitness; // 戦闘艦
            }
        }
    }
}
