namespace Ginei
{
    /// <summary>
    /// 役職保持者の共通アクセス（政府役職システム GOV-1 #142）。軍人/文民どちらの人物も役職に就ける
    /// よう、任命・資格判定が依存する最小の共通窓口を切る。`Person`（軍人/文民）が実装し、将来 `AdmiralData`
    /// も非破壊で実装しうる。役職資格（軍人専用/文民専用/政治任用）は <see cref="OfficeRules"/> がこの窓口を読む。
    /// </summary>
    public interface ICharacter
    {
        /// <summary>一意ID。</summary>
        int Id { get; }

        /// <summary>表示名。</summary>
        string CharacterName { get; }

        /// <summary>所属勢力。</summary>
        Faction Faction { get; }

        /// <summary>階級 tier（#14・序列。無ければ0）。役職の必要階級判定に使う。</summary>
        int RankTier { get; }

        /// <summary>軍人か（true=軍人／false=文民）。軍人専用/文民専用役職の資格判定に使う。</summary>
        bool IsMilitary { get; }

        /// <summary>政治家か（政党・選挙で出世＝政治任用役職の資格・GOV-6 #159）。</summary>
        bool IsPolitician { get; }

        // --- 人物ライフサイクル（LIFE-1/2 #151/#152） ---

        /// <summary>生年（一次データ。年齢は <c>currentYear - BirthYear</c> で導出＝暦とズレない）。0＝未設定。</summary>
        int BirthYear { get; }

        /// <summary>死亡したか（LIFE-2 #152。死亡で各レジストリから保持解除される）。</summary>
        bool IsDeceased { get; }

        /// <summary>任に就ける状態か（生存かつ自由＝捕虜/死亡でない。LIFE-2/4。空席判定・後任補充の窓口）。</summary>
        bool IsAvailable { get; }
    }
}
