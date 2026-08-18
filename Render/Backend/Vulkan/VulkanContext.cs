using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Solas.Render.Data;
using Solas.Render.Logics;
using Solas.Render.Settings;
using Solas.Render.Vulkan.Extensions;
using Solas.Transform;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Vulkan;

internal sealed unsafe class VulkanContext(IWindow window) : IDisposable
{
    internal GraphicsSettings Settings = null!;
    internal TransformData CameraTransform = null!;
    internal CameraData CameraData = null!;

    internal uint FrameIndex;
    internal bool FrameBufferResized;

    internal readonly string[] RequiredDeviceExtensions =
    [
        KhrSwapchain.ExtensionName
    ];

    internal readonly string[] ValidationLayers =
    [
        "VK_LAYER_KHRONOS_validation"
    ];

    internal readonly IWindow? Window = window;
    internal Vk? Vk;

    internal Instance Instance;

    internal ExtDebugUtils? DebugUtils;
    internal DebugUtilsMessengerEXT DebugMessenger;

    internal KhrSurface? KhrSurface;
    internal SurfaceKHR Surface;

    internal PhysicalDevice PhysicalDevice;
    internal Device Device;

    internal Queue GraphicsQueue;

    internal KhrSwapchain? KhrSwapChain;
    internal SwapchainKHR SwapChain;
    internal Image[]? SwapChainImages;
    internal Extent2D SwapChainExtent;
    internal SurfaceFormatKHR SwapChainSurfaceFormat;
    internal ImageView[]? SwapChainImageViews;

    internal DescriptorSetLayout DescriptorSetLayout;

    internal CommandPool CommandPool;
    internal CommandBuffer[]? CommandBuffers;

    internal Semaphore[]? PresentCompleteSemaphores;
    internal Semaphore[]? RenderFinishedSemaphores;
    internal Fence[]? InFlightFences;

    internal DescriptorPool DescriptorPool;

    internal Image DepthImage;
    internal DeviceMemory DepthImageMemory;
    internal ImageView DepthImageView;
    internal Format DepthFormat;
    internal VulkanDepthResources DepthResources = null!;

    internal SampleCountFlags MsaaSamples = SampleCountFlags.Count1Bit;
    internal Image ColorImage;
    internal DeviceMemory ColorImageMemory;
    internal ImageView ColorImageView;
    internal VulkanColorResources ColorResources = null!;

    internal VulkanShadowResources ShadowResources = null!;
    internal VulkanShadowRenderer ShadowRenderer = null!;
    internal Image ShadowDepthArrayImage;
    internal DeviceMemory ShadowDepthArrayMemory;
    internal ImageView ShadowDepthArrayView;
    internal ImageView[] ShadowDepthLayerViews = [];
    internal Image PointShadowCubeArrayImage;
    internal DeviceMemory PointShadowCubeArrayMemory;
    internal ImageView PointShadowCubeArrayView;
    internal ImageView[] PointShadowCubeViews = [];
    internal ImageView[] PointShadowFaceViews = [];
    internal Sampler ShadowSampler;
    internal Buffer[] ShadowMatrixBuffers = [];
    internal DeviceMemory[] ShadowMatrixBuffersMemory = [];
    internal uint ShadowMatrixCapacity;
    internal bool ShadowImagesInitialized;
    internal Pipeline ShadowSetupPipeline;
    internal PipelineLayout ShadowSetupPipelineLayout;
    internal Pipeline ShadowDepthRigidPipeline;
    internal PipelineLayout ShadowDepthRigidPipelineLayout;

    internal readonly Dictionary<MeshRenderLogic, VulkanRenderData> RenderDataMap = new();
    internal readonly List<VulkanRenderData> RenderData = [];
    internal VulkanResourceManager ResourceManager = null!;

    internal Image ScreenPingImage;
    internal DeviceMemory ScreenPingImageMemory;
    internal ImageView ScreenPingImageView;

    internal Image ScreenPongImage;
    internal DeviceMemory ScreenPongImageMemory;
    internal ImageView ScreenPongImageView;

    internal Sampler ScreenSampler;
    internal Buffer[] ScreenUniformBuffers = [];
    internal DeviceMemory[] ScreenUniformBuffersMemory = [];
    internal DescriptorSet[][] ScreenDescriptorSets = [];
    internal VulkanMaterialPipeline[] ScreenPipelines = [];

    internal Pipeline ComputePipeline;
    internal PipelineLayout ComputePipelineLayout;
    internal DescriptorSetLayout ComputeDescriptorSetLayout;

    internal VulkanLightingResources LightingResources = null!;
    internal VulkanLightingDescriptors LightingDescriptors = null!;
    internal VulkanPipelineFactory PipelineFactory = null!;
    internal Buffer[] LightBuffers = [];
    internal DeviceMemory[] LightBuffersMemory = [];
    internal Buffer[] LightUploadBuffers = [];
    internal DeviceMemory[] LightUploadBuffersMemory = [];
    internal void*[] LightUploadBuffersMappedPointers = [];

    internal Buffer[] GlobalLightIndicesBuffers = [];
    internal DeviceMemory[] GlobalLightIndicesBuffersMemory = [];

    internal Buffer[] TileGridBuffers = [];
    internal DeviceMemory[] TileGridBuffersMemory = [];

    internal Buffer[] GlobalIndexCounterBuffers = [];
    internal DeviceMemory[] GlobalIndexCounterBuffersMemory = [];

    internal Buffer[] FrameParamsBuffers = [];
    internal DeviceMemory[] FrameParamsBuffersMemory = [];
    internal void*[] FrameParamsMappedPointers = [];

    internal Buffer[] IndirectDrawBuffers = [];
    internal DeviceMemory[] IndirectDrawBuffersMemory = [];
    internal void*[] IndirectDrawMappedPointers = [];

    internal DescriptorSetLayout LightingGlobalSet0Layout;
    internal DescriptorSetLayout LightingFrameSet1Layout;

    internal DescriptorSet[] LightingGlobalSetsSet0 = [];
    internal DescriptorSet[] LightingFrameSetsSet1 = [];

    internal Pipeline LightCullingPipeline;
    internal PipelineLayout LightCullingPipelineLayout;

    internal Pipeline GeometryCullingPipeline;
    internal PipelineLayout GeometryCullingPipelineLayout;
    internal DescriptorSetLayout GeometryCullingSet0Layout;
    internal DescriptorSet[] GeometryCullingSetsSet0 = [];
    internal Buffer[] ObjectDataBuffers = [];
    internal DeviceMemory[] ObjectDataBuffersMemory = [];
    internal void*[] ObjectDataMappedPointers = [];

    internal Matrix4x4 CameraViewMatrix;
    internal Matrix4x4 CameraProjectionMatrix;

    internal Image ResolveImage;
    internal DeviceMemory ResolveImageMemory;
    internal ImageView ResolveImageView;

    internal Extent2D RenderExtent => new(
        (uint)MathF.Max(1.0f, SwapChainExtent.Width * Settings.RenderScale),
        (uint)MathF.Max(1.0f, SwapChainExtent.Height * Settings.RenderScale)
    );

    public void Dispose()
    {
        if (ResolveImageView.Handle != 0)
        {
            Vk!.DestroyImageView(Device, ResolveImageView, null);
            Vk!.DestroyImage(Device, ResolveImage, null);
            Vk!.FreeMemory(Device, ResolveImageMemory, null);
        }

        if (LightCullingPipeline.Handle != 0)
        {
            Vk!.DestroyPipeline(Device, LightCullingPipeline, null);
            LightCullingPipeline = default;
        }

        if (LightCullingPipelineLayout.Handle != 0)
        {
            Vk!.DestroyPipelineLayout(Device, LightCullingPipelineLayout, null);
            LightCullingPipelineLayout = default;
        }

        if (GeometryCullingPipeline.Handle != 0)
        {
            Vk!.DestroyPipeline(Device, GeometryCullingPipeline, null);
            GeometryCullingPipeline = default;
        }

        if (GeometryCullingPipelineLayout.Handle != 0)
        {
            Vk!.DestroyPipelineLayout(Device, GeometryCullingPipelineLayout, null);
            GeometryCullingPipelineLayout = default;
        }

        if (GeometryCullingSet0Layout.Handle != 0)
        {
            Vk!.DestroyDescriptorSetLayout(Device, GeometryCullingSet0Layout, null);
            GeometryCullingSet0Layout = default;
        }

        if (ComputePipeline.Handle != 0)
        {
            Vk!.DestroyPipeline(Device, ComputePipeline, null);
            ComputePipeline = default;
        }

        if (ComputePipelineLayout.Handle != 0)
        {
            Vk!.DestroyPipelineLayout(Device, ComputePipelineLayout, null);
            ComputePipelineLayout = default;
        }

        if (LightingGlobalSet0Layout.Handle != 0)
        {
            Vk!.DestroyDescriptorSetLayout(Device, LightingGlobalSet0Layout, null);
            LightingGlobalSet0Layout = default;
        }

        if (LightingFrameSet1Layout.Handle != 0)
        {
            Vk!.DestroyDescriptorSetLayout(Device, LightingFrameSet1Layout, null);
            LightingFrameSet1Layout = default;
        }

        if (ComputeDescriptorSetLayout.Handle != 0)
        {
            Vk!.DestroyDescriptorSetLayout(Device, ComputeDescriptorSetLayout, null);
            ComputeDescriptorSetLayout = default;
        }

        for (var i = 0; i < Settings.MaxFramesInFlight; i++)
        {
            Vk!.DestroyBuffer(Device, TileGridBuffers[i], null);
            Vk!.FreeMemory(Device, TileGridBuffersMemory[i], null);

            if (LightBuffersMemory != null && i < LightBuffersMemory.Length && LightBuffersMemory[i].Handle != 0)
            {
                Vk!.DestroyBuffer(Device, LightBuffers[i], null);
                Vk!.FreeMemory(Device, LightBuffersMemory[i], null);
            }

            if (LightUploadBuffersMemory != null && i < LightUploadBuffersMemory.Length &&
                LightUploadBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, LightUploadBuffersMemory[i]);
                Vk!.DestroyBuffer(Device, LightUploadBuffers[i], null);
                Vk!.FreeMemory(Device, LightUploadBuffersMemory[i], null);
            }

            if (GlobalIndexCounterBuffersMemory != null && i < GlobalIndexCounterBuffersMemory.Length &&
                GlobalIndexCounterBuffersMemory[i].Handle != 0)
            {
                Vk!.DestroyBuffer(Device, GlobalIndexCounterBuffers[i], null);
                Vk!.FreeMemory(Device, GlobalIndexCounterBuffersMemory[i], null);
            }

            if (FrameParamsBuffersMemory != null && i < FrameParamsBuffersMemory.Length &&
                FrameParamsBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, FrameParamsBuffersMemory[i]);
                Vk!.DestroyBuffer(Device, FrameParamsBuffers[i], null);
                Vk!.FreeMemory(Device, FrameParamsBuffersMemory[i], null);
            }

            if (GlobalLightIndicesBuffersMemory != null && i < GlobalLightIndicesBuffersMemory.Length &&
                GlobalLightIndicesBuffersMemory[i].Handle != 0)
            {
                Vk!.DestroyBuffer(Device, GlobalLightIndicesBuffers[i], null);
                Vk!.FreeMemory(Device, GlobalLightIndicesBuffersMemory[i], null);
            }

            if (IndirectDrawBuffersMemory != null && i < IndirectDrawBuffersMemory.Length &&
                IndirectDrawBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, IndirectDrawBuffersMemory[i]);
                Vk!.DestroyBuffer(Device, IndirectDrawBuffers[i], null);
                Vk!.FreeMemory(Device, IndirectDrawBuffersMemory[i], null);
            }

            if (ObjectDataBuffersMemory != null && i < ObjectDataBuffersMemory.Length &&
                ObjectDataBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, ObjectDataBuffersMemory[i]);
                Vk!.DestroyBuffer(Device, ObjectDataBuffers[i], null);
                Vk!.FreeMemory(Device, ObjectDataBuffersMemory[i], null);
            }
        }

        foreach (var renderData in RenderData)
        {
            for (var i = 0; i < renderData.UniformBuffersMemory.Length; i++)
            {
                if (renderData.UniformBuffersMemory[i].Handle != 0)
                {
                    Vk!.DestroyBuffer(Device, renderData.UniformBuffers![i], null);
                    Vk!.FreeMemory(Device, renderData.UniformBuffersMemory[i], null);
                }
            }
        }

        RenderDataMap.Clear();
        RenderData.Clear();

        Array.Clear(LightUploadBuffersMappedPointers, 0, LightUploadBuffersMappedPointers.Length);
        Array.Clear(FrameParamsMappedPointers, 0, FrameParamsMappedPointers.Length);

        ShadowRenderer.Dispose();
        ShadowResources.Dispose();
        ResourceManager.Dispose();

        Vk!.DestroyDescriptorSetLayout(Device, DescriptorSetLayout, null);
        Vk!.DestroyDescriptorPool(Device, DescriptorPool, null);

        for (var i = 0; i < RenderFinishedSemaphores!.Length; i++)
        {
            Vk!.DestroySemaphore(Device, RenderFinishedSemaphores![i], null);
        }

        for (var i = 0; i < Settings.MaxFramesInFlight; i++)
        {
            Vk!.DestroySemaphore(Device, PresentCompleteSemaphores![i], null);
            Vk!.DestroyFence(Device, InFlightFences![i], null);
        }

        if (CommandPool.Handle != 0)
        {
            Vk!.DestroyCommandPool(Device, CommandPool, null);
        }

        Vk!.DestroyDevice(Device, null);

        if (Settings.EnableValidationLayers)
        {
            DebugUtils!.DestroyDebugUtilsMessenger(Instance, DebugMessenger, null);
        }

        KhrSurface!.DestroySurface(Instance, Surface, null);
        Vk!.DestroyInstance(Instance, null);
        Vk!.Dispose();
    }
}