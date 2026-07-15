using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;

namespace Solas.Render.Backend.Vulkan;

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
    }

    private bool IsDeviceSuitable(PhysicalDevice device)
    {
        // 1. Check if the physical device supports Vulkan 1.3
        PhysicalDeviceProperties properties;
        Ctx.Vk!.GetPhysicalDeviceProperties(device, &properties);
        // Note: Vk.Version13 is equivalent to Vulkan API version 1.3
        bool supportsVulkan13 = properties.ApiVersion >= Vk.Version13;

        // 2. Check if any queue family supports graphics operations
        uint queueFamilyCount = 0;
        Ctx.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);
        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pQueueFamilies = queueFamilies)
        {
            Ctx.Vk.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, pQueueFamilies);
        }

        bool supportsGraphics = queueFamilies.Any(q => q.QueueFlags.HasFlag(QueueFlags.GraphicsBit));

        // 3. Check if all required extensions are supported
        uint extensionCount = 0;
        Ctx.Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);
        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* pAvailableExtensions = availableExtensions)
        {
            Ctx.Vk.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, pAvailableExtensions);
        }

        bool supportsAllRequiredExtensions = Ctx.RequiredDeviceExtensions.All(required =>
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
        
        bool supportsRequiredFeatures = vk11Features.ShaderDrawParameters &&
                                        vk13Features.DynamicRendering && vk13Features.Synchronization2 && 
                                        extDynamicStateFeatures.ExtendedDynamicState;

        return supportsVulkan13 && supportsGraphics && supportsAllRequiredExtensions && supportsRequiredFeatures;
    }
}