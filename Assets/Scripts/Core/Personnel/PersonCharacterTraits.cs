namespace Ginei
{
    /// <summary>
    /// 個人の政治信条（PER-PROPS A2・creed）。所属勢力のイデオロギーとのズレが離反・クーデターの火種になる
    /// （解決は <see cref="CreedRules"/>）。既定＝無関心＝政治的緊張を生まない（後方互換）。
    /// </summary>
    public enum Creed
    {
        無関心,   // 政治に無関心（既定・緊張なし）
        帝政擁護, // 君主制・正統を支持
        共和主義, // 民主・共和制を信奉（ヤン型）
        門閥主義, // 血統・貴族秩序を是とする
        能力主義, // 実力本位・反門閥
        中道,     // 漸進・現実主義
    }

    /// <summary>
    /// 社会階層・出自（PER-PROPS A4・socialOrigin）。登用プール・忠誠・政治派閥に影響する
    /// （解決は <see cref="SocialOriginRules"/>）。既定＝平民＝後方互換。
    /// </summary>
    public enum SocialOrigin
    {
        平民,     // 市民（既定）
        門閥貴族, // 名門貴族（碧晶公国型）
        下級貴族, // 没落・地方貴族
        植民星,   // 辺境植民星の出
        軍人家系, // 代々の武門
    }

    /// <summary>趣味（PER-PROPS C8・hobby・人物の彩り）。イベント文・会話の種。既定＝なし＝後方互換。</summary>
    public enum Hobby { なし, 読書, 茶, 音楽, 棋譜, 武芸, 蒐集, 美食, 歴史, 園芸 }

    /// <summary>悪癖（PER-PROPS C8・vice・突発事件の種）。大酒・賭博などは将来 ViceRules でリスク化しうる。既定＝なし。</summary>
    public enum Vice { なし, 大酒, 賭博, 吝嗇, 好色, 傲慢, 短気 }
}
