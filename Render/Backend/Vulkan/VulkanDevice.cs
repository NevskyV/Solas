using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanDevice : VulkanInjectable
{
    internal void CreateLogicalDevice()
    {
        // 1. Find the index of the first queue family that supports graphics
        uint queueFamilyCount = 0;
        Ctx.Vk!.GetPhysicalDeviceQueueFamilyProperties(Ctx.PhysicalDevice, &queueFamilyCount, null);

        var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
        fixed (QueueFamilyProperties* pQueueFamilies = queueFamilies)
        {
            Ctx.Vk.GetPhysicalDeviceQueueFamilyProperties(Ctx.PhysicalDevice, &queueFamilyCount, pQueueFamilies);
        }

        uint queueIndex = uint.MaxValue; // Equivalent to ~0 (all bits set to 1)

        for (uint qfpIndex = 0; qfpIndex < (uint)queueFamilies.Length; qfpIndex++)
        {
            // Check if queue family supports graphics operations
            bool supportsGraphics = (queueFamilies[qfpIndex].QueueFlags & QueueFlags.GraphicsBit) != 0;

            // Check if queue family supports presentation to the KHR surface
            Bool32 supportsPresent = false;
            Ctx.KhrSurface!.GetPhysicalDeviceSurfaceSupport(Ctx.PhysicalDevice, qfpIndex, Ctx.Surface,
                &supportsPresent);

            if (supportsGraphics && supportsPresent)
            {
                // Found a queue family that supports both graphics and present
                queueIndex = qfpIndex;
                break;
            }
        }

        if (queueIndex == uint.MaxValue)
        {
            throw new Exception("Could not find a queue for graphics and present -> terminating");
        }

        int graphicsIndex = -1;
        for (int i = 0; i < queueFamilies.Length; i++)
        {
            if (queueFamilies[i].QueueFlags.HasFlag(QueueFlags.GraphicsBit))
            {
                graphicsIndex = i;
                break;
            }
        }

        if (graphicsIndex == -1)
        {
            throw new Exception("No graphics queue family found!");
        }

        // 2. Set up the required features using struct chaining (pNext pointers)
        var extDynamicStateFeatures = new PhysicalDeviceExtendedDynamicStateFeaturesEXT
        {
            SType = StructureType.PhysicalDeviceExtendedDynamicStateFeaturesExt,
            ExtendedDynamicState = true,
            PNext = null
        };

        var vk13Features = new PhysicalDeviceVulkan13Features
        {
            SType = StructureType.PhysicalDeviceVulkan13Features,
            DynamicRendering = true,
            Synchronization2 = true,
            PNext = &extDynamicStateFeatures
        };

        var vk11Features = new PhysicalDeviceVulkan11Features
        {
            SType = StructureType.PhysicalDeviceVulkan11Features,
            ShaderDrawParameters = true,
            PNext = &vk13Features
        };

        var features2 = new PhysicalDeviceFeatures2
        {
            SType = StructureType.PhysicalDeviceFeatures2,
            Features = { SamplerAnisotropy = true },
            PNext = &vk11Features
        };

        // 3. Define the queue creation properties
        float queuePriority = 0.5f;
        var deviceQueueCreateInfo = new DeviceQueueCreateInfo
        {
            SType = StructureType.DeviceQueueCreateInfo,
            QueueFamilyIndex = (uint)graphicsIndex,
            QueueCount = 1,
            PQueuePriorities = &queuePriority
        };

        // 4. Marshal C# strings to native null-terminated UTF-8 byte arrays
        byte** ppEnabledExtensionNames = (byte**)SilkMarshal.StringArrayToPtr(Ctx.RequiredDeviceExtensions);
        uint enabledExtensionCount = (uint)Ctx.RequiredDeviceExtensions.Length;

        // 5. Define logical device creation properties
        var deviceCreateInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &features2,
            QueueCreateInfoCount = 1,
            PQueueCreateInfos = &deviceQueueCreateInfo,
            EnabledExtensionCount = enabledExtensionCount,
            PpEnabledExtensionNames = ppEnabledExtensionNames
        };

        try
        {
            // Create the logical device
            Device device;
            Result result = Ctx.Vk.CreateDevice(Ctx.PhysicalDevice, &deviceCreateInfo, null, &device);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create logical device: {result}");
            }

            Ctx.Device = device;

            // Get the graphics queue handle
            Queue graphicsQueue;
            Ctx.Vk.GetDeviceQueue(Ctx.Device, (uint)graphicsIndex, 0, &graphicsQueue);
            Ctx.GraphicsQueue = graphicsQueue;
        }
        finally
        {
            // Clean up the allocated unmanaged string array pointer memory
            SilkMarshal.FreeString((nint)ppEnabledExtensionNames);
        }
    }
}