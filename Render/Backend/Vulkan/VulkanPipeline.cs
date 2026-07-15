using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanPipeline : VulkanInjectable
{
    internal void CreateGraphicsPipeline()
    {
        var vertShaderCode = File.ReadAllBytes(@"D:\CS-Projects\Solas\SolasEngine\Render\Shaders\slang.spv");

        var shaderModule = CreateShaderModule(vertShaderCode);

        PipelineShaderStageCreateInfo vertShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.VertexBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("vertMain")
        };

        PipelineShaderStageCreateInfo fragShaderStageInfo = new()
        {
            SType = StructureType.PipelineShaderStageCreateInfo,
            Stage = ShaderStageFlags.FragmentBit,
            Module = shaderModule,
            PName = (byte*)SilkMarshal.StringToPtr("fragMain")
        };

        PipelineShaderStageCreateInfo[] shaderStages = [vertShaderStageInfo, fragShaderStageInfo];
        
        PipelineVertexInputStateCreateInfo vertexInputInfo = new()
        {
            SType = StructureType.PipelineVertexInputStateCreateInfo,
            VertexBindingDescriptionCount = 0,
            VertexAttributeDescriptionCount = 0,
        };
        
        PipelineInputAssemblyStateCreateInfo inputAssembly = new()
        {
            SType = StructureType.PipelineInputAssemblyStateCreateInfo,
            Topology = PrimitiveTopology.TriangleList,
            PrimitiveRestartEnable = false,
        };
        
        DynamicState[] dynamicStates = [DynamicState.Viewport, DynamicState.Scissor];
        fixed (DynamicState* pDynamicStates = dynamicStates)
        fixed (PipelineShaderStageCreateInfo* pStages = shaderStages)
        {
            var dynamicState = new PipelineDynamicStateCreateInfo
            {
                SType = StructureType.PipelineDynamicStateCreateInfo,
                DynamicStateCount = (uint)dynamicStates.Length,
                PDynamicStates = pDynamicStates
            };


            var viewportState = new PipelineViewportStateCreateInfo
            {
                SType = StructureType.PipelineViewportStateCreateInfo,
                ViewportCount = 1,
                ScissorCount = 1
            };

            PipelineRasterizationStateCreateInfo rasterizer = new()
            {
                SType = StructureType.PipelineRasterizationStateCreateInfo,
                DepthClampEnable = false,
                RasterizerDiscardEnable = false,
                PolygonMode = PolygonMode.Fill,
                LineWidth = 1,
                CullMode = CullModeFlags.BackBit,
                FrontFace = FrontFace.Clockwise,
                DepthBiasEnable = false,
            };

            PipelineMultisampleStateCreateInfo multisampling = new()
            {
                SType = StructureType.PipelineMultisampleStateCreateInfo,
                SampleShadingEnable = false,
                RasterizationSamples = SampleCountFlags.Count1Bit,
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
                SetLayoutCount = 0,
                PushConstantRangeCount = 0,
            };

            if (Ctx.Vk!.CreatePipelineLayout(Ctx.Device, in pipelineLayoutInfo, null, out Ctx.PipelineLayout) != Result.Success)
            {
                throw new Exception("failed to create pipeline layout!");
            }

            var colorFormat = Ctx.SwapChainSurfaceFormat.Format;
            PipelineRenderingCreateInfo pipelineRenderingCreateInfo = new()
            {
                SType = StructureType.PipelineRenderingCreateInfo,
                ColorAttachmentCount = 1,
                PColorAttachmentFormats = &colorFormat,
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
                Layout = Ctx.PipelineLayout,
                RenderPass = default
            };

            if (Ctx.Vk!.CreateGraphicsPipelines(Ctx.Device, default, 1, in pipelineInfo, null, out Ctx.GraphicsPipeline) !=
                Result.Success)
            {
                throw new Exception("failed to create graphics pipeline!");
            }
        }
        
        Ctx.Vk!.DestroyShaderModule(Ctx.Device, shaderModule, null);

        SilkMarshal.Free((nint)vertShaderStageInfo.PName);
        SilkMarshal.Free((nint)fragShaderStageInfo.PName);
    }
    
    private ShaderModule CreateShaderModule(byte[] code)
    {
        ShaderModuleCreateInfo createInfo = new()
        {
            SType = StructureType.ShaderModuleCreateInfo,
            CodeSize = (nuint)code.Length,
        };

        ShaderModule shaderModule;

        fixed (byte* codePtr = code)
        {
            createInfo.PCode = (uint*)codePtr;

            if (Ctx.Vk!.CreateShaderModule(Ctx.Device, in createInfo, null, out shaderModule) != Result.Success)
            {
                throw new Exception();
            }
        }

        return shaderModule;

    }
}