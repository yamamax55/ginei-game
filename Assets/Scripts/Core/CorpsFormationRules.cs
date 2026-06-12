using System.Collections.Generic;
using UnityEngine;

namespace Ginei
{
    /// <summary>軍団陣形のスロット（1艦隊ぶん）。軍団中心からのローカル座標(+Y=前方)・列・最前列か・軍団長か。</summary>
    public struct CorpsSlot
    {
        public Vector2 localPos;  // 軍団長スロットを基準に MonoBehaviour が世界座標へ変換（+Y=前方）
        public int rank;          // 列番号（0=最前列）
        public bool frontRank;    // 最前列か（方陣ローテーションの対象＝前方部隊）
        public bool commander;    // 軍団長スロットか（後方中央＝前線に出過ぎない）

        public CorpsSlot(Vector2 localPos, int rank, bool frontRank, bool commander)
        {
            this.localPos = localPos; this.rank = rank; this.frontRank = frontRank; this.commander = commander;
        }
    }

    /// <summary>
    /// 軍団（複数艦隊）の陣形ジオメトリ（純ロジック・test-first）。隷下艦隊を陣形スロットへ配置するための座標を計算する。
    /// 史実準拠の配慮：<b>軍団長は後方中央</b>（前線に出過ぎない）／<b>横陣は幅を制限</b>（広がり過ぎを防ぐため最大列数で折り返す）／
    /// <b>方陣は前方部隊を識別</b>（一定時間で前列を後方へローテーションするため）。
    /// 個艦の陣形（<see cref="Squadron"/>）とは別レイヤー＝艦隊を単位に並べる。
    /// </summary>
    public static class CorpsFormationRules
    {
        /// <summary>横陣の最大列数（幅制限＝広がり過ぎ防止）。これを超える艦隊は後列へ折り返す。</summary>
        public const int MaxLineColumns = 7;
        /// <summary>方陣の列数上限の目安。</summary>
        public const int SquareMaxColumns = 5;
        /// <summary>紡錘陣の列数（細く深い縦長）。</summary>
        public const int SpindleColumns = 3;

        /// <summary>その陣形・戦闘艦隊数に対する1列あたりの艦隊数（横陣は幅制限が効く）。</summary>
        public static int ColumnsFor(Formation formation, int combatCount)
        {
            if (combatCount <= 0) return 1;
            switch (formation)
            {
                case Formation.横陣:   return Mathf.Clamp(combatCount, 1, MaxLineColumns);
                case Formation.鶴翼陣: return Mathf.Clamp(combatCount, 1, MaxLineColumns);
                case Formation.紡錘陣: return Mathf.Clamp(combatCount, 1, SpindleColumns);
                case Formation.方陣:   return Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(combatCount)), 1, SquareMaxColumns);
                default:               return Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(combatCount)), 1, SquareMaxColumns);
            }
        }

        /// <summary>
        /// 軍団陣形のスロットを計算する（fleetCount＝軍団長を含む全艦隊数）。最後に必ず軍団長スロット（後方中央）を1つ付ける。
        /// 円陣は中心周りのリング、その他は前→後の格子。spacing は艦隊間隔（艦隊規模に合わせ大きめ）。
        /// </summary>
        public static List<CorpsSlot> ComputeSlots(int fleetCount, Formation formation, float spacing)
        {
            var slots = new List<CorpsSlot>();
            if (fleetCount <= 0) return slots;
            if (fleetCount == 1) { slots.Add(new CorpsSlot(Vector2.zero, 0, false, true)); return slots; }

            int combat = fleetCount - 1;

            if (formation == Formation.円陣)
            {
                // 円陣：戦闘艦隊を前方寄りのリングに、軍団長は後方中央（リング内ではなく後ろ）。
                float radius = Mathf.Max(spacing, spacing * combat / (2f * Mathf.PI));
                for (int i = 0; i < combat; i++)
                {
                    float ang = (i / (float)combat) * Mathf.PI * 2f + Mathf.PI / 2f; // 先頭を前方(+Y)から
                    float x = Mathf.Cos(ang) * radius;
                    float y = Mathf.Sin(ang) * radius + radius; // リング中心を前方へ寄せる
                    bool front = y >= radius; // 前半円を前列扱い
                    slots.Add(new CorpsSlot(new Vector2(x, y), front ? 0 : 1, front, false));
                }
                slots.Add(new CorpsSlot(new Vector2(0f, -spacing), 1, false, true));
                return slots;
            }

            int cols = ColumnsFor(formation, combat);
            int rows = Mathf.CeilToInt((float)combat / cols);
            int idx = 0;
            for (int r = 0; r < rows && idx < combat; r++)
            {
                int countThisRow = Mathf.Min(cols, combat - idx);
                // 前方=+Y。最前列(r=0)を最も前へ、最後尾戦闘列(r=rows-1)を y=0 に置く。
                float y = (rows - 1 - r) * spacing;
                // 鶴翼陣は前列ほど幅広・後列ほど狭い弧の意匠（横陣/方陣/紡錘陣は等幅）。
                float colSpacing = (formation == Formation.鶴翼陣) ? spacing * (1f + 0.15f * (rows - 1 - r)) : spacing;
                for (int k = 0; k < countThisRow; k++)
                {
                    float x = (k - (countThisRow - 1) / 2f) * colSpacing;
                    slots.Add(new CorpsSlot(new Vector2(x, y), r, r == 0, false));
                    idx++;
                }
            }
            // 軍団長：最後尾戦闘列(y=0)のさらに後方・中央（前線に出過ぎない）。
            slots.Add(new CorpsSlot(new Vector2(0f, -spacing), rows, false, true));
            return slots;
        }

        /// <summary>
        /// 方陣の前方部隊ローテーション順（戦闘艦隊を前→後で並べたときの新しい並び）。
        /// 前列 frontCount 隊を末尾（後方）へ回し、残りを前へ詰める＝前線部隊を後退させ次列を前へ出す。
        /// 戻り値[j] = 新しい j 番目（前→後）に入る元の艦隊インデックス。軍団長は対象外（別管理）。
        /// </summary>
        public static int[] RotateFrontToBack(int combatCount, int frontCount)
        {
            int n = Mathf.Max(0, combatCount);
            var order = new int[n];
            if (n == 0) return order;
            frontCount = Mathf.Clamp(frontCount, 0, n);
            int w = 0;
            for (int i = frontCount; i < n; i++) order[w++] = i; // 後続列を前へ
            for (int i = 0; i < frontCount; i++) order[w++] = i; // 前列を末尾(後方)へ
            return order;
        }
    }
}
