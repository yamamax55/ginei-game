// 恒星メッシュ用シェーダー（#恒星モデル）。
//
// このプロジェクトの URP は **2D Renderer** で動いており、3D ライトの前方/遅延パスが無い。
// そこで実光源に頼らず、**頂点カラー（COLOR）＋視線との角度**だけで球の立体感を作る。
// LightMode タグを付けない＝SRPDefaultUnlit として 2D Renderer の不透明描画に確実に拾われる。
//
// 恒星の見せ方：
//  ・表面の色は FBX の頂点カラーが持つ（名前ごとの固有色）。ここでは色を作らず「乗せる」だけ。
//  ・中心が明るく縁が落ちる（周縁減光＝limb darkening）＝平たい円板でなく球に見える。
//  ・中心の白熱はごく控えめ。白飛びさせると恒星が「ただの白丸」になり、名前ごとの色が消える。
//  ・最後にソフトな圧縮をかけて 1.0 を超える成分をなだらかに丸める＝HDR の白飛びを避ける。
Shader "Ginei/StarSelfLit"
{
    Properties
    {
        _Tint        ("Tint (multiplies vertex color)", Color) = (1,1,1,1)
        _Brightness  ("Brightness", Range(0.1, 3))             = 1.15
        _LimbPower   ("Limb Darkening Power", Range(0.1, 4))   = 0.85
        _LimbFloor   ("Limb Floor (edge brightness)", Range(0,1)) = 0.30
        _CoreBoost   ("Core Glow (same hue)", Range(0, 2))     = 0.45
        _CorePower   ("Core Glow Tightness", Range(1, 16))     = 5.0
        _SoftKnee    ("Highlight Soft Knee", Range(0, 2))      = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            half _Brightness;
            half _LimbPower;
            half _LimbFloor;
            half _CoreBoost;
            half _CorePower;
            half _SoftKnee;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color  : COLOR;   // 表面の固有色（Blender の頂点カラー COLOR）
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos        : SV_POSITION;
                float3 worldNormal: TEXCOORD0;
                float3 worldPos   : TEXCOORD1;
                fixed4 color      : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 N = normalize(i.worldNormal);
                float3 V = normalize(_WorldSpaceCameraPos - i.worldPos);

                // 表面色＝頂点カラー×Tint。頂点カラーが無い（白）モデルでも破綻しない。
                half3 c = i.color.rgb * _Tint.rgb;

                // 正面度：1=こちらを向く面（中心）／0=シルエットの縁。
                half ndv = saturate(dot(N, V));

                // 周縁減光。縁を _LimbFloor まで落として球の丸みを出す（真っ黒にはしない）。
                half limb  = pow(ndv, _LimbPower);
                half shade = lerp(_LimbFloor, 1.0h, limb);

                half3 lit = c * shade * _Brightness;

                // 中心の白熱は「同じ色を濃くする」方向で足す＝白へ寄せない（色が飛ばない）。
                lit += c * pow(ndv, _CorePower) * _CoreBoost;

                // ソフト圧縮：1.0 を超えるぶんをなだらかに丸めて白飛びを防ぐ。
                lit = lit / (1.0h + lit * _SoftKnee);

                return fixed4(saturate(lit), 1.0);
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
