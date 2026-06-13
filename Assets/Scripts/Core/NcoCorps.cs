namespace Ginei
{
    /// <summary>
    /// 下士官団（NCO corps）＝部隊の「背骨」の集約質指標（#210 兵・下士官・将校の三層の下士官層）。
    /// 個々の兵を管理せず、部隊の<b>下士官の厚み（density）と質（quality）の2スカラー</b>に集約する（タイクン化/終盤ラグ回避）。
    /// 強い下士官団は練度（命中/回避）・結束（士気の粘り <see cref="FleetMorale"/>）・自律（命令なしで動ける＝任務戦術#147/通信断#206）の背骨。
    /// 解決は <see cref="NcoEducationRules"/> が唯一の窓口（米軍 NCOPDS の PME ラダー＋STEP＋“経験は急造できない”）。純データ。
    /// </summary>
    [System.Serializable]
    public class NcoCorps
    {
        /// <summary>下士官の厚み 0..1（理想比に対する充足＝<see cref="NcoEducationRules.Thickness"/> で算出）。</summary>
        public float density = 0.5f;

        /// <summary>下士官団の質 0..1（PME 到達段＋経験。叩き上げの institutional experience）。</summary>
        public float quality = 0.5f;

        public NcoCorps() { }

        public NcoCorps(float density, float quality)
        {
            this.density = density;
            this.quality = quality;
        }
    }
}
