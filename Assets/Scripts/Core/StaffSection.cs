namespace Ginei
{
    /// <summary>
    /// 幕僚（参謀）の機能区分（米軍 continental staff の G/J-1〜6 を参考）。各セクションは別々の役割を担う。
    /// 人事(G1)/情報(G2)/作戦(G3)/兵站(G4)/計画(G5)/通信(G6)。`StaffRules.RelevantStat` で重視能力に写す。
    /// </summary>
    public enum StaffSection
    {
        人事,  // G1 Personnel：補充・士気行政・将兵管理
        情報,  // G2 Intelligence：敵情・索敵・諜報
        作戦,  // G3 Operations：作戦の計画と実行・指揮
        兵站,  // G4 Logistics：補給・輸送・整備・継戦
        計画,  // G5 Plans：長期計画・戦略立案
        通信,  // G6 Signal：通信・指揮統制(C2)・連携
    }

    /// <summary>
    /// 参謀のレベル（史実準拠で二分）。部隊参謀＝艦隊長〜軍団長に付く幕僚（G-staff相当）。
    /// 大本営参謀本部＝勢力の最高統帥幕僚（J-staff相当・戦略レベル）。
    /// </summary>
    public enum StaffLevel
    {
        部隊参謀,        // 各部隊指揮官（艦隊長〜軍団長）に付く幕僚
        大本営参謀本部,  // 勢力の最高司令部（戦争計画・戦略諜報・国家動員）
    }

    /// <summary>幕僚セクションが重視する能力（AdmiralData/Person の統率/運営/情報に対応）。</summary>
    public enum StaffStat { 統率, 運営, 情報 }
}
