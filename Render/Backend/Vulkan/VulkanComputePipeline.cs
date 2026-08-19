using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanComputePipeline : VulkanInjectable
{
    internal void Create()
    {
        var assembly = typeof(VulkanComputePipeline).Assembly;
        using var stream = assembly.GetManifestResourceStream("Solas.Render.StandardShaders.Embedded.LightCulling.spv")
                           ?? throw new FileNotFoundException("Light culling shader resource not found.");

        var computeCode = new byte[stream.Length];
        stream.ReadExactly(computeCode);
        var shaderModule = CreateShaderModule(computeCode);

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

        var res =
            Ctx.Vk!.CreateComputePipelines(Ctx.Device, default, 1, &pipelineInfo, null, out Ctx.LightCullingPipeline);
        if (res != Result.Success)
        {
            throw new Exception($"failed to create compute pipeline! Result = {res}");
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, shaderModule, null);
        SilkMarshal.Free((nint)stageInfo.PName);

        using var binningStream =
            assembly.GetManifestResourceStream("Solas.Render.StandardShaders.Embedded.LightBinning.spv")
            ?? throw new FileNotFoundException("Light binning shader resource not found.");
        var binningCode = new byte[binningStream.Length];
        binningStream.ReadExactly(binningCode);
        var binningShaderModule = CreateShaderModule(binningCode);
        PipelineShaderStageCreateInfo binningStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = binningShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };
        ComputePipelineCreateInfo binningPipelineInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = binningStageInfo,
            Layout = Ctx.LightCullingPipelineLayout
        };
        var binningResult = Ctx.Vk!.CreateComputePipelines(
            Ctx.Device,
            default,
            1,
            &binningPipelineInfo,
            null,
            out Ctx.LightBinningPipeline);
        if (binningResult != Result.Success)
        {
            throw new Exception($"failed to create light binning compute pipeline! Result = {binningResult}");
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, binningShaderModule, null);
        SilkMarshal.Free((nint)binningStageInfo.PName);

        using var geomStream =
            assembly.GetManifestResourceStream("Solas.Render.StandardShaders.Embedded.GeometryCulling.spv")
            ?? throw new FileNotFoundException("Geometry culling shader resource not found.");

        var geomCode = new byte[geomStream.Length];
        geomStream.ReadExactly(geomCode);
        var geomShaderModule = CreateShaderModule(geomCode);

        DescriptorSetLayout[] geomSetLayouts = [Ctx.GeometryCullingSet0Layout, Ctx.LightingFrameSet1Layout];

        fixed (DescriptorSetLayout* pGeomSetLayouts = geomSetLayouts)
        {
            PipelineLayoutCreateInfo geomPipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)geomSetLayouts.Length,
                PSetLayouts = pGeomSetLayouts
            };

            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, &geomPipelineLayoutInfo, null,
                    out Ctx.GeometryCullingPipelineLayout) != Result.Success)
            {
                throw new Exception("failed to create geometry culling pipeline layout!");
            }
        }

        PipelineShaderStageCreateInfo geomStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.ComputeBit,
            Module = geomShaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("main")
        };

        ComputePipelineCreateInfo geomPipelineInfo = new()
        {
            SType = StructureType.ComputePipelineCreateInfo,
            Stage = geomStageInfo,
            Layout = Ctx.GeometryCullingPipelineLayout
        };

        var resGeom = Ctx.Vk!.CreateComputePipelines(Ctx.Device, default, 1, &geomPipelineInfo, null,
            out Ctx.GeometryCullingPipeline);
        if (resGeom != Result.Success)
        {
            throw new Exception($"failed to create geometry culling compute pipeline! Result = {resGeom}");
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, geomShaderModule, null);
        SilkMarshal.Free((nint)geomStageInfo.PName);
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

            if (Ctx.Vk!.CreateShaderModule(Ctx.Device, &createInfo, null, out var shaderModule) !=
                Result.Success)
            {
                throw new Exception("failed to create shader module!");
            }

            return shaderModule;
        }
    }
}