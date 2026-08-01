using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Solas.Render.Vulkan.Extensions;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanComputePipeline : VulkanInjectable
{
    internal void Create()
    {
        var computeShaderCode = File.ReadAllBytes(@"D:\CS-Projects\Solas\SolasEngine\Render\Shaders\compute.spv");
        var shaderModule = ShaderModule.Create(Ctx, computeShaderCode);

        PipelineShaderStageCreateInfo computeShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("compMain")
        };

        fixed (DescriptorSetLayout* pSetLayout = &Ctx.ComputeDescriptorSetLayout)
        {
            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = 1,
                PSetLayouts = pSetLayout,
                PushConstantRangeCount = 0,
                PPushConstantRanges = null
            };

            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, &pipelineLayoutInfo, null, out Ctx.ComputePipelineLayout) !=
                Result.Success)
            {
                throw new Exception("Failed to create compute pipeline layout!");
            }
        }

        ComputePipelineCreateInfo pipelineInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = computeShaderStageInfo,
            Layout = Ctx.ComputePipelineLayout
        };

        if (Ctx.Vk!.CreateComputePipelines(Ctx.Device, default, 1, &pipelineInfo, null, out Ctx.ComputePipeline) !=
            Result.Success)
        {
            throw new Exception("Failed to create compute pipeline!");
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, shaderModule, null);

        SilkMarshal.Free((nint)computeShaderStageInfo.PName);
    }
}