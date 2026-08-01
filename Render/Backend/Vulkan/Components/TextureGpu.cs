using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan.Components;

internal unsafe class TextureGpu : IDisposable
{
    private readonly Vk _vk;
    private readonly Device _device;

    internal Image Image;
    internal DeviceMemory Memory;
    internal ImageView ImageView;
    internal Sampler Sampler;

    internal TextureGpu(Vk vk, Device device, Image image, DeviceMemory memory, ImageView imageView,
        Sampler sampler)
    {
        _vk = vk;
        _device = device;
        Image = image;
        Memory = memory;
        ImageView = imageView;
        Sampler = sampler;
    }

    public void Dispose()
    {
        _vk.DestroySampler(_device, Sampler, null);
        _vk.DestroyImageView(_device, ImageView, null);
        _vk.DestroyImage(_device, Image, null);
        _vk.FreeMemory(_device, Memory, null);
    }
}