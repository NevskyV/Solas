using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanComputePipeline : VulkanInjectable
{
    internal void Create()
    {
        byte[] computeCode = File.ReadAllBytes(@"D:\CS-Projects\Solas\SolasEngine\Render\Shaders\light_culling.spv");
        ShaderModule shaderModule = CreateShaderModule(computeCode);

        DescriptorSetLayout[] setLayouts = [Ctx.LightingGlobalSet0Layout, Ctx.LightingFrameSet1Layout];

        fixed (DescriptorSetLayout* pSetLayouts = setLayouts)
        {
            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)setLayouts.Length,
                PSetLayouts = pSetLayouts
            };

            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, &pipelineLayoutInfo, null,
                    out Ctx.LightCullingPipelineLayout) != Result.Success)
            {
                throw new Exception("failed to create compute pipeline layout!");
            }
        }

        PipelineShaderStageCreateInfo stageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        ComputePipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = stageInfo,
            Layout = Ctx.LightCullingPipelineLayout
        };

        if (Ctx.Vk!.CreateComputePipelines(Ctx.Device, default, 1, &pipelineInfo, null, out Ctx.LightCullingPipeline) !=
            Result.Success)
        {
            throw new Exception("failed to create compute pipeline!");
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, shaderModule, null);
        SilkMarshal.Free((nint)stageInfo.PName);
    }

    private ShaderModule CreateShaderModule(byte[] code)
    {
        fixed (byte* pCode = code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
                PCode = (uint*)pCode
            };

            if (Ctx.Vk!.CreateShaderModule(Ctx.Device, &createInfo, null, out ShaderModule shaderModule) !=
                Result.Success)
            {
                throw new Exception("failed to create shader module!");
            }

            return shaderModule;
        }
    }
}