using UnityEngine;
using System.Collections.Generic;

namespace Ginei
{
    // Formation enum は Core の Formation.cs へ切り出し（#496・AdmiralData 等の Core 参照のため）

    /// <summary>
    /// 艦種ごとの能力倍率（#80・実効値パターン＝基準値を上書きせず生成時に乗算）。Inspector で調整可。
    /// </summary>
    [System.Serializable]
    public class ShipClassStats
    {
        [Tooltip("耐久倍率（艦艇数=Squadron.escortShipCount に乗算）")]
        public float durabilityMultiplier = 1f;

        [Tooltip("火力倍率（EscortShip が基準ダメージに乗算）")]
        public float firepowerMultiplier = 1f;

        [Tooltip("速度倍率（スロット追従の上限速度に乗算）")]
        public float speedMultiplier = 1f;

        [Tooltip("表示スケール倍率（memberScale に乗算。大型/小型の識別）")]
        public float scaleMultiplier = 1f;

        [Tooltip("陣営色に乗せる微差の色味（識別用）")]
        public Color tint = Color.white;

        public ShipClassStats() { }

        public ShipClassStats(float durability, float firepower, float speed, float scale, Color tint)
        {
            durabilityMultiplier = durability;
            firepowerMultiplier = firepower;
            speedMultiplier = speed;
            scaleMultiplier = scale;
            this.tint = tint;
        }
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

        [Header("艦種編成（#80・配下艦を戦艦/巡航艦/駆逐艦に差別化）")]
        [Tooltip("戦艦の編成比率（3比率は内部で正規化される）")]
        [Range(0f, 1f)] public float battleshipRatio = 0.2f;

        [Tooltip("巡航艦の編成比率")]
        [Range(0f, 1f)] public float cruiserRatio = 0.5f;

        [Tooltip("駆逐艦の編成比率")]
        [Range(0f, 1f)] public float destroyerRatio = 0.3f;

        [Tooltip("戦艦：高耐久・高火力・低速・大型表示")]
        public ShipClassStats battleshipStats = new ShipClassStats(2.0f, 1.6f, 0.7f, 1.35f, new Color(1f, 0.95f, 0.8f));

        [Tooltip("巡航艦：中庸（基準）")]
        public ShipClassStats cruiserStats = new ShipClassStats(1.0f, 1.0f, 1.0f, 1.0f, Color.white);

        [Tooltip("駆逐艦：低耐久・高速・小型表示")]
        public ShipClassStats destroyerStats = new ShipClassStats(0.6f, 0.8f, 1.4f, 0.75f, new Color(0.8f, 0.9f, 1f));

        [Header("配下艦の挙動（#69 リアル化）")]
        [Tooltip("スロットへ追従する最大速度の倍率（旗艦 maxSpeed×この値が上限。遅れは取り戻せるがワープしない）")]
        public float catchUpRatio = 1.3f;

        [Tooltip("配下艦が進行方向へ回頭する速度（度/秒）。移動中はこの速さで進行方向へ向き直る")]
        public float escortRotationSpeed = 180f;

        [Tooltip("この速さ(units/秒)以上で移動中は進行方向を向く。未満（定位置付近）では旗艦の向きへ戻す")]
        public float headingMoveThreshold = 0.5f;

        [Tooltip("定位置付近で旗艦の向き（射界）へ戻す回頭速度（度/秒）")]
        public float alignToFlagshipSpeed = 360f;

        [Header("配下艦の分離（重なり・すり抜け防止）")]
        [Tooltip("同一部隊内の配下艦同士が保つ最小間隔（0で無効）")]
        public float minSeparation = 0.6f;

        [Tooltip("分離の押し離し強度（0〜1。大きいほど一気に最小間隔まで開く）")]
        [Range(0f, 1f)]
        public float separationStrength = 0.5f;

        [Tooltip("分離スキャンの間引き間隔（秒）。0で毎フレーム。重い時は大きく")]
        public float separationUpdateInterval = 0.1f;

        // SmoothDamp用の速度バッファ（memberShips と添字同期）
        private List<Vector2> velocities = new List<Vector2>();

        // 各配下艦の EscortShip キャッシュ（memberShips と添字同期。移動で speedMultiplier を毎フレーム読むため）
        private List<EscortShip> memberEscorts = new List<EscortShip>();

        // 編成時に割り当てた艦種（memberShips と添字同期。初期化中のみ使用）
        private List<ShipClass> memberClasses = new List<ShipClass>();

        // 交戦中判定に使う旗艦の武装（Start でキャッシュ）
        private FleetWeapon flagshipWeapon;

        // 速度上限の基準＝旗艦の最高速（Start でキャッシュ。無ければフォールバック）
        private FleetMovement flagshipMovement;
        private float flagshipMaxSpeed = 6f;

        // 分離スキャンの間引き用タイマー（timeScale 追従の Time.time 基準）
        private float nextSeparationTime = 0f;

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
            AssignShipClasses();       // #80 各配下艦に艦種を割り当て、スケールに反映
            SetupEscorts();            // 各配下艦に EscortShip・艦艇数（耐久倍率）・火力/速度倍率・陣営を設定
            InitVelocities();
            RecolorFleet();            // 生成した配下艦にも陣営色を反映（艦種の色味#80は FactionColor が陣営色×classTint で適用）
            flagshipWeapon = GetComponent<FleetWeapon>();
            flagshipMovement = GetComponent<FleetMovement>();
            if (flagshipMovement != null) flagshipMaxSpeed = flagshipMovement.maxSpeed;
            // 分離スキャンの初回位相を分散（全部隊が同フレームに走らないように）
            nextSeparationTime = Time.time + Random.value * Mathf.Max(0f, separationUpdateInterval);
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
                if (n == "SelectionRing" || n == "FlagshipMarker" || n == "FlagshipMarkerGlow") continue;
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
        /// 各配下艦に EscortShip を付与・初期化し、艦種ごとの耐久（艦艇数）・火力・速度倍率を設定する（#80）。
        /// 倍率は基準値を上書きせず実効値として乗算（旗艦の基準は非破壊）。
        /// </summary>
        private void SetupEscorts()
        {
            FleetStrength flagship = GetComponent<FleetStrength>();
            memberEscorts.Clear();
            for (int i = 0; i < memberShips.Count; i++)
            {
                Transform ship = memberShips[i];
                if (ship == null) { memberEscorts.Add(null); continue; }

                EscortShip escort = ship.GetComponent<EscortShip>();
                if (escort == null) escort = ship.gameObject.AddComponent<EscortShip>();

                ShipClass cls = (i < memberClasses.Count) ? memberClasses[i] : ShipClass.巡航艦;
                ShipClassStats stats = StatsFor(cls);
                escort.shipClass = cls;
                escort.shipCount = Mathf.Max(1, Mathf.RoundToInt(escortShipCount * stats.durabilityMultiplier));
                escort.firepowerMultiplier = stats.firepowerMultiplier;
                escort.speedMultiplier = stats.speedMultiplier;
                escort.classTint = stats.tint; // FactionColor が陣営色×この色で塗る
                escort.Setup(this, flagship);

                memberEscorts.Add(escort);
            }
        }

        /// <summary>艦種ごとの倍率セットを返す。</summary>
        private ShipClassStats StatsFor(ShipClass cls)
        {
            switch (cls)
            {
                case ShipClass.戦艦:   return battleshipStats;
                case ShipClass.駆逐艦: return destroyerStats;
                case ShipClass.巡航艦:
                default:               return cruiserStats;
            }
        }

        /// <summary>
        /// 編成比率（戦艦/巡航艦/駆逐艦）に応じて各配下艦へ艦種を割り当て、表示スケールに反映する（#80）。
        /// スロット配置は艦種非依存（最小実装＝混成のみ。前衛/遊撃の配置最適化は将来）。
        /// </summary>
        private void AssignShipClasses()
        {
            int n = memberShips.Count;
            memberClasses = new List<ShipClass>(n);

            // 比率を正規化（全0なら全て巡航艦）。
            float bb = Mathf.Max(0f, battleshipRatio);
            float cr = Mathf.Max(0f, cruiserRatio);
            float dd = Mathf.Max(0f, destroyerRatio);
            float sum = bb + cr + dd;

            // 戦艦・駆逐艦の隻数を比率から決め、残りを巡航艦に（混在しやすいよう一定間隔で割当）。
            int battleships = (sum > 0f) ? Mathf.RoundToInt(n * bb / sum) : 0;
            int destroyers = (sum > 0f) ? Mathf.RoundToInt(n * dd / sum) : 0;
            if (battleships + destroyers > n) destroyers = Mathf.Max(0, n - battleships);

            for (int i = 0; i < n; i++) memberClasses.Add(ShipClass.巡航艦);

            // 戦艦は前方寄り（添字前半）、駆逐艦は後方寄り（添字後半）に置いて団子を避ける。
            for (int i = 0; i < battleships && i < n; i++) memberClasses[i] = ShipClass.戦艦;
            for (int i = 0; i < destroyers && (n - 1 - i) >= battleships; i++) memberClasses[n - 1 - i] = ShipClass.駆逐艦;

            // 表示スケールを艦種倍率で調整（現在のスケール＝memberScale 済みに乗算）。
            for (int i = 0; i < n; i++)
            {
                if (memberShips[i] == null) continue;
                float s = StatsFor(memberClasses[i]).scaleMultiplier;
                if (!Mathf.Approximately(s, 1f)) memberShips[i].localScale *= s;
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
        /// 生存配下艦を艦種別に集計します（HUDの内訳表示用・#80）。
        /// </summary>
        public void GetEscortClassBreakdown(out int battleships, out int cruisers, out int destroyers)
        {
            battleships = 0; cruisers = 0; destroyers = 0;
            foreach (var m in memberShips)
            {
                if (m == null) continue;
                EscortShip e = m.GetComponent<EscortShip>();
                if (e == null || !e.IsAlive) continue;
                switch (e.shipClass)
                {
                    case ShipClass.戦艦:   battleships++; break;
                    case ShipClass.駆逐艦: destroyers++; break;
                    default:               cruisers++; break;
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
            if (idx < memberEscorts.Count) memberEscorts.RemoveAt(idx); // 艦種キャッシュも添字同期
        }

        private void Update()
        {
            UpdateShipPositions();
        }

        /// <summary>
        /// 陣形に基づいた各艦の目標座標を計算し、SmoothDampで追従させます。
        /// #69：速度上限（ワープ防止）・進行方向への回頭・分離（重なり防止）で動きを現実的にする。
        /// </summary>
        private void UpdateShipPositions()
        {
            EnsureSlots();

            // 交戦中はゆっくり追従（穴埋め移動も緩やかに）。非交戦時は従来の機敏さ。
            bool inCombat = (flagshipWeapon != null && flagshipWeapon.IsInCombat);
            float st = inCombat ? combatSmoothTime : smoothTime;

            float dt = Time.deltaTime;
            // 速度上限の基準＝旗艦の最高速 × catchUpRatio（遅れは取り戻せるがワープしない）。
            float baseMaxSpeed = (flagshipMovement != null ? flagshipMovement.maxSpeed : flagshipMaxSpeed)
                                 * Mathf.Max(0.1f, catchUpRatio);

            for (int i = 0; i < memberShips.Count; i++)
            {
                if (memberShips[i] == null) continue;
                if (i >= cachedSlots.Count) continue;

                // #80 艦種の速度倍率を上限に乗算（戦艦は遅く・駆逐艦は速く＝速い遊撃）。FleetMovement は不変。
                float speedMul = (i < memberEscorts.Count && memberEscorts[i] != null)
                                 ? memberEscorts[i].SpeedMultiplier : 1f;
                float maxSpeed = baseMaxSpeed * Mathf.Max(0.1f, speedMul);
                float maxStep = maxSpeed * dt;

                // ローカル座標→ワールド座標（旗艦の回転に追従。root スケールは1前提）
                Vector3 targetWorldPos = transform.TransformPoint(cachedSlots[i]);
                targetWorldPos.z = transform.position.z;

                Vector2 currentPos = memberShips[i].position;
                Vector2 velocity = velocities[i];
                // SmoothDamp 自体に maxSpeed を渡して、旗艦回頭時の外周艦の異常加速を抑える。
                Vector2 nextPos = Vector2.SmoothDamp(currentPos, (Vector2)targetWorldPos, ref velocity, st, maxSpeed);

                // 1フレームの移動量も上限でクランプ（瞬間移動＝ワープを確実に防ぐ）。
                Vector2 delta = nextPos - currentPos;
                if (dt > 0f && delta.magnitude > maxStep)
                {
                    delta = delta.normalized * maxStep;
                    nextPos = currentPos + delta;
                    if (velocity.magnitude > maxSpeed) velocity = velocity.normalized * maxSpeed;
                }
                velocities[i] = velocity;

                memberShips[i].position = new Vector3(nextPos.x, nextPos.y, targetWorldPos.z);

                // 向き：十分な速度で移動中は進行方向へ弧を描いて回頭、定位置付近では旗艦の向き（射界）へ戻す。
                UpdateEscortFacing(memberShips[i], delta, dt);
            }

            // 同一部隊内のすり抜け・重なりを押し離しで解消（間引き＋位相分散で軽量に）。
            ApplySeparation();
        }

        /// <summary>
        /// 配下艦の向きを更新。移動中(閾値以上)は進行方向(+Y=前方)へ回頭、
        /// 定位置付近では旗艦の向きへ戻す。回頭は速度制限つき（瞬間的な向き反転を防ぐ）。
        /// ※移動中は前を向けない＝撃ちにくい、という挙動は仕様として許容（#69）。
        /// </summary>
        private void UpdateEscortFacing(Transform ship, Vector2 delta, float dt)
        {
            if (dt <= 0f) return;
            float speed = delta.magnitude / dt;

            Quaternion targetRot;
            float rotSpeed;
            if (speed >= headingMoveThreshold && delta.sqrMagnitude > 1e-6f)
            {
                // 進行方向を前方(Transform.up)に向ける：up が角度φのとき z 回転は φ-90°。
                float ang = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
                targetRot = Quaternion.Euler(0f, 0f, ang);
                rotSpeed = escortRotationSpeed;
            }
            else
            {
                // 定位置：旗艦の向き（射界）へ戻す
                targetRot = transform.rotation;
                rotSpeed = alignToFlagshipSpeed;
            }
            ship.rotation = Quaternion.RotateTowards(ship.rotation, targetRot, rotSpeed * dt);
        }

        /// <summary>
        /// 同一部隊内の配下艦同士が minSeparation 未満に重なったら、半分ずつ押し離して
        /// すり抜け・団子を防ぐ。間引き(separationUpdateInterval)＋初回位相分散で負荷を抑える。
        /// 旗艦(transform)は対象外＝root は動かさない。
        /// </summary>
        private void ApplySeparation()
        {
            if (minSeparation <= 0f || separationStrength <= 0f) return;
            if (separationUpdateInterval > 0f)
            {
                if (Time.time < nextSeparationTime) return;
                nextSeparationTime = Time.time + separationUpdateInterval;
            }

            float minSq = minSeparation * minSeparation;
            int n = memberShips.Count;
            for (int i = 0; i < n; i++)
            {
                Transform a = memberShips[i];
                if (a == null) continue;
                Vector2 pa = a.position;
                for (int j = i + 1; j < n; j++)
                {
                    Transform b = memberShips[j];
                    if (b == null) continue;
                    Vector2 pb = b.position;
                    Vector2 d = pa - pb;
                    float dsq = d.sqrMagnitude;
                    if (dsq < minSq && dsq > 1e-6f)
                    {
                        float dist = Mathf.Sqrt(dsq);
                        Vector2 push = d / dist * ((minSeparation - dist) * 0.5f * separationStrength);
                        pa += push;
                        pb -= push;
                        a.position = new Vector3(pa.x, pa.y, a.position.z);
                        b.position = new Vector3(pb.x, pb.y, b.position.z);
                    }
                }
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
