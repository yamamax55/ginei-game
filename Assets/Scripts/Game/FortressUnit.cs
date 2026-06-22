using UnityEngine;

namespace Ginei
{
    /// <summary>
    /// 戦術要塞（イゼルローン型・#76）。Battle シーンの固定拠点を「動かない艦隊」として実装する。
    /// 構成＝コア（旗艦相当・IShipTarget・巨大耐久・移動/士気なし）＋外周の砲台群（<see cref="FortressTurret"/>）。
    /// - 砲台は個別破壊可能で、稼働数が減るほどコアのシールドが弱まり被ダメが増える（段階攻略＝<see cref="FortressBatteryRules"/>）。
    /// - コアは円形＝背面が無いため側背面ボーナスは自然に相殺される（攻撃が全周から来て平均化）。
    /// - ビジュアルはランタイム生成（外周リング＋コア＋砲台。陣営色で着色）。
    /// - コア撃破＝要塞陥落（爆発＋画面シェイク）。勝敗算入は #78（要塞攻略/要塞防衛）が <see cref="FortressRegistry"/> を見る。
    /// </summary>
    public class FortressUnit : MonoBehaviour, IShipTarget
    {
        [Header("コア（旗艦相当）")]
        [Tooltip("コアの耐久（艦艇数相当）。0で要塞陥落")]
        public int coreStrength = 9000;

        [Tooltip("コアの被ダメ軽減（0..1）。要塞コアは固い")]
        [Range(0f, 0.9f)]
        public float coreDamageReduction = 0.3f;

        [Header("シールド（稼働砲台依存・FortressBatteryRules）")]
        [Tooltip("全砲台稼働時のコア被ダメ倍率（小さいほど固い）")]
        public float shieldedCoreDamageFactor = 0.25f;
        [Tooltip("稼働砲台比がこの値以下でシールド消失（コア被ダメ→等倍）")]
        public float exposedTurretRatio = 0.3f;

        [Header("砲台群")]
        [Tooltip("外周に配置する砲台の数")]
        public int turretCount = 8;
        [Tooltip("砲台を並べる外周リングの半径（ワールド単位）")]
        public float turretRingRadius = 3.0f;
        [Tooltip("各砲台の見た目スケール")]
        public float turretScale = 0.9f;
        [Tooltip("砲台1基の耐久（艦艇数相当）")]
        public int turretStrength = 400;
        [Tooltip("砲台1基の射程")]
        public float turretRange = 14f;
        [Tooltip("砲台1基の1発ダメージ")]
        public int turretDamage = 28;

        [Header("ビジュアル")]
        [Tooltip("コアの表示半径（ワールド単位）")]
        public float coreVisualRadius = 1.6f;
        [Tooltip("外周リングの表示太さ（防御陣の可視化）")]
        public float perimeterRingRadius = 3.4f;
        [Tooltip("陣営色フォールバック（帝国）")]
        public Color imperialColor = new Color(0.9f, 0.2f, 0.2f);
        [Tooltip("陣営色フォールバック（同盟）")]
        public Color allianceColor = new Color(0.2f, 0.5f, 0.9f);

        // ── 陣営（生成時に設定）──
        private Faction faction = Faction.帝国;
        private FactionData factionData;
        private string fortressName = "要塞";

        // ── ランタイム状態 ──
        private readonly System.Collections.Generic.List<FortressTurret> turrets = new System.Collections.Generic.List<FortressTurret>();
        private int activeTurrets;
        private bool fallen = false;
        private SpriteRenderer coreRenderer;
        private TextMesh nameLabel;

        // 共有スプライト（アプリ寿命）。インスタンス固有マテリアルは持たない（SpriteRenderer.color で着色）。
        private static Sprite sharedDisc;
        private static Sprite sharedRing;

        private const int CoreSortingOrder = -5;
        private const int RingSortingOrder = -6;
        private const int TurretSortingOrder = -4;

        // ── IShipTarget 実装 ──
        public Transform Transform => transform;
        public Faction Faction => faction;
        public FactionData FactionData => factionData;
        public bool IsAlive => !fallen && coreStrength > 0;

        /// <summary>稼働中の砲台数（シールド計算・#78 制圧判定・Result 表示用）。</summary>
        public int ActiveTurretCount => activeTurrets;
        /// <summary>総砲台数。</summary>
        public int TurretCount => turrets.Count;
        /// <summary>コアの残存割合（0..1・Result 表示用）。</summary>
        public float CoreRatio => maxCoreStrength > 0 ? Mathf.Clamp01((float)coreStrength / maxCoreStrength) : 0f;
        /// <summary>要塞名。</summary>
        public string FortressName => fortressName;

        private int maxCoreStrength = 1;
        private FortressBatteryParams Params => new FortressBatteryParams(shieldedCoreDamageFactor, exposedTurretRatio);

        /// <summary>
        /// 要塞を初期化する（BattleSetup から呼ぶ）。陣営・規模を反映し、ビジュアルと砲台群を生成、
        /// 索敵レジストリ（IShipTarget）と要塞レジストリへ登録する。
        /// </summary>
        public void Setup(Faction faction, FactionData factionData, string fortressName,
            int turretCountOverride = -1, int coreStrengthOverride = -1)
        {
            this.faction = factionData != null ? factionData.legacyFaction : faction;
            this.factionData = factionData;
            if (!string.IsNullOrEmpty(fortressName)) this.fortressName = fortressName;
            if (turretCountOverride >= 0) turretCount = turretCountOverride;
            if (coreStrengthOverride > 0) coreStrength = coreStrengthOverride;
            maxCoreStrength = Mathf.Max(1, coreStrength);

            transform.rotation = Quaternion.identity; // 円形＝向きは持たない

            Color color = ResolveColor();
            BuildVisuals(color);
            BuildTurrets(color);

            FleetRegistry.Register(this);   // 攻撃対象（IShipTarget）として索敵に載せる
            FortressRegistry.Register(this); // 勝敗判定・攻城目標の照会用
        }

        private Color ResolveColor()
        {
            if (factionData != null) return factionData.color;
            return faction == Faction.帝国 ? imperialColor : allianceColor;
        }

        // ── ビジュアル生成（外周リング＋コア＋名前ラベル）──
        private void BuildVisuals(Color color)
        {
            // 防御陣を表す外周リング（薄く陣営色）
            GameObject ringObj = new GameObject("FortressRing");
            ringObj.transform.SetParent(transform, false);
            ringObj.transform.localScale = Vector3.one * (perimeterRingRadius * 2f);
            SpriteRenderer ringSR = ringObj.AddComponent<SpriteRenderer>();
            ringSR.sprite = GetRingSprite();
            ringSR.sortingOrder = RingSortingOrder;
            ringSR.color = new Color(color.r, color.g, color.b, 0.22f);

            // コア（暗い金属＋陣営色）
            GameObject coreObj = new GameObject("FortressCore");
            coreObj.transform.SetParent(transform, false);
            coreObj.transform.localScale = Vector3.one * (coreVisualRadius * 2f);
            coreRenderer = coreObj.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = GetDiscSprite();
            coreRenderer.sortingOrder = CoreSortingOrder;
            coreRenderer.color = Color.Lerp(color, new Color(0.15f, 0.15f, 0.18f), 0.4f);

            // 名前ラベル（legacy TextMesh・日本語フォントは FontProvider 経由）
            GameObject labelObj = new GameObject("FortressLabel");
            labelObj.transform.SetParent(transform, false);
            labelObj.transform.localPosition = new Vector3(0f, perimeterRingRadius + 0.8f, 0f);
            nameLabel = labelObj.AddComponent<TextMesh>();
            nameLabel.text = fortressName;
            nameLabel.anchor = TextAnchor.MiddleCenter;
            nameLabel.alignment = TextAlignment.Center;
            nameLabel.fontSize = 48;
            nameLabel.characterSize = 0.08f;
            nameLabel.color = color;
            Font jp = FontProvider.JapaneseFont;
            if (jp != null) { nameLabel.font = jp; nameLabel.GetComponent<MeshRenderer>().material = jp.material; }
        }

        // ── 砲台群の生成（外周に等間隔・外向き）──
        private void BuildTurrets(Color color)
        {
            int n = Mathf.Max(0, turretCount);
            for (int i = 0; i < n; i++)
            {
                float angDeg = FortressBatteryRules.TurretAngleDeg(i, n);
                float angRad = angDeg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angRad), Mathf.Sin(angRad)); // 外向き
                Vector3 localPos = new Vector3(dir.x, dir.y, 0f) * turretRingRadius;

                GameObject tObj = new GameObject($"Turret_{i}");
                tObj.transform.SetParent(transform, false);
                tObj.transform.localPosition = localPos;
                // transform.up を外向きに（2D：Atan2-90°）
                float zRot = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
                tObj.transform.localRotation = Quaternion.Euler(0f, 0f, zRot);
                tObj.transform.localScale = Vector3.one * turretScale;

                SpriteRenderer sr = tObj.AddComponent<SpriteRenderer>();
                sr.sprite = GetDiscSprite();
                sr.sortingOrder = TurretSortingOrder;
                sr.color = color;

                FortressTurret turret = tObj.AddComponent<FortressTurret>();
                turret.shipCount = turretStrength;
                turret.range = turretRange;
                turret.damage = turretDamage;
                turret.Setup(this, sr, color);
                turrets.Add(turret);
            }
            activeTurrets = turrets.Count;
        }

        /// <summary>砲台が沈黙したとき（稼働数を減らす＝コアのシールドが弱まる）。</summary>
        public void OnTurretSilenced()
        {
            activeTurrets = Mathf.Max(0, activeTurrets - 1);
        }

        /// <summary>
        /// コアにダメージ。コアの被ダメ軽減＋稼働砲台比のシールド倍率を掛けて適用（基準ダメージは非破壊）。
        /// 砲台を削るほどシールドが弱まりコアが脆くなる（段階攻略）。
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (fallen || damage <= 0) return;
            float reduced = damage * (1f - Mathf.Clamp01(coreDamageReduction));
            float shield = FortressBatteryRules.CoreDamageMultiplier(activeTurrets, turrets.Count, Params);
            int applied = Mathf.Max(1, Mathf.RoundToInt(reduced * shield));
            coreStrength -= applied;
            FlashCore();
            if (coreStrength <= 0) Fall();
        }

        private void FlashCore()
        {
            if (coreRenderer == null) return;
            // 被弾の明滅は Juice に集約（元の色へ自動復帰＝白へ恒久ドリフトしない）。
            Juice.Flash(coreRenderer, Color.white);
        }

        /// <summary>要塞陥落：爆発演出＋画面シェイク＋通知。砲台ごと破棄し、レジストリから外す。</summary>
        private void Fall()
        {
            if (fallen) return;
            fallen = true;
            coreStrength = 0;

            FleetRegistry.Unregister(this);
            FortressRegistry.Unregister(this);

            SpawnExplosion(transform.position);
            if (AudioManager.Instance != null) AudioManager.Instance.PlayExplosion();
            CameraController cam = Object.FindAnyObjectByType<CameraController>();
            if (cam != null) cam.Shake();

            NotificationCenter.Push(NotificationCategory.戦闘, NotificationSeverity.警告,
                $"要塞「{fortressName}」（{faction}）が陥落した");
            Debug.Log($"FortressUnit: 要塞「{fortressName}」({faction}) が陥落した。");

            Destroy(gameObject); // 子の砲台も破棄（各 OnDestroy で Unregister）
        }

        private void SpawnExplosion(Vector3 pos)
        {
            GameObject go = new GameObject("FortressExplosion");
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.duration = 0.8f;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startColor = new Color(1f, 0.7f, 0.25f, 1f);
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 80) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = coreVisualRadius;
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.material = new Material(Shader.Find("Sprites/Default"));
            psr.sortingOrder = 20;
            ps.Play();
        }

        private void OnDestroy()
        {
            FleetRegistry.Unregister(this);
            FortressRegistry.Unregister(this);
        }

        // ── 共有スプライト（アプリ寿命で1個ずつ生成）──
        private static Sprite GetDiscSprite()
        {
            if (sharedDisc != null) return sharedDisc;
            const int size = 64;
            const float c = (size - 1) / 2f;
            const float r = size / 2f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    float a = Mathf.Clamp01((r - d) / 1.5f); // 縁を軽くアンチエイリアス
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            sharedDisc = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedDisc;
        }

        private static Sprite GetRingSprite()
        {
            if (sharedRing != null) return sharedRing;
            const int size = 128;
            const float c = (size - 1) / 2f;
            const float outerR = size / 2f;
            const float innerR = size * 0.42f;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    if (d > outerR || d < innerR) { tex.SetPixel(x, y, new Color(1f, 1f, 1f, 0f)); continue; }
                    float mid = (innerR + outerR) * 0.5f;
                    float half = (outerR - innerR) * 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / half);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            tex.Apply();
            sharedRing = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return sharedRing;
        }
    }
}
