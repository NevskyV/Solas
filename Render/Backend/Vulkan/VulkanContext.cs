using System.Resources;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Solas.Render.Components;
using Solas.Render.Data;
using Solas.Render.Logics;
using Solas.Render.Vulkan.Extensions;
using Solas.Transform;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Vulkan;

internal sealed unsafe class VulkanContext(IWindow window) : IDisposable
{
    internal TransformData CameraTransform;
    internal CameraData CameraData;

    internal readonly uint MaxFramesInFlight = 2;
    internal uint FrameIndex;
    internal bool FrameBufferResized;

    internal readonly bool EnableValidationLayers = true;

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

    internal Buffer[] ShaderStorageBuffers;
    internal DeviceMemory[] ShaderStorageBuffersMemory;
    internal Pipeline ComputePipeline;
    internal PipelineLayout ComputePipelineLayout;
    internal DescriptorSet[] ComputeDescriptorSets;
    internal DescriptorSetLayout ComputeDescriptorSetLayout;

    public void Dispose()
    {
        ResourceManager.Dispose();

        Vk!.DestroyImageView(Device, ColorImageView, null);
        Vk!.DestroyImage(Device, ColorImage, null);
        Vk!.FreeMemory(Device, ColorImageMemory, null);

        Vk!.DestroyDescriptorSetLayout(Device, DescriptorSetLayout, null);
        Vk!.DestroyDescriptorPool(Device, DescriptorPool, null);

        for (int i = 0; i < RenderFinishedSemaphores!.Length; i++)
        {
            Vk!.DestroySemaphore(Device, RenderFinishedSemaphores![i], null);
        }

        for (int i = 0; i < MaxFramesInFlight; i++)
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

        if (EnableValidationLayers)
        {
            DebugUtils!.DestroyDebugUtilsMessenger(Instance, DebugMessenger, null);
        }

        KhrSurface!.DestroySurface(Instance, Surface, null);
        Vk!.DestroyInstance(Instance, null);
        Vk!.Dispose();
    }
}