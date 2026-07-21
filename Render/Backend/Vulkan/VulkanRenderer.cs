using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Vulkan;

internal class VulkanRenderer : IRenderer
{
    private VulkanContext _context;

    private readonly VulkanDebug _debug = new();
    private readonly VulkanSurface _surface = new();
    private readonly VulkanPhysicalDevice _physicalDevice = new();
    private readonly VulkanDevice _device = new();
    private readonly VulkanSwapChain _swapChain = new();
    private readonly VulkanPipeline _pipeline = new();
    private readonly VulkanCommands _commands = new();
    private readonly VulkanSynchronisation _synchronisation = new();
    private readonly VulkanVertexBuffer _vertexBuffer = new();
    private readonly VulkanIndexBuffer _indexBuffer = new();
    private readonly VulkanDescriptorSetLayout _descriptorSetLayout = new();
    private readonly VulkanUniformBuffers _uniformBuffers = new();
    private readonly VulkanDescriptorPool _descriptorPool = new();
    private readonly VulkanDescriptorSets _descriptorSets = new();
    private readonly VulkanTextureImage _textureImage = new();
    private readonly VulkanTextureImageView _textureImageView = new();
    private readonly VulkanTextureSampler _textureSampler = new();
    private readonly VulkanDepthResources _depthResources = new();

    void IRenderer.Start(IWindow window)
    {
        _context = new VulkanContext(window);
        _context.DepthResources = _depthResources;
        VulkanInjectable[] injectables =
        [
            _debug,
            _surface,
            _physicalDevice,
            _device,
            _swapChain,
            _pipeline,
            _commands,
            _synchronisation,
            _vertexBuffer,
            _indexBuffer,
            _uniformBuffers,
            _descriptorSetLayout,
            _descriptorPool,
            _descriptorSets,
            _textureImage,
            _textureImageView,
            _textureSampler,
            _depthResources
        ];

        foreach (var injectable in injectables)
        {
            injectable.Ctx = _context;
        }

        CreateInstance();
        _debug.SetupDebugMessenger();
        _surface.Create();
        _physicalDevice.PickPhysicalDevice();
        _device.CreateLogicalDevice();
        _swapChain.Create();
        _swapChain.CreateImageViews();
        _descriptorSetLayout.Create();
        _depthResources.Create();
        _pipeline.Create();
        _commands.CreateCommandPool();
        _textureImage.Create();
        _textureImageView.Create();
        _textureSampler.Create();
        _vertexBuffer.Create();
        _indexBuffer.Create();
        _uniformBuffers.Create();
        _descriptorPool.Create();
        _descriptorSets.Create();
        _commands.CreateCommandBuffers();
        _synchronisation.CreateSyncObjects();
    }

    private unsafe void CreateInstance()
    {
        _context.Vk = Vk.GetApi();
        if (_context.EnableValidationLayers && !_debug.CheckValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Hello Triangle"),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi("Solas"),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo
        };

        var extensions = _debug.GetRequiredExtensions();
        createInfo.EnabledExtensionCount = (uint)extensions.Length;
        createInfo.PpEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(extensions);
        if (_context.EnableValidationLayers)
        {
            createInfo.EnabledLayerCount = (uint)_context.ValidationLayers.Length;
            createInfo.PpEnabledLayerNames = (byte**)SilkMarshal.StringArrayToPtr(_context.ValidationLayers);

            DebugUtilsMessengerCreateInfoEXT debugCreateInfo = new();
            _debug.PopulateDebugMessengerCreateInfo(ref debugCreateInfo);
            createInfo.PNext = &debugCreateInfo;
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
            createInfo.PNext = null;
        }

        if (_context.Vk!.CreateInstance(in createInfo, null, out _context.Instance) != Result.Success)
        {
            throw new Exception("failed to create instance!");
        }

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);
        SilkMarshal.Free((nint)createInfo.PpEnabledExtensionNames);

        if (_context.EnableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }
    }

    unsafe void IRenderer.DrawFrame()
    {
        if (_context.Vk!.WaitForFences(_context.Device, [_context.InFlightFences![_context.FrameIndex]], true,
                ulong.MaxValue) != Result.Success)
        {
            throw new Exception("failed to wait for fence!");
        }

        var imageIndex = 0u;
        var result = _context.KhrSwapChain!.AcquireNextImage(_context.Device, _context.SwapChain, ulong.MaxValue,
            _context.PresentCompleteSemaphores![_context.FrameIndex], default, ref imageIndex);
        if (result == Result.ErrorOutOfDateKhr)
        {
            _swapChain.RecreateSwapChain();
            return;
        }

        if (result != Result.Success && result != Result.SuboptimalKhr)
        {
            throw new Exception("failed to acquire swap chain image!");
        }

        _context.Vk!.ResetFences(_context.Device, [_context.InFlightFences![_context.FrameIndex]]);

        _context.Vk!.ResetCommandBuffer(_context.CommandBuffers![_context.FrameIndex],
            CommandBufferResetFlags.ReleaseResourcesBit);
        _commands.RecordCommandBuffer(imageIndex);

        _uniformBuffers.Update(_context.FrameIndex);

        PipelineStageFlags waitDestinationStageMask = PipelineStageFlags.ColorAttachmentOutputBit;
        fixed (Semaphore* pPresentCompleteSemaphore = &_context.PresentCompleteSemaphores![_context.FrameIndex])
        fixed (Semaphore* pRenderFinishedSemaphore = &_context.RenderFinishedSemaphores![imageIndex])
        fixed (SwapchainKHR* pSwapChain = &_context.SwapChain)
        fixed (CommandBuffer* pCommandBuffer = &_context.CommandBuffers![_context.FrameIndex])
        {
            SubmitInfo submitInfo = new SubmitInfo
            {
                SType = StructureType.SubmitInfo,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = pPresentCompleteSemaphore,
                PWaitDstStageMask = &waitDestinationStageMask,
                CommandBufferCount = 1,
                PCommandBuffers = pCommandBuffer,
                SignalSemaphoreCount = 1,
                PSignalSemaphores = pRenderFinishedSemaphore
            };

            _context.Vk!.QueueSubmit(_context.GraphicsQueue, [submitInfo],
                _context.InFlightFences![_context.FrameIndex]);

            PresentInfoKHR presentInfoKhr = new()
            {
                SType = StructureType.PresentInfoKhr,
                WaitSemaphoreCount = 1,
                PWaitSemaphores = pRenderFinishedSemaphore,
                SwapchainCount = 1,
                PSwapchains = pSwapChain,
                PImageIndices = &imageIndex
            };

            result = _context.KhrSwapChain.QueuePresent(_context.GraphicsQueue, &presentInfoKhr);
            if (result == Result.ErrorOutOfDateKhr || result == Result.SuboptimalKhr || _context.FrameBufferResized)
            {
                _context.FrameBufferResized = false;
                _swapChain.RecreateSwapChain();
            }
            else if (result != Result.Success)
            {
                throw new Exception("failed to acquire swap chain image!");
            }
        }

        _context.FrameIndex = (_context.FrameIndex + 1) % _context.MaxFramesInFlight;
    }

    void IRenderer.OnResize(Vector2D<int> newSize)
    {
        _context.FrameBufferResized = true;
    }

    public void Dispose()
    {
        _context.Vk!.DeviceWaitIdle(_context.Device);
        _swapChain.Dispose();
        _context.Dispose();
    }
}