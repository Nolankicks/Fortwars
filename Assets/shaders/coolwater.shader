HEADER
{
	Description = "";
}

FEATURES
{
	#include "common/features.hlsl"
}

MODES
{
	VrForward();
	Depth(); 
	ToolsVis( S_MODE_TOOLS_VIS );
	ToolsWireframe( "vr_tools_wireframe.shader" );
	ToolsShadingComplexity( "tools_shading_complexity.shader" );
}

COMMON
{
	#include "common/shared.hlsl"
	#include "procedural.hlsl"

	#define CUSTOM_MATERIAL_INPUTS
}

struct VertexInput
{
	#include "common/vertexinput.hlsl"
};

struct PixelInput
{
	#include "common/pixelinput.hlsl"
	float4 vScreenSpacePosition : TEXCOORD13;
};

VS
{
	#include "common/vertex.hlsl"

	float4 ScreenSpacePosition(float4 position)
	{
		float4 v = position * 0.5;
		v.xy += v.w;
		v.zw = position.zw;
		return v;
	}

	PixelInput MainVs( VertexInput v )
	{
		PixelInput i = ProcessVertex( v );
		PixelInput o = FinalizeVertex( i );
		o.vScreenSpacePosition = ScreenSpacePosition( i.vPositionPs );
		return o;
	}
}

PS
{
	#include "common/pixel.hlsl"
	
	BoolAttribute( bWantsFBCopyTexture, true );
	BoolAttribute( translucent, true );

	SamplerState g_sSampler0 < Filter( ANISO ); AddressU( WRAP ); AddressV( WRAP ); >;

	Texture2D g_tReflection < Attribute( "Reflection" ); SrgbRead( true ); >;
	Texture2D g_tRefraction < Attribute( "FrameBufferCopyTexture" ); SrgbRead( true ); >;

	CreateInputTexture2D( Normal, Srgb, 8, "None", "_normal", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tNormal < Channel( RGBA, Box( Normal ), Srgb ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	CreateInputTexture2D( Fresnel, Srgb, 8, "None", "_color", ",0/,0/0", Default4( 1.00, 1.00, 1.00, 1.00 ) );
	Texture2D g_tFresnel < Channel( RGBA, Box( Fresnel ), Srgb ); OutputFormat( DXT5 ); SrgbRead( false ); >;

	float4 MainPs( PixelInput i ) : SV_Target0
	{
		float2 wave = i.vTextureCoords.xy * 1;
		float3 normal1 = Tex2DS( g_tNormal, g_sSampler0, wave + (g_flTime * 0.025) ).xyz * 2.0 - 1.0;
		float3 normal2 = Tex2DS( g_tNormal, g_sSampler0, wave + 0.5 + (g_flTime * 0.04) ).xyz * 2.0 - 1.0;
		float3 normal = (normal1 + normal2) * 0.5;

		float4 uv1 = i.vScreenSpacePosition;
		uv1.xy += normal.xy * 15;
		uv1.xy *= -1.0;

		float2 uv2 = i.vPositionSs.xy * g_vFrameBufferCopyInvSizeAndUvScale.xy;
		uv2.xy -= normal.xy * 0.5;

		float3 reflectionColor = Tex2DS( g_tReflection, g_sSampler0, uv1.xy / uv1.w ).rgb;
		float3 refractionColor = Tex2DS( g_tRefraction, g_sSampler0, uv2 ).rgb * float3( 0.5, 0.9, 1.0 );

		float fresnelFactor = dot( normalize( -i.vPositionWithOffsetWs.xyz ), normal );
		fresnelFactor = Tex2DS( g_tFresnel, g_sSampler0, float2( fresnelFactor, fresnelFactor ) ).r;

		float3 finalColor = lerp( refractionColor, reflectionColor, fresnelFactor );

		float3 blueTint = float3(0.45, 0.8, 1.0);
		
        finalColor *= blueTint;


		return float4( finalColor, 1.0 );
	}
}