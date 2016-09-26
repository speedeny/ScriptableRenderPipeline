#ifndef UNITY_MATERIAL_DISNEYGGX_INCLUDED
#define UNITY_MATERIAL_DISNEYGGX_INCLUDED

//-----------------------------------------------------------------------------
// SurfaceData and BSDFData
//-----------------------------------------------------------------------------

// Main structure that store the user data (i.e user input of master node in material graph)
struct SurfaceData
{
	float3	diffuseColor;
	float	occlusion;

	float3	specularColor;
	float	smoothness;

	float3	normal;		// normal in world space
};

struct BSDFData
{
	float3	diffuseColor;
	float	occlusion;

	float3	fresnel0;
	float	perceptualRoughness;

	float3	normalWS;
	float	roughness; 
};

//-----------------------------------------------------------------------------
// conversion function for forward and deferred
//-----------------------------------------------------------------------------

BSDFData ConvertSurfaceDataToBSDFData(SurfaceData data)
{
	BSDFData output;

	output.diffuseColor = data.diffuseColor;
	output.occlusion = data.occlusion;

	output.fresnel0 = data.specularColor;	
	output.perceptualRoughness = SmoothnessToPerceptualRoughness(data.smoothness);

	output.normalWS = data.normal;
	output.roughness = PerceptualRoughnessToRoughness(output.perceptualRoughness);
	

	return output;
}

// This will encode UnityStandardData into GBuffer
void EncodeIntoGBuffer(SurfaceData data, out float4 outGBuffer0, out float4 outGBuffer1, out float4 outGBuffer2)
{
	// RT0: diffuse color (rgb), occlusion (a) - sRGB rendertarget
	outGBuffer0 = float4(data.diffuseColor, data.occlusion);

	// RT1: spec color (rgb), perceptual roughness (a) - sRGB rendertarget
	outGBuffer1 = float4(data.specularColor, SmoothnessToPerceptualRoughness(data.smoothness));

	// RT2: normal (rgb), --unused, very low precision-- (a) 
	outGBuffer2 = float4(PackNormalCartesian(data.normal), 1.0f);
}

// This decode the Gbuffer in a BSDFData struct
BSDFData DecodeFromGBuffer(float4 inGBuffer0, float4 inGBuffer1, float4 inGBuffer2)
{
	BSDFData output;

	output.diffuseColor = inGBuffer0.rgb;
	output.occlusion = inGBuffer0.a;

	output.fresnel0 = inGBuffer1.rgb;
	output.perceptualRoughness = inGBuffer1.a;

	output.normalWS = UnpackNormalCartesian(inGBuffer2.rgb);
	output.roughness = PerceptualRoughnessToRoughness(output.perceptualRoughness);

	return output;
}

//-----------------------------------------------------------------------------
// EvaluateBSDF functions for each light type
//-----------------------------------------------------------------------------

void EvaluateBSDF_Punctual(	float3 V, float3 positionWS, PunctualLightData light, BSDFData material,
							out float4 diffuseLighting,
							out float4 specularLighting)
{
	float3 unL = light.positionWS - positionWS;
	float3 L = normalize(unL);

	// Always done, directional have it neutral
	float attenuation = GetDistanceAttenuation(unL, light.invSqrAttenuationRadius);
	// Always done, point and dir have it neutral
	attenuation *= GetAngleAttenuation(L, light.forward, light.angleScale, light.angleOffset);
	float illuminance = saturate(dot(material.normalWS, L)) * attenuation;

	diffuseLighting = float4(0.0f, 0.0f, 0.0f, 1.0f);
	specularLighting = float4(0.0f, 0.0f, 0.0f, 1.0f);

	if (illuminance > 0.0f)
	{
		float NdotV = abs(dot(material.normalWS, V)) + 1e-5f; // TODO: check Eric idea about doing that when writting into the GBuffer (with our forward decal)
		float3 H = normalize(V + L);
		float LdotH = saturate(dot(L, H));
		float NdotH = saturate(dot(material.normalWS, H));
		float NdotL = saturate(dot(material.normalWS, L));
		float3 F = F_Schlick(material.fresnel0, LdotH);
		float Vis = V_SmithJointGGX(NdotL, NdotV, material.roughness);
		float D = D_GGX(NdotH, material.roughness);
		specularLighting.rgb = F * Vis * D;
		float disneyDiffuse = DisneyDiffuse(NdotV, NdotL, LdotH, material.perceptualRoughness);
		diffuseLighting.rgb = material.diffuseColor * disneyDiffuse;

		diffuseLighting.rgb *= light.color * illuminance;
		specularLighting.rgb *= light.color * illuminance;
	}
}

#endif // UNITY_MATERIAL_DISNEYGGX_INCLUDED