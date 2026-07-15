using Silk.NET.Maths;
using Silk.NET.Vulkan;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanSwapChain : VulkanInjectable, IDisposable
{
    internal void CreateSwapChain()
    {
        SurfaceCapabilitiesKHR surfaceCapabilities;
        Ctx.KhrSurface!.GetPhysicalDeviceSurfaceCapabilities(Ctx.PhysicalDevice, Ctx.Surface, &surfaceCapabilities);
        
        Ctx.SwapChainExtent = ChooseSwapExtent(surfaceCapabilities);
        uint minImageCount = ChooseSwapMinImageCount(surfaceCapabilities);

        // Retrieve available surface formats
        uint formatCount = 0;
        Ctx.KhrSurface.GetPhysicalDeviceSurfaceFormats(Ctx.PhysicalDevice, Ctx.Surface, &formatCount, null);
        var availableFormats = new SurfaceFormatKHR[formatCount];
        fixed (SurfaceFormatKHR* pFormats = availableFormats)
        {
            Ctx.KhrSurface.GetPhysicalDeviceSurfaceFormats(Ctx.PhysicalDevice, Ctx.Surface, &formatCount, pFormats);
        }
        Ctx.SwapChainSurfaceFormat = ChooseSwapSurfaceFormat(availableFormats);

        // Retrieve available presentation modes
        uint presentModeCount = 0;
        Ctx.KhrSurface.GetPhysicalDeviceSurfacePresentModes(Ctx.PhysicalDevice, Ctx.Surface, &presentModeCount, null);
        var availablePresentModes = new PresentModeKHR[presentModeCount];
        fixed (PresentModeKHR* pPresentModes = availablePresentModes)
        {
            Ctx.KhrSurface.GetPhysicalDeviceSurfacePresentModes(Ctx.PhysicalDevice, Ctx.Surface, &presentModeCount, pPresentModes);
        }
        PresentModeKHR presentMode = ChooseSwapPresentMode(availablePresentModes);

        var swapChainCreateInfo = new SwapchainCreateInfoKHR
        {
            SType = StructureType.SwapchainCreateInfoKhr,
            Surface = Ctx.Surface,
            MinImageCount = minImageCount,
            ImageFormat = Ctx.SwapChainSurfaceFormat.Format,
            ImageColorSpace = Ctx.SwapChainSurfaceFormat.ColorSpace,
            ImageExtent = Ctx.SwapChainExtent,
            ImageArrayLayers = 1,
            ImageUsage = ImageUsageFlags.ColorAttachmentBit,
            ImageSharingMode = SharingMode.Exclusive,
            PreTransform = surfaceCapabilities.CurrentTransform,
            CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
            PresentMode = presentMode,
            Clipped = true
        };
        
        if (!Ctx.Vk!.TryGetDeviceExtension(Ctx.Instance, Ctx.Device, out Ctx.KhrSwapChain))
        {
            throw new NotSupportedException("VKContext._KHRContext._swapchain extension not found.");
        }

        if (Ctx.KhrSwapChain!.CreateSwapchain(Ctx.Device, in swapChainCreateInfo, null, out Ctx.SwapChain) != Result.Success)
        {
            throw new Exception("failed to create swap chain!");
        }

        // Retrieve swapchain images
        uint imageCount = 0;
        Ctx.KhrSwapChain.GetSwapchainImages(Ctx.Device, Ctx.SwapChain, &imageCount, null);
        Ctx.SwapChainImages = new Image[imageCount];
        fixed (Image* pImages = Ctx.SwapChainImages)
        {
            Ctx.KhrSwapChain.GetSwapchainImages(Ctx.Device, Ctx.SwapChain, &imageCount, pImages);
        }
    }
    
    public void Dispose()
    {
        foreach (var imageView in Ctx.SwapChainImageViews!)
        {
            Ctx.Vk!.DestroyImageView(Ctx.Device, imageView, null);
        }

        Ctx.KhrSwapChain!.DestroySwapchain(Ctx.Device, Ctx.SwapChain, null);
    }

    internal void RecreateSwapChain()
    {
        Vector2D<int> framebufferSize = Ctx.Window!.FramebufferSize;

        while (framebufferSize.X == 0 || framebufferSize.Y == 0)
        {
            framebufferSize = Ctx.Window.FramebufferSize;
            Ctx.Window.DoEvents();
        }
        
        Ctx.Vk!.DeviceWaitIdle(Ctx.Device);

        Dispose();

        CreateSwapChain();
        CreateImageViews();
    }
    
    internal void CreateImageViews()
    {
        Ctx.SwapChainImageViews = new ImageView[Ctx.SwapChainImages!.Length];

        var imageViewCreateInfo = new ImageViewCreateInfo
        {
            SType = StructureType.ImageViewCreateInfo,
            ViewType = ImageViewType.Type2D,
            Format = Ctx.SwapChainSurfaceFormat.Format,
            SubresourceRange = new ImageSubresourceRange
            {
                AspectMask = ImageAspectFlags.ColorBit,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            }
        };

        for (int i = 0; i < Ctx.SwapChainImages.Length; i++)
        {
            // Bind the current image to the creation info
            imageViewCreateInfo.Image = Ctx.SwapChainImages[i];

            ImageView imageView;
            Result result = Ctx.Vk!.CreateImageView(Ctx.Device, &imageViewCreateInfo, null, &imageView);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create image view for image index {i}: {result}");
            }

            Ctx.SwapChainImageViews[i] = imageView;
        }
    }

    private uint ChooseSwapMinImageCount(SurfaceCapabilitiesKHR surfaceCapabilities)
    {
        uint minImageCount = Math.Max(3u, surfaceCapabilities.MinImageCount);
        if (surfaceCapabilities.MaxImageCount > 0 && surfaceCapabilities.MaxImageCount < minImageCount)
        {
            minImageCount = surfaceCapabilities.MaxImageCount;
        }
        return minImageCount;
    }

    private SurfaceFormatKHR ChooseSwapSurfaceFormat(SurfaceFormatKHR[] availableFormats)
    {
        if (availableFormats.Length == 0)
        {
            throw new ArgumentException("No surface formats are available.");
        }

        foreach (var format in availableFormats)
        {
            if (format.Format == Format.B8G8R8A8Srgb && format.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr)
            {
                return format;
            }
        }

        return availableFormats[0];
    }

    private PresentModeKHR ChooseSwapPresentMode(PresentModeKHR[] availablePresentModes)
    {
        if (availablePresentModes.Contains(PresentModeKHR.MailboxKhr))
        {
            return PresentModeKHR.MailboxKhr;
        }

        return PresentModeKHR.FifoKhr; // Guaranteed to be supported by Vulkan specification
    }

    private Extent2D ChooseSwapExtent(SurfaceCapabilitiesKHR capabilities)
    {
        if (capabilities.CurrentExtent.Width != uint.MaxValue)
        {
            return capabilities.CurrentExtent;
        }


        return new Extent2D
        {
            Width = Math.Clamp((uint)Ctx.Window!.Size.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
            Height = Math.Clamp((uint)Ctx.Window!.Size.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height)
        };
    }
}