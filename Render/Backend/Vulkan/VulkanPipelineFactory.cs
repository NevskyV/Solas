using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Solas.Render.Components;
using Solas.Render.Vulkan.Extensions;
using ShaderModule = Silk.NET.Vulkan.ShaderModule;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanPipelineFactory : VulkanInjectable
{
    private readonly Dictionary<string, VulkanMaterialPipeline> _pipelineCache = new();

    internal VulkanMaterialPipeline GetOrCreatePipeline(Material? material)
    {
        var hash = material?.GetPipelineHash() ?? "Default";

        if (_pipelineCache.TryGetValue(hash, out var cachedPipeline))
        {
            return cachedPipeline;
        }

        var pipeline = CreatePipelineForMaterial(material, hash);
        _pipelineCache[hash] = pipeline;
        return pipeline;
    }

    private VulkanMaterialPipeline CreatePipelineForMaterial(Material? material, string hash)
    {
        byte[] spirvCode;
        if (material != null)
        {
            spirvCode = SlangMaterialCompiler.Instance.CompileToSpirv(material);
        }
        else
        {
            spirvCode = SlangMaterialCompiler.Instance.CompileToSpirv(new Material(Dimensions.ThreeD));
        }

        var shaderModule = ShaderModule.Create(Ctx, spirvCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("vertexMain")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("fragmentMain")
        };

        PipelineShaderStageCreateInfo[] shaderStages = [vertShaderStageInfo, fragShaderStageInfo];

        DescriptorSetLayout[] setLayouts = [Ctx.LightingGlobalSet0Layout, Ctx.DescriptorSetLayout];

        var bindingDescription = Vertex.GetBindingDescription();
        var attributeDescriptions = Vertex.GetAttributeDescriptions();
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];

        PipelineLayout pipelineLayout;
        Pipeline graphicsPipeline;

        fixed (DynamicState* pDynamicStates = dynamicStates)
        fixed (PipelineShaderStageCreateInfo* pStages = shaderStages)
        fixed (VertexInputAttributeDescription* attributeDescriptionsPtr = attributeDescriptions)
        fixed (DescriptorSetLayout* pDescriptorSetLayout = setLayouts)
        {
            PipelineVertexInputStateCreateInfo vertexInputInfo = new()
            {
                SType = StructureType.PipelineVertexInputStateCreateInfo,
                VertexBindingDescriptionCount = 1,
                PVertexBindingDescriptions = &bindingDescription,
                VertexAttributeDescriptionCount = (uint)attributeDescriptions.Length,
                PVertexAttributeDescriptions = attributeDescriptionsPtr
            };

            PipelineInputAssemblyStateCreateInfo inputAssembly = new()
            {
                SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                Topology = PrimitiveTopology.TriangleList,
                PrimitiveRestartEnable = false,
            };

            PipelineDynamicStateCreateInfo dynamicState = new()
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = pDynamicStates
            };

            PipelineViewportStateCreateInfo viewportState = new()
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };

            PipelineDepthStencilStateCreateInfo depthStencil = new()
            {
                SType = StructureType.PipelineDepthStencilStateCreateInfo,
                DepthTestEnable = true,
                DepthWriteEnable = true,
                DepthCompareOp = CompareOp.Less,
                DepthBoundsTestEnable = false,
                StencilTestEnable = false
            };

            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = (PolygonMode)Ctx.Settings.PolygonMode,
                LineWidth = 1.0f,
                CullMode = CullModeFlags.None,
                FrontFace = FrontFace.CounterClockwise,
                DepthBiasEnable = false,
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = Ctx.MsaaSamples,
            };

            PipelineColorBlendAttachmentState colorBlendAttachment = new()
            {
                ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit |
                                 ColorComponentFlags.ABit,
                BlendEnable = true,
                SrcColorBlendFactor = BlendFactor.SrcAlpha,
                DstColorBlendFactor = BlendFactor.OneMinusSrcAlpha,
                ColorBlendOp = BlendOp.Add,
                AlphaBlendOp = BlendOp.Add,
                SrcAlphaBlendFactor = BlendFactor.One,
                DstAlphaBlendFactor = BlendFactor.Zero,
            };

            PipelineColorBlendStateCreateInfo colorBlending = new()
            {
                SType = StructureType.PipelineColorBlendStateCreateInfo,
                LogicOpEnable = false,
                LogicOp = LogicOp.Copy,
                AttachmentCount = 1,
                PAttachments = &colorBlendAttachment,
            };

            PipelineLayoutCreateInfo pipelineLayoutInfo = new()
            {
                SType = StructureType.PipelineLayoutCreateInfo,
                SetLayoutCount = (uint)setLayouts.Length,
                PSetLayouts = pDescriptorSetLayout,
                PushConstantRangeCount = 0,
            };

            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, in pipelineLayoutInfo, null, out pipelineLayout) !=
                Result.Success)
            {
                throw new Exception("failed to create pipeline layout!");
            }

            var colorFormat = Ctx.SwapChainSurfaceFormat.Format;
            PipelineRenderingCreateInfo pipelineRenderingCreateInfo = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &colorFormat,
                DepthAttachmentFormat = Ctx.DepthFormat,
                PNext = null
            };

            GraphicsPipelineCreateInfo pipelineInfo = new()
            {
                SType = StructureType.GraphicsPipelineCreateInfo,
                PNext = &pipelineRenderingCreateInfo,
                StageCount = 2,
                PStages = pStages,
                PVertexInputState = &vertexInputInfo,
                PInputAssemblyState = &inputAssembly,
                PViewportState = &viewportState,
                PRasterizationState = &rasterizer,
                PMultisampleState = &multisampling,
                PColorBlendState = &colorBlending,
                PDynamicState = &dynamicState,
                PDepthStencilState = &depthStencil,
                Layout = pipelineLayout,
                RenderPass = default
            };

            if (Ctx.Vk!.CreateGraphicsPipelines(Ctx.Device, default, 1, in pipelineInfo, null, out graphicsPipeline) !=
                Result.Success)
            {
                throw new Exception("failed to create graphics pipeline!");
            }
        }

        Ctx.Vk!.DestroyShaderModule(Ctx.Device, shaderModule, null);
        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);

        return new VulkanMaterialPipeline
        {
            Pipeline = graphicsPipeline,
            Layout = pipelineLayout,
            Hash = hash
        };
    }

    internal void Dispose()
    {
        foreach (var pipeline in _pipelineCache.Values)
        {
            pipeline.Dispose(Ctx.Vk!, Ctx.Device);
        }

        _pipelineCache.Clear();
    }
}