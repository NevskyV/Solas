using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Backend.Vulkan;

internal sealed class VulkanContext(IWindow window) : IDisposable
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

    internal PipelineLayout PipelineLayout;
    internal Pipeline GraphicsPipeline;

    internal CommandPool CommandPool;
    internal CommandBuffer[]? CommandBuffers;
    
    internal Semaphore[]? PresentCompleteSemaphores;
    internal Semaphore[]? RenderFinishedSemaphores;
    internal Fence[]? InFlightFences;

    public unsafe void Dispose()
    {
        foreach (var buffer in CommandBuffers!)
        {
            Vk!.FreeCommandBuffers(Device, CommandPool, 1, &buffer);
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

        for (int i = 0; i < RenderFinishedSemaphores!.Length; i++)
        {
            Vk!.DestroySemaphore(Device, RenderFinishedSemaphores![i], null);
        }
        
        for (int i = 0; i < MaxFramesInFlight; i++)
        {
            Vk!.DestroySemaphore(Device, PresentCompleteSemaphores![i], null);
            Vk!.DestroyFence(Device, InFlightFences![i], null);
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