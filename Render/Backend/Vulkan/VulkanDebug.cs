using System.Runtime.InteropServices;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;

namespace Solas.Render.Backend.Vulkan;

internal unsafe class VulkanDebug : VulkanInjectable
{
    internal void PopulateDebugMessengerCreateInfo(ref DebugUtilsMessengerCreateInfoEXT createInfo)
    {
        createInfo.SType = StructureType.DebugUtilsMessengerCreateInfoExt;
        createInfo.MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
                                     DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt;
        createInfo.MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
                                 DebugUtilsMessageTypeFlagsEXT.ValidationBitExt;
        createInfo.PfnUserCallback = (DebugUtilsMessengerCallbackFunctionEXT)DebugCallback;
    }

    internal void SetupDebugMessenger()
    {
        if (!Ctx.EnableValidationLayers) return;
        
        if (!Ctx.Vk!.TryGetInstanceExtension(Ctx.Instance, out Ctx.DebugUtils)) return;

        DebugUtilsMessengerCreateInfoEXT createInfo = new();
        PopulateDebugMessengerCreateInfo(ref createInfo);

        if (Ctx.DebugUtils!.CreateDebugUtilsMessenger(Ctx.Instance, in createInfo, null, out Ctx.DebugMessenger) != Result.Success)
        {
            throw new Exception("failed to set up debug messenger!");
        }
    }

    internal string[] GetRequiredExtensions()
    {
        var glfwExtensions = Ctx.Window!.VkSurface!.GetRequiredExtensions(out var glfwExtensionCount);
        var extensions = SilkMarshal.PtrToStringArray((nint)glfwExtensions, (int)glfwExtensionCount);

        if (Ctx.EnableValidationLayers)
        {
            return extensions.Append(ExtDebugUtils.ExtensionName).ToArray();
        }

        return extensions;
    }

    internal bool CheckValidationLayerSupport()
    {
        uint layerCount = 0;
        Ctx.Vk!.EnumerateInstanceLayerProperties(ref layerCount, null);
        var availableLayers = new LayerProperties[layerCount];
        fixed (LayerProperties* availableLayersPtr = availableLayers)
        {
            Ctx.Vk!.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr);
        }

        var availableLayerNames = availableLayers.Select(layer => Marshal.PtrToStringAnsi((IntPtr)layer.LayerName)).ToHashSet();

        return Ctx.ValidationLayers.All(availableLayerNames.Contains);
    }

    private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageTypes, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
    {
        Console.WriteLine("validation layer:" + Marshal.PtrToStringAnsi((nint)pCallbackData->PMessage));

        return Vk.False;
    }
}