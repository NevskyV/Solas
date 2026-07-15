using Silk.NET.Vulkan;
using Semaphore = Silk.NET.Vulkan.Semaphore;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanSynchronisation : VulkanInjectable
{
    internal void CreateSyncObjects()
    {
        Ctx.PresentCompleteSemaphores = new Semaphore[Ctx.MaxFramesInFlight];
        Ctx.RenderFinishedSemaphores = new Semaphore[Ctx.SwapChainImages!.Length];
        Ctx.InFlightFences = new Fence[Ctx.MaxFramesInFlight];

        SemaphoreCreateInfo semaphoreInfo = new()
        {
            SType = StructureType.SemaphoreCreateInfo,
        };

        FenceCreateInfo fenceInfo = new()
        {
            SType = StructureType.FenceCreateInfo,
            Flags = FenceCreateFlags.SignaledBit,
        };
        
        for (var i = 0; i < Ctx.MaxFramesInFlight; i++)
        {
            if (Ctx.Vk!.CreateSemaphore(Ctx.Device, in semaphoreInfo, null, out Ctx.PresentCompleteSemaphores[i]) != Result.Success ||
                Ctx.Vk!.CreateFence(Ctx.Device, in fenceInfo, null, out Ctx.InFlightFences[i]) != Result.Success)
            {
                throw new Exception("failed to create synchronization objects for a frame!");
            }
        }
        
        for (var i = 0; i < Ctx.SwapChainImages.Length; i++)
        {
            if (Ctx.Vk!.CreateSemaphore(Ctx.Device, in semaphoreInfo, null, out Ctx.RenderFinishedSemaphores[i]) != Result.Success)
            {
                throw new Exception("failed to create render finished semaphore!");
            }
        }
    }
}