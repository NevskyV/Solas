using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Maths;
using Silk.NET.Vulkan;
using Silk.NET.Windowing;
using Solas.Render.Components;
using Solas.Render.Data;
using Solas.Render.Logics;
using Solas.Render.Vulkan.Extensions;
using Solas.Transform;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Vulkan;

internal class VulkanRenderer : IRenderer
{
    private VulkanContext _context = null!;

    private readonly ConcurrentQueue<Action> _pendingActions = new();

    private readonly VulkanDebug _debug = new();
    private readonly VulkanSurface _surface = new();
    private readonly VulkanPhysicalDevice _physicalDevice = new();
    private readonly VulkanDevice _device = new();
    private readonly VulkanSwapChain _swapChain = new();
    private readonly VulkanPipeline _pipeline = new();
    private readonly VulkanCommands _commands = new();
    private readonly VulkanSynchronisation _synchronisation = new();
    private readonly VulkanDescriptorSetLayout _descriptorSetLayout = new();
    private readonly VulkanUniformBuffers _uniformBuffers = new();
    private readonly VulkanDescriptorPool _descriptorPool = new();
    private readonly VulkanDescriptorSets _descriptorSets = new();
    private readonly VulkanDepthResources _depthResources = new();
    private readonly VulkanColorResources _colorResources = new();
    private readonly VulkanResourceManager _resourceManager = new();
    private readonly VulkanComputePipeline _computePipeline = new();
    private readonly VulkanLightingResources _lightingResources = new();
    private readonly VulkanLightingDescriptors _lightingDescriptors = new();

    void IRenderer.Start(IWindow window, TransformData cameraTransform, CameraData cameraData)
    {
        _context = new VulkanContext(window);
        _context.DepthResources = _depthResources;
        _context.ColorResources = _colorResources;
        _context.CameraTransform = cameraTransform;
        _context.CameraData = cameraData;
        _context.ResourceManager = _resourceManager;
        _context.LightingResources = _lightingResources;

        _context.Settings = Query.GetSettings<GraphicsSettings>();

        VulkanInjectable.Ctx = _context;

        CreateInstance();
        _debug.SetupDebugMessenger();
        _surface.Create();
        _physicalDevice.PickPhysicalDevice();
        _device.CreateLogicalDevice();

        _swapChain.Create();
        _swapChain.CreateImageViews();
        _colorResources.Create();
        _depthResources.Create();

        _lightingDescriptors.CreateLayouts();
        _descriptorSetLayout.Create();

        _computePipeline.Create();
        _pipeline.Create();

        _commands.CreateCommandPool();

        _lightingResources.Create();
        _descriptorPool.Create();

        _lightingDescriptors.AllocateAndWriteSets();

        _commands.CreateCommandBuffers();

        SetupMeshRenderers();
        _synchronisation.CreateSyncObjects();
    }

    private void SetupMeshRenderers()
    {
        var preloadedRenders = RenderLogicEventHandler.BorrowLoadedLogic();
        for (var i = 0; i < preloadedRenders.Length; i++)
        {
            LoadModel(preloadedRenders[i]);
        }

        RenderLogicEventHandler.CreateLogicEvent += LoadModel;
        RenderLogicEventHandler.MeshUpdateEvent += UpdateMesh;
        RenderLogicEventHandler.TextureUpdateEvent += UpdateTexture;
        RenderLogicEventHandler.DisposeLogicEvent += UnloadModel;
    }

    private void LoadModel(MeshRenderLogic logic)
    {
        _pendingActions.Enqueue(() =>
        {
            if (logic.Mesh == null || logic.Texture == null) return;
            if (_context.RenderDataMap.ContainsKey(logic)) return;

            var renderData = new VulkanRenderData(logic)
            {
                GpuMesh = _resourceManager.AcquireMesh(logic.Mesh),
                GpuTexture = _resourceManager.AcquireTexture(logic.Texture)
            };

            _uniformBuffers.CreateForObject(renderData);
            _descriptorSets.CreateForObject(renderData);

            _context.RenderDataMap[logic] = renderData;
            _context.RenderData.Add(renderData);
        });
    }

    private void UpdateMesh(MeshRenderLogic logic, Mesh newMesh)
    {
        _pendingActions.Enqueue(() =>
        {
            if (!_context.RenderDataMap.TryGetValue(logic, out var renderData)) return;

            _context.Vk!.DeviceWaitIdle(_context.Device);

            if (logic.Mesh != null)
            {
                _resourceManager.ReleaseMesh(logic.Mesh.Id);
            }

            renderData.GpuMesh = _resourceManager.AcquireMesh(newMesh);
        });
    }

    private void UpdateTexture(MeshRenderLogic logic, Texture newTexture)
    {
        _pendingActions.Enqueue(() =>
        {
            if (!_context.RenderDataMap.TryGetValue(logic, out var renderData)) return;

            _context.Vk!.DeviceWaitIdle(_context.Device);

            if (logic.Texture != null)
            {
                _resourceManager.ReleaseTexture(logic.Texture.Id);
            }

            renderData.GpuTexture = _resourceManager.AcquireTexture(newTexture);
            _descriptorSets.UpdateTextureBinding(renderData);
        });
    }

    private void UnloadModel(MeshRenderLogic logic)
    {
        _pendingActions.Enqueue(() =>
        {
            if (!_context.RenderDataMap.TryGetValue(logic, out var renderData)) return;

            _context.Vk!.DeviceWaitIdle(_context.Device);

            _descriptorSets.FreeForObject(renderData);
            _uniformBuffers.DestroyForObject(renderData);

            if (logic.Mesh != null)
            {
                _resourceManager.ReleaseMesh(logic.Mesh.Id);
            }

            if (logic.Texture != null)
            {
                _resourceManager.ReleaseTexture(logic.Texture.Id);
            }

            _context.RenderDataMap.Remove(logic);
            _context.RenderData.Remove(renderData);
        });
    }

    private void ProcessPendingActions()
    {
        if (_pendingActions.Count == 0) return;
        while (_pendingActions.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    private unsafe void CreateInstance()
    {
        _context.Vk = Vk.GetApi();
        if (_context.Settings.EnableValidationLayers && !_debug.CheckValidationLayerSupport())
        {
            throw new Exception("validation layers requested, but not available!");
        }

        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Solas Game"),
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
        if (_context.Settings.EnableValidationLayers)
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

        if (_context.Settings.EnableValidationLayers)
        {
            SilkMarshal.Free((nint)createInfo.PpEnabledLayerNames);
        }
    }

    unsafe void IRenderer.DrawFrame()
    {
        ProcessPendingActions();

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

        _context.FrameIndex = (_context.FrameIndex + 1) % _context.Settings.MaxFramesInFlight;
    }

    void IRenderer.OnResize(Vector2D<int> newSize)
    {
        _context.FrameBufferResized = true;
    }

    public void Dispose()
    {
        RenderLogicEventHandler.CreateLogicEvent -= LoadModel;
        RenderLogicEventHandler.MeshUpdateEvent -= UpdateMesh;
        RenderLogicEventHandler.TextureUpdateEvent -= UpdateTexture;
        RenderLogicEventHandler.DisposeLogicEvent -= UnloadModel;

        _context.Vk!.DeviceWaitIdle(_context.Device);
        _swapChain.Dispose();
        _context.Dispose();
    }
}