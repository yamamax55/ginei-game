using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    /// <summary>
    /// 陣形の種類定義。すべて「旗艦＝中心(原点)、配下艦は左右対称、旗艦の向き(Transform.up=前方)に追従」。
    /// </summary>
    public enum Formation
    {
        紡錘陣, // Spindle（デフォルト）：前方にやや尖った縦長のレンズ形
        鶴翼陣, // Crescent：前方に開いた幅広の弧
        円陣,   // Circle：同心円リング
        横陣,   // Line abreast：左右一直線（1〜2段）
        方陣    // Square：格子状
    }

    /// <summary>
    /// 旗艦（自身）を中心に配下艦を陣形通りに配置・追従させるクラス。
    /// Start で旗艦のスプライトを流用して escortCount 隻の配下艦を生成する（手置きの子があればそれも含める）。
    /// </summary>
    public class Squadron : MonoBehaviour
    {
        [Header("陣形設定")]
        public Formation currentFormation = Formation.紡錘陣;

        [Tooltip("艦同士の間隔")]
        public float spacing = 1.2f;

        [Tooltip("追従の滑らかさ（秒）")]
        public float smoothTime = 0.3f;

        [Tooltip("交戦中の追従の滑らかさ（秒）。smoothTime より大きくして戦闘中はゆっくり動かす（穴埋め移動も緩やかに）")]
        public float combatSmoothTime = 1.2f;

        [Header("配下艦")]
        [Tooltip("部隊の配下艦数（旗艦中心に展開）")]
        public int escortCount = 50;

        [Tooltip("配下艦1隻あたりの艦艇数（合計兵力が過大にならないよう調整。旗艦は別途多め）")]
        public int escortShipCount = 200;

        [Tooltip("配下艦のスケール倍率（旗艦との見分け用。やや小さくする。旗艦rootのスケールは変えない）")]
        public float memberScale = 0.8f;

        [Tooltip("配下艦リスト（手置きがあれば使用、無ければ自動生成）")]
        public List<Transform> memberShips = new List<Transform>();

        // SmoothDamp用の速度バッファ（memberShips と添字同期）
        private List<Vector2> velocities = new List<Vector2>();

        // 交戦中判定に使う旗艦の武装（Start でキャッシュ）
        private FleetWeapon flagshipWeapon;

        // 陣形スロットのキャッシュ（隻数・陣形が変わった時だけ再計算）
        private List<Vector2> cachedSlots = new List<Vector2>();
        private Formation cachedFormation;
        private int cachedCount = -1;

        private void Awake()
        {
            // Squadron を持つ＝旗艦。識別マーカーを必ず付ける（未付与なら自動追加。プレハブ編集不要）。
            if (GetComponent<FlagshipMarker>() == null)
            {
                gameObject.AddComponent<FlagshipMarker>();
            }
        }

        private void Start()
        {
            CollectExistingMembers();  // 手置きの子を memberShips に取り込む
            ApplyMemberScale();        // 手置き分を縮小（生成分は生成時に縮小済み）
            GenerateEscorts();         // escortCount まで旗艦スプライトを流用して補う
            SetupEscorts();            // 各配下艦に EscortShip・艦艇数・陣営を設定
            InitVelocities();
            RecolorFleet();            // 生成した配下艦にも陣営色を反映
            flagshipWeapon = GetComponent<FleetWeapon>();
        }

        /// <summary>
        /// memberShips が空なら、艦以外の特殊な子を除いて配下艦として取り込む。
        /// </summary>
        private void CollectExistingMembers()
        {
            if (memberShips.Count > 0) return;
            foreach (Transform child in transform)
            {
                if (child.name == "StrengthDisplay" || child.name == "SelectionRing"
                    || child.name == "WeaponArcLine" || child.name == "MoraleLabel"
                    || child.name == "FlagshipMarker") continue;
                memberShips.Add(child);
            }
        }

        /// <summary>
        /// 手置き配下艦のローカルスケールに memberScale を掛けて、旗艦より一回り小さく見せる。
        /// </summary>
        private void ApplyMemberScale()
        {
            if (Mathf.Approximately(memberScale, 1f)) return;
            foreach (var ship in memberShips)
            {
                if (ship != null) ship.localScale *= memberScale;
            }
        }

        /// <summary>
        /// escortCount に達するまで、旗艦のスプライトを流用して配下艦を生成する。
        /// </summary>
        private void GenerateEscorts()
        {
            int toAdd = escortCount - memberShips.Count;
            if (toAdd <= 0) return;

            SpriteRenderer template = FindShipSpriteTemplate();
            if (template == null) return; // 流用元が無ければ生成しない

            for (int i = 0; i < toAdd; i++)
            {
                memberShips.Add(CreateEscort(template, i));
            }
        }

        /// <summary>
        /// 配下艦スプライトの流用元を探す。手置き配下艦＞旗艦本体スプライトの順。
        /// </summary>
        private SpriteRenderer FindShipSpriteTemplate()
        {
            foreach (var m in memberShips)
            {
                if (m == null) continue;
                var sr = m.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null) return sr;
            }
            // 旗艦本体（特殊な子を除く最初の SpriteRenderer）
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in all)
            {
                string n = sr.gameObject.name;
                if (n == "SelectionRing" || n == "FlagshipMarker") continue;
                if (sr.sprite != null) return sr;
            }
            return null;
        }

        /// <summary>
        /// テンプレートのスプライトを使って配下艦オブジェクトを1隻生成する。
        /// </summary>
        private Transform CreateEscort(SpriteRenderer template, int index)
        {
            GameObject go = new GameObject($"Escort_{index}");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            // 旗艦 root スケールは1の前提。配下艦は memberScale で一回り小さく。
            go.transform.localScale = Vector3.one * memberScale;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = template.sprite;
            sr.sortingLayerID = template.sortingLayerID;
            sr.sortingOrder = template.sortingOrder;
            sr.color = template.color; // 陣営色は後段の RecolorFleet で再適用

            return go.transform;
        }

        /// <summary>
        /// 各配下艦に EscortShip を付与・初期化し、艦艇数を設定する。
        /// </summary>
        private void SetupEscorts()
        {
            FleetStrength flagship = GetComponent<FleetStrength>();
            foreach (var ship in memberShips)
            {
                if (ship == null) continue;
                EscortShip escort = ship.GetComponent<EscortShip>();
                if (escort == null) escort = ship.gameObject.AddComponent<EscortShip>();
                escort.shipCount = escortShipCount;
                escort.Setup(this, flagship);
            }
        }

        private void InitVelocities()
        {
            velocities.Clear();
            for (int i = 0; i < memberShips.Count; i++) velocities.Add(Vector2.zero);
        }

        private void RecolorFleet()
        {
            FactionColor color = GetComponent<FactionColor>();
            if (color != null) color.ApplyColors();
        }

        /// <summary>
        /// 現在の陣形スロット（旗艦中心のローカル座標群）のコピーを返す（FormationPreview 用）。
        /// </summary>
        public List<Vector2> GetFormationSlots()
        {
            EnsureSlots();
            return new List<Vector2>(cachedSlots);
        }

        /// <summary>配下艦スプライトの流用元 Sprite（プレビュー用）。無ければ null。</summary>
        public Sprite GetShipSprite()
        {
            SpriteRenderer sr = FindShipSpriteTemplate();
            return sr != null ? sr.sprite : null;
        }

        /// <summary>艦スプライトの現在色（陣営色。プレビューの淡い着色用）。</summary>
        public Color GetShipColor()
        {
            SpriteRenderer sr = FindShipSpriteTemplate();
            return sr != null ? sr.color : Color.white;
        }

        /// <summary>
        /// 配下艦の残存状況を集計します（HUD表示用）。
        /// </summary>
        public void GetEscortStatus(out int aliveCount, out int totalShipCount)
        {
            aliveCount = 0;
            totalShipCount = 0;
            foreach (var m in memberShips)
            {
                if (m == null) continue;
                EscortShip e = m.GetComponent<EscortShip>();
                if (e != null && e.IsAlive)
                {
                    aliveCount++;
                    totalShipCount += e.shipCount;
                }
            }
        }

        /// <summary>
        /// 艦隊全体を囲う外接円（旗艦を中心、生存配下艦の最遠＋余白を半径）を求めます。
        /// 攻撃目標選択時のハイライト表示用。
        /// </summary>
        public void GetBoundingCircle(out Vector3 center, out float radius)
        {
            center = transform.position;
            float maxSq = 0f;
            foreach (var m in memberShips)
            {
                if (m == null) continue;
                // 実在する生存「配下艦」だけで半径を決める（提督名/兵力などの文字ラベルや
                // マーカーは EscortShip を持たないので除外。文字は円からはみ出して重なってよい）
                EscortShip e = m.GetComponent<EscortShip>();
                if (e == null || !e.IsAlive) continue;
                float d = ((Vector2)m.position - (Vector2)center).sqrMagnitude;
                if (d > maxSq) maxSq = d;
            }
            radius = Mathf.Sqrt(maxSq) + spacing * 0.5f; // 艦艇に沿う小さめの余白
            float minRadius = spacing;                   // 旗艦のみでも見える最小サイズ
            if (radius < minRadius) radius = minRadius;
        }

        /// <summary>
        /// 消滅した配下艦をリストから除外します（陣形計算の対象から外す）。
        /// velocities と添字を揃えるため同じ位置を同時に削除します。
        /// </summary>
        public void RemoveMember(Transform member)
        {
            int idx = memberShips.IndexOf(member);
            if (idx < 0) return;
            memberShips.RemoveAt(idx);
            if (idx < velocities.Count) velocities.RemoveAt(idx);
        }

        private void Update()
        {
            UpdateShipPositions();
        }

        /// <summary>
        /// 陣形に基づいた各艦の目標座標を計算し、SmoothDampで追従させます。
        /// </summary>
        private void UpdateShipPositions()
        {
            EnsureSlots();

            // 交戦中はゆっくり追従（穴埋め移動も緩やかに）。非交戦時は従来の機敏さ。
            bool inCombat = (flagshipWeapon != null && flagshipWeapon.IsInCombat);
            float st = inCombat ? combatSmoothTime : smoothTime;

            for (int i = 0; i < memberShips.Count; i++)
            {
                if (memberShips[i] == null) continue;
                if (i >= cachedSlots.Count) continue;

                // ローカル座標→ワールド座標（旗艦の回転に追従。root スケールは1前提）
                Vector3 targetWorldPos = transform.TransformPoint(cachedSlots[i]);
                targetWorldPos.z = transform.position.z;

                Vector2 currentPos = memberShips[i].position;
                Vector2 velocity = velocities[i];
                Vector2 nextPos = Vector2.SmoothDamp(currentPos, targetWorldPos, ref velocity, st);
                velocities[i] = velocity;

                memberShips[i].position = new Vector3(nextPos.x, nextPos.y, targetWorldPos.z);
                memberShips[i].rotation = transform.rotation; // 向きは旗艦に同期
            }
        }

        /// <summary>
        /// 隻数・陣形が変わった時だけ陣形スロットを再計算してキャッシュする。
        /// </summary>
        private void EnsureSlots()
        {
            int n = memberShips.Count;
            if (n == cachedCount && currentFormation == cachedFormation) return;
            cachedSlots = ComputeSlots(currentFormation, n);
            cachedCount = n;
            cachedFormation = currentFormation;
        }

        // ===== 陣形ごとのスロット計算（すべて原点中心・左右対称・+Y=前方）=====

        private List<Vector2> ComputeSlots(Formation f, int n)
        {
            switch (f)
            {
                case Formation.鶴翼陣: return ComputeCrescent(n);
                case Formation.円陣:   return ComputeCircle(n);
                case Formation.横陣:   return ComputeLine(n);
                case Formation.方陣:   return ComputeSquare(n);
                case Formation.紡錘陣:
                default:               return ComputeSpindle(n);
            }
        }

        /// <summary>紡錘陣：中央が最も幅広く、前後が細い縦長レンズ（前方をやや尖らせる）。</summary>
        private List<Vector2> ComputeSpindle(int n)
        {
            var slots = new List<Vector2>(n);
            if (n <= 0) return slots;

            int rows = Mathf.Max(3, Mathf.RoundToInt(Mathf.Sqrt(n) * 1.6f));
            float[] w = new float[rows];
            float sum = 0f;
            for (int r = 0; r < rows; r++)
            {
                float t = (rows == 1) ? 0.5f : (float)r / (rows - 1); // 0=後方,1=前方
                float yn = t * 2f - 1f;                               // -1..1
                float width = Mathf.Sqrt(Mathf.Max(0f, 1f - yn * yn)); // レンズ（楕円）
                if (yn > 0f) width *= Mathf.Lerp(1f, 0.65f, yn);       // 前方をやや細く尖らせる
                w[r] = Mathf.Max(0.05f, width);
                sum += w[r];
            }

            int[] perRow = new int[rows];
            int assigned = 0;
            for (int r = 0; r < rows; r++)
            {
                perRow[r] = Mathf.Max(0, Mathf.RoundToInt(w[r] / sum * n));
                assigned += perRow[r];
            }
            AdjustCounts(perRow, ref assigned, n);

            float yStart = -(rows - 1) / 2f;
            for (int r = 0; r < rows; r++)
            {
                int c = perRow[r];
                float y = (yStart + r) * spacing;
                for (int k = 0; k < c; k++)
                {
                    float x = (c == 1) ? 0f : (k - (c - 1) / 2f) * spacing;
                    slots.Add(new Vector2(x, y));
                }
            }
            return slots;
        }

        /// <summary>鶴翼陣：旗艦を要に、前方へ開いた幅広の弧（厚み2〜3列）。</summary>
        private List<Vector2> ComputeCrescent(int n)
        {
            var slots = new List<Vector2>(n);
            if (n <= 0) return slots;

            int layers = (n >= 24) ? 3 : (n >= 12 ? 2 : 1);
            float spreadDeg = 95f;            // 前方(90°)を中心に±この角度
            float baseRadius = spacing * 3f;  // 旗艦から弧までの距離

            int perLayer = Mathf.CeilToInt((float)n / layers);
            int idx = 0;
            for (int L = 0; L < layers && idx < n; L++)
            {
                int c = Mathf.Min(perLayer, n - idx);
                float radius = baseRadius + L * spacing;
                for (int k = 0; k < c; k++)
                {
                    float t = (c == 1) ? 0.5f : (float)k / (c - 1);
                    float angleDeg = 90f + Mathf.Lerp(-spreadDeg, spreadDeg, t);
                    float a = angleDeg * Mathf.Deg2Rad;
                    slots.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
                    idx++;
                }
            }
            return slots;
        }

        /// <summary>円陣：旗艦中心の同心円リング。</summary>
        private List<Vector2> ComputeCircle(int n)
        {
            var slots = new List<Vector2>(n);
            int placed = 0;
            int ring = 1;
            while (placed < n)
            {
                float radius = ring * spacing;
                int capacity = Mathf.Max(1, Mathf.FloorToInt(2f * Mathf.PI * radius / spacing));
                int c = Mathf.Min(capacity, n - placed);
                float offset = (ring % 2) * 0.5f; // リングごとに少しずらす
                for (int k = 0; k < c; k++)
                {
                    float a = (2f * Mathf.PI * (k + offset)) / c;
                    slots.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
                }
                placed += c;
                ring++;
            }
            return slots;
        }

        /// <summary>横陣：旗艦中心の左右一直線（隻数が多ければ2段）。</summary>
        private List<Vector2> ComputeLine(int n)
        {
            var slots = new List<Vector2>(n);
            int rows = (n > 16) ? 2 : 1;
            int perRow = Mathf.CeilToInt((float)n / rows);
            int idx = 0;
            for (int r = 0; r < rows && idx < n; r++)
            {
                int c = Mathf.Min(perRow, n - idx);
                float y = (r - (rows - 1) / 2f) * spacing * 0.8f;
                for (int k = 0; k < c; k++)
                {
                    float x = (k - (c - 1) / 2f) * spacing;
                    slots.Add(new Vector2(x, y));
                    idx++;
                }
            }
            return slots;
        }

        /// <summary>方陣：旗艦中心の格子状。</summary>
        private List<Vector2> ComputeSquare(int n)
        {
            var slots = new List<Vector2>(n);
            if (n <= 0) return slots;

            int cols = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
            int rows = Mathf.CeilToInt((float)n / cols);
            for (int idx = 0; idx < n; idx++)
            {
                int r = idx / cols;
                int col = idx % cols;
                float x = (col - (cols - 1) / 2f) * spacing;
                float y = (r - (rows - 1) / 2f) * spacing;
                slots.Add(new Vector2(x, y));
            }
            return slots;
        }

        /// <summary>行ごとの配分隻数の合計を target に一致させる（中央行で増減）。</summary>
        private void AdjustCounts(int[] perRow, ref int assigned, int target)
        {
            int center = perRow.Length / 2;
            int guard = 0;
            while (assigned < target && guard++ < 100000)
            {
                perRow[center]++;
                assigned++;
            }
            while (assigned > target && guard++ < 100000)
            {
                int maxR = 0;
                for (int r = 1; r < perRow.Length; r++) if (perRow[r] > perRow[maxR]) maxR = r;
                if (perRow[maxR] <= 0) break;
                perRow[maxR]--;
                assigned--;
            }
        }
    }
}
