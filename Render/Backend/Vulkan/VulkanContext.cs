using System.Numerics;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Solas.Render.Components;
using Buffer = Silk.NET.Vulkan.Buffer;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Backend.Vulkan;

internal sealed unsafe class VulkanContext(IWindow window) : IDisposable
{
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

    internal readonly Vertex[] Vertices =
    [
        new(new Vector2(-0.5f, -0.5f), new Vector3(1.0f, 0.0f, 0.0f)),
        new(new Vector2(0.5f, -0.5f), new Vector3(1.0f, 0.0f, 1.0f)),
        new(new Vector2(0.5f, 0.5f), new Vector3(0.0f, 0.0f, 1.0f)),
        new(new Vector2(-0.5f, 0.5f), new Vector3(1.0f, 0.0f, 1.0f))
    ];

    internal readonly uint[] Indices = [0, 1, 2, 2, 3, 0];

    internal Buffer VertexBuffer;
    internal DeviceMemory VertexBufferMemory;

    internal Buffer IndexBuffer;
    internal DeviceMemory IndexBufferMemory;

    internal Buffer[]? UniformBuffers;
    internal DeviceMemory[]? UniformBuffersMemory;

    internal DescriptorPool DescriptorPool;
    internal DescriptorSet[]? DescriptorSets;

    public void Dispose()
    {
        Vk!.DestroyDescriptorSetLayout(Device, DescriptorSetLayout, null);
        Vk!.DestroyDescriptorPool(Device, DescriptorPool, null);
        Vk!.DestroyBuffer(Device, IndexBuffer, null);
        Vk!.FreeMemory(Device, IndexBufferMemory, null);
        Vk!.DestroyBuffer(Device, VertexBuffer, null);
        Vk!.FreeMemory(Device, VertexBufferMemory, null);

        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            Vk!.DestroyBuffer(Device, UniformBuffers![i], null);
            Vk!.FreeMemory(Device, UniformBuffersMemory![i], null);
        }

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