using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanTextureSampler : VulkanInjectable
{
    internal void Create()
    {
        Ctx.Vk!.GetPhysicalDeviceProperties(Ctx.PhysicalDevice, out var pProperties);
        SamplerCreateInfo samplerInfo = new()
        {
            SType = StructureType.SamplerCreateInfo,
            MagFilter = Filter.Linear,
            MinFilter = Filter.Linear,
            MipmapMode = SamplerMipmapMode.Linear,
            MipLodBias = 0f,
            MinLod = 0f,
            MaxLod = 0f,
            AddressModeU = SamplerAddressMode.Repeat,
            AddressModeV = SamplerAddressMode.Repeat,
            AddressModeW = SamplerAddressMode.Repeat,
            AnisotropyEnable = true,
            MaxAnisotropy = pProperties.Limits.MaxSamplerAnisotropy,
            CompareEnable = false,
            CompareOp = CompareOp.Always,
            BorderColor = BorderColor.FloatOpaqueBlack,
            UnnormalizedCoordinates = false
        };

        Ctx.Vk!.CreateSampler(Ctx.Device, &samplerInfo, null, out Ctx.TextureSampler);
    }
}