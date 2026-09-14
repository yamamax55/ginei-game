using System.Collections.Generic;

namespace Ginei
{
    public partial class GalaxyView
    {
        // ===== 選挙の Game 接続を検証する入口（PlayMode 試験用・非保存） =====
        // 盤面を組まない（Start を走らせない無効な GameObject 上で使う）。本番と同じ private 経路をそのまま呼ぶだけ＝別ロジックを持たない。
        // セーブ・シーン・設定には触れない。

        /// <summary>試験用：選挙の配線が読む盤面（地図・内政・人物名簿）を差し込む。</summary>
        public void BindElectionQaWorld(GalaxyMap qaMap, IDictionary<int, Province> qaProvinces,
                                        List<Person> qaCommanders, List<Person> qaCivilians)
        {
            map = qaMap;
            provinces.Clear();
            if (qaProvinces != null)
                foreach (KeyValuePair<int, Province> kv in qaProvinces) provinces[kv.Key] = kv.Value;
            commanders = qaCommanders ?? new List<Person>();
            civilians = qaCivilians ?? new List<Person>();
        }

        /// <summary>試験用：開幕・読込時の政府シード（要職・省庁・保存済みの首相/知事の復元）。</summary>
        public void SeedGovernmentForQa() => SeedGovernment();

        /// <summary>試験用：年次の政党政治と選挙。</summary>
        public void RunPoliticsTickForQa() => RunPoliticsTick();

        /// <summary>試験用：年次の宰相銓衡（民主政では選出首相の維持）。</summary>
        public void RunCivilAppointmentTickForQa() => RunCivilAppointmentTick();

        /// <summary>試験用：年次の総督銓衡（民主政では選出知事の在任整理）。</summary>
        public void RunGovernorAppointmentTickForQa() => RunGovernorAppointmentTick();

        /// <summary>
        /// 試験用：観測層が読む <see cref="Active"/> を差し替え、直前の値を返す（本番は Start が設定する）。
        /// Start/OnDestroy を走らせない無効な GameObject では自動で解除されないため、呼び出し側が返り値で必ず戻す。
        /// </summary>
        public static GalaxyView SwapActiveForQa(GalaxyView view)
        {
            GalaxyView previous = Active;
            Active = view;
            return previous;
        }

        /// <summary>試験用：選挙の暦年（統一クロックの宇宙暦）。</summary>
        public int ElectionYearForQa => ElectionYear();
    }
}
