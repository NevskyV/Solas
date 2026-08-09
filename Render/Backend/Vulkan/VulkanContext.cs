using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Solas.Render.Components;
using Solas.Render.Data;
using Solas.Render.Logics;
using Solas.Render.Settings;
using Solas.Render.Vulkan.Components;
using Solas.Render.Vulkan.Extensions;
using Solas.Transform;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Vulkan;

internal sealed unsafe class VulkanContext(IWindow window) : IDisposable
{
    internal GraphicsSettings Settings;
    internal TransformData CameraTransform;
    internal CameraData CameraData;

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
    internal PipelineLayout PipelineLayout;
    internal Pipeline GraphicsPipeline;

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
    internal VulkanDepthResources DepthResources;

    internal SampleCountFlags MsaaSamples = SampleCountFlags.Count1Bit;
    internal Image ColorImage;
    internal DeviceMemory ColorImageMemory;
    internal ImageView ColorImageView;
    internal VulkanColorResources ColorResources;

    internal Dictionary<MeshRenderLogic, VulkanRenderData> RenderDataMap = new();
    internal List<VulkanRenderData> RenderData = [];
    internal VulkanResourceManager ResourceManager;

    internal Sampler ScreenSampler;
    internal Buffer[] ScreenUniformBuffers = [];
    internal DeviceMemory[] ScreenUniformBuffersMemory = [];
    internal DescriptorSet[] ScreenDescriptorSets = [];
    internal VulkanMaterialPipeline ScreenPipeline;

    internal Buffer[] ShaderStorageBuffers;
    internal DeviceMemory[] ShaderStorageBuffersMemory;
    internal Pipeline ComputePipeline;
    internal PipelineLayout ComputePipelineLayout;
    internal DescriptorSet[] ComputeDescriptorSets;
    internal DescriptorSetLayout ComputeDescriptorSetLayout;

    internal VulkanLightingResources LightingResources;
    internal VulkanPipelineFactory PipelineFactory;
    internal Buffer[] LightBuffers = [];
    internal DeviceMemory[] LightBuffersMemory = [];
    internal void*[] LightBuffersMappedPointers = [];

    internal Buffer[] GlobalLightIndicesBuffers = [];
    internal DeviceMemory[] GlobalLightIndicesBuffersMemory = [];

    internal Buffer[] TileGridBuffers = [];
    internal DeviceMemory[] TileGridBuffersMemory = [];

    internal Buffer[] GlobalIndexCounterBuffers = [];
    internal DeviceMemory[] GlobalIndexCounterBuffersMemory = [];
    internal void*[] GlobalIndexCounterMappedPointers = [];

    internal Buffer[] FrameParamsBuffers = [];
    internal DeviceMemory[] FrameParamsBuffersMemory = [];
    internal void*[] FrameParamsMappedPointers = [];

    internal DescriptorSetLayout LightingGlobalSet0Layout;
    internal DescriptorSetLayout LightingFrameSet1Layout;

    internal DescriptorSet[] LightingGlobalSetsSet0 = [];
    internal DescriptorSet[] LightingFrameSetsSet1 = [];

    internal Pipeline LightCullingPipeline;
    internal PipelineLayout LightCullingPipelineLayout;

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

        for (int i = 0; i < Settings.MaxFramesInFlight; i++)
        {
            Vk!.DestroyBuffer(Device, TileGridBuffers[i], null);
            Vk!.FreeMemory(Device, TileGridBuffersMemory[i], null);

            if (LightBuffersMemory != null && i < LightBuffersMemory.Length && LightBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, LightBuffersMemory[i]);
                Vk!.DestroyBuffer(Device, LightBuffers[i], null);
                Vk!.FreeMemory(Device, LightBuffersMemory[i], null);
            }

            if (GlobalIndexCounterBuffersMemory != null && i < GlobalIndexCounterBuffersMemory.Length &&
                GlobalIndexCounterBuffersMemory[i].Handle != 0)
            {
                Vk!.UnmapMemory(Device, GlobalIndexCounterBuffersMemory[i]);
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

            if (ShaderStorageBuffersMemory != null && i < ShaderStorageBuffersMemory.Length &&
                ShaderStorageBuffersMemory[i].Handle != 0)
            {
                Vk!.DestroyBuffer(Device, ShaderStorageBuffers[i], null);
                Vk!.FreeMemory(Device, ShaderStorageBuffersMemory[i], null);
            }
        }

        foreach (var renderData in RenderData)
        {
            for (int i = 0; i < renderData.UniformBuffersMemory.Length; i++)
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

        Array.Clear(LightBuffersMappedPointers, 0, LightBuffersMappedPointers.Length);
        Array.Clear(GlobalIndexCounterMappedPointers, 0, GlobalIndexCounterMappedPointers.Length);
        Array.Clear(FrameParamsMappedPointers, 0, FrameParamsMappedPointers.Length);

        ResourceManager.Dispose();

        Vk!.DestroyDescriptorSetLayout(Device, DescriptorSetLayout, null);
        Vk!.DestroyDescriptorPool(Device, DescriptorPool, null);

        for (int i = 0; i < RenderFinishedSemaphores!.Length; i++)
        {
            Vk!.DestroySemaphore(Device, RenderFinishedSemaphores![i], null);
        }

        for (int i = 0; i < Settings.MaxFramesInFlight; i++)
        {
            Vk!.DestroySemaphore(Device, PresentCompleteSemaphores![i], null);
            Vk!.DestroyFence(Device, InFlightFences![i], null);
        }

        if (CommandPool.Handle != 0)
        {
            Vk!.DestroyCommandPool(Device, CommandPool, null);
        }

        if (GraphicsPipeline.Handle != 0)
        {
            Vk!.DestroyPipeline(Device, GraphicsPipeline, null);
        }

        if (PipelineLayout.Handle != 0)
        {
            Vk!.DestroyPipelineLayout(Device, PipelineLayout, null);
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