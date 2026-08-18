using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanPhysicalDevice : VulkanInjectable
{
    internal void PickPhysicalDevice()
    {
        uint deviceCount = 0;
        Ctx.Vk!.EnumeratePhysicalDevices(Ctx.Instance, &deviceCount, null);

        if (deviceCount == 0)
        {
            throw new RuntimeWrappedException("Failed to find GPUs with Vulkan support!");
        }

        var physicalDevices = new PhysicalDevice[deviceCount];
        fixed (PhysicalDevice* pDevices = physicalDevices)
        {
            Ctx.Vk.EnumeratePhysicalDevices(Ctx.Instance, &deviceCount, pDevices);
        }

        PhysicalDevice? selectedDevice = null;
        foreach (var device in physicalDevices)
        {
            if (IsDeviceSuitable(device))
            {
                selectedDevice = device;
                break;
            }
        }

        if (selectedDevice == null)
        {
            throw new Exception("failed to find a suitable GPU!");
        }

        Ctx.PhysicalDevice = selectedDevice.Value;
        Ctx.MsaaSamples = GetMaxUsableSampleCount();
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        // 1. Check if the physical device supports Vulkan 1.3
        PhysicalDeviceProperties properties;
        Ctx.Vk!.GetPhysicalDeviceProperties(device, &properties);

        var supportsVulkan13 = properties.ApiVersion >= Vk.Version13;

        // 2. Check if any queue family supports graphics operations
        uint queueFamilyCount = 0;
        Ctx.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pQueueFamilies = queueFamilies)
        {
            Ctx.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, pQueueFamilies);
        }

        var supportsGraphics = queueFamilies.Any(q => q.QueueFlags.HasFlag(QueueFlags.GraphicsBit));

        // 3. Check if all required extensions are supported
        uint extensionCount = 0;
        Ctx.Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);
        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* pAvailableExtensions = availableExtensions)
        {
            Ctx.Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, pAvailableExtensions);
        }

        var supportsAllRequiredExtensions = Ctx.RequiredDeviceExtensions.All(required =>
            availableExtensions.Any(avail =>
            {
                // Convert the fixed byte buffer of the extension name to a C# string
                var pName = avail.ExtensionName;
                var availableName = Marshal.PtrToStringAnsi((IntPtr)pName) ?? string.Empty;
                return availableName == required;
            })
        );

        // 4. Query and check required features using struct chaining (pNext)
        var extDynamicStateFeatures = new PhysicalDeviceExtendedDynamicStateFeaturesEXT
        {
            SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
            PNext = null
        };

        var vk13Features = new PhysicalDeviceVulkan13Features
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            PNext = &extDynamicStateFeatures
        };

        var vk11Features = new PhysicalDeviceVulkan11Features
        {
            SType = StructureType.PhysicalDeviceVulkan11Features,
            PNext = &vk13Features
        };

        var features2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            PNext = &vk11Features
        };

        Ctx.Vk.GetPhysicalDeviceFeatures2(device, &features2);

        var supportsRequiredFeatures = vk11Features.ShaderDrawParameters && features2.Features.SamplerAnisotropy &&
                                       features2.Features.ImageCubeArray && vk13Features.DynamicRendering &&
                                       vk13Features.Synchronization2 &&
                                       extDynamicStateFeatures.ExtendedDynamicState;

        return supportsVulkan13 && supportsGraphics && supportsAllRequiredExtensions && supportsRequiredFeatures;
    }

    private SampleCountFlags GetMaxUsableSampleCount()
    {
        var physicalDeviceProperties = Ctx.Vk!.GetPhysicalDeviceProperties(Ctx.PhysicalDevice);
        var counts = physicalDeviceProperties.Limits.FramebufferColorSampleCounts &
                     physicalDeviceProperties.Limits.FramebufferDepthSampleCounts;

        if ((counts & (SampleCountFlags)Ctx.Settings.Msaa) != 0)
        {
            return (SampleCountFlags)Ctx.Settings.Msaa;
        }

        if ((counts & SampleCountFlags.Count64Bit) != 0)
        {
            return SampleCountFlags.Count64Bit;
        }

        if ((counts & SampleCountFlags.Count32Bit) != 0)
        {
            return SampleCountFlags.Count32Bit;
        }

        if ((counts & SampleCountFlags.Count16Bit) != 0)
        {
            return SampleCountFlags.Count16Bit;
        }

        if ((counts & SampleCountFlags.Count8Bit) != 0)
        {
            return SampleCountFlags.Count8Bit;
        }

        if ((counts & SampleCountFlags.Count4Bit) != 0)
        {
            return SampleCountFlags.Count4Bit;
        }

        if ((counts & SampleCountFlags.Count2Bit) != 0)
        {
            return SampleCountFlags.Count2Bit;
        }

        return SampleCountFlags.Count1Bit;
    }
}