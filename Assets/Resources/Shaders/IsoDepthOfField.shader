// 等距 2D 用的伪景深：精灵走 Sprites/Default 不写深度，拿不到深度图，
// 所以用屏幕纵向的模糊——等距画面里「越往上越远」，所以上沿糊得比下沿重。
// 中间一条带保持清晰，越靠近画面顶端模糊越强，视觉上等价于「镜头拉近后只剩取景中心对焦」。
Shader "Hidden/Shenyan/IsoDepthOfField"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;

    sampler2D _BlurTex;

    float2 _Dir;          // 模糊方向（1,0）或（0,1）
    float  _Radius;       // 模糊半径（像素）
    float  _Amount;       // 最终叠加强度 0..1
    float  _Focus;        // 清晰带中心（uv.y）
    float  _Band;         // 清晰带半高
    float  _FalloffUp;    // 往上（远处）过渡到全模糊的宽度
    float  _FalloffDown;  // 往下（近处）过渡到全模糊的宽度

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv  : TEXCOORD0;
    };

    v2f vert(appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }

    // 5 抽头高斯（可分离，横竖各跑一遍）
    fixed4 fragBlur(v2f i) : SV_Target
    {
        float2 off = _Dir * _MainTex_TexelSize.xy * _Radius;

        fixed4 c = tex2D(_MainTex, i.uv) * 0.2270270270;
        c += tex2D(_MainTex, i.uv + off * 1.3846153846) * 0.3162162162;
        c += tex2D(_MainTex, i.uv - off * 1.3846153846) * 0.3162162162;
        c += tex2D(_MainTex, i.uv + off * 3.2307692308) * 0.0702702703;
        c += tex2D(_MainTex, i.uv - off * 3.2307692308) * 0.0702702703;
        return c;
    }

    // 按离清晰带的距离把模糊版盖回原图；上方（远）比下方（近）过渡得更急
    fixed4 fragComposite(v2f i) : SV_Target
    {
        fixed4 sharp = tex2D(_MainTex, i.uv);
        fixed4 blur  = tex2D(_BlurTex, i.uv);

        float dy = i.uv.y - _Focus;
        float d = abs(dy);
        float width = max(0.0001, dy > 0.0 ? _FalloffUp : _FalloffDown);
        float m = smoothstep(_Band, _Band + width, d) * _Amount;

        return lerp(sharp, blur, m);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlur
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }

    Fallback Off
}
