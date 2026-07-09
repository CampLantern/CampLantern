// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)
Shader "Custom/BaitAdditive" {
	Properties{
		_TintColor("Tint Color", Color) = (0.5,0.5,0.5,0.5)
	}

	Category
	{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
		Blend SrcAlpha One
		ColorMask RGB
		Cull Off Lighting Off ZWrite Off

		stencil 
		{
			ref 0
			comp equal
			writeMask 255 // default
			readMask 255 // default
			pass incrSat
			Zfail keep
			Fail keep
		}

		SubShader 
		{
			Pass 
			{
				CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#pragma target 2.0
				#include "UnityCG.cginc"

				fixed4 _TintColor;

				struct appdata_t {
					half4 vertex : POSITION;

					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f {
					half4 vertex : SV_POSITION;
					fixed4 color : COLOR;
					
					UNITY_VERTEX_OUTPUT_STEREO
				};

				v2f vert(appdata_t v)
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_OUTPUT(v2f, o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					o.vertex = UnityObjectToClipPos(v.vertex);
					o.color = _TintColor * 2; // v.color;
					
					return o;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					fixed4 col = i.color; // *2 // *tex2D(_MainTex, i.texcoord);
					col.a = saturate(col.a); // alpha should not have double-brightness applied to it, but we can't fix that legacy behavior without breaking everyone's effects, so instead clamp the output to get sensible HDR behavior (case 967476)

					return col;
				}
				ENDCG
			}
		}
	}
}