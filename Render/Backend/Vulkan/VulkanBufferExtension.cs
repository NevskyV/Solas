using Silk.NET.Vulkan;
using Buffer = Silk.NET.Vulkan.Buffer;


namespace Solas.Render.Backend.Vulkan;

internal static class VulkanBufferExtension
{
    extension(Buffer)
    {
        internal static unsafe (Buffer, DeviceMemory) Create(VulkanContext ctx, ulong size, BufferUsageFlags usage,
            MemoryPropertyFlags properties)
        {
            BufferCreateInfo bufferCreateInfo = new()
            {
                SType = StructureType.BufferCreateInfo,
                Size = size,
                Usage = usage,
                SharingMode = SharingMode.Exclusive,
            };

            ctx.Vk!.CreateBuffer(ctx.Device, &bufferCreateInfo, null, out var buffer);

            MemoryRequirements memRequirements = ctx.Vk.GetBufferMemoryRequirements(ctx.Device, buffer);

            MemoryAllocateInfo memoryAllocateInfo = new MemoryAllocateInfo()
            {
                SType = StructureType.MemoryAllocateInfo,
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex = FindMemoryType(ctx, memRequirements.MemoryTypeBits, properties)
            };

            if (ctx.Vk.AllocateMemory(ctx.Device, &memoryAllocateInfo, null, out var bufferMemory) != Result.Success)
            {
                throw new Exception("failed to allocate buffer memory!");
            }

            ctx.Vk.BindBufferMemory(ctx.Device, buffer, bufferMemory, 0);
            return (buffer, bufferMemory);
        }

        internal static unsafe void CopyBuffer(VulkanContext ctx, Buffer srcBuffer, Buffer dstBuffer, ulong bufferSize)
        {
            var allocInfo = new CommandBufferAllocateInfo()
            {
                SType = StructureType.CommandBufferAllocateInfo,
                CommandPool = ctx.CommandPool,
                Level = CommandBufferLevel.Primary,
                CommandBufferCount = 1
            };

            ctx.Vk!.AllocateCommandBuffers(ctx.Device, in allocInfo, out var commandBuffer);

            CommandBufferBeginInfo beginInfo = new()
            {
                SType = StructureType.CommandBufferBeginInfo,
                Flags = CommandBufferUsageFlags.OneTimeSubmitBit,
            };

            ctx.Vk!.BeginCommandBuffer(commandBuffer, in beginInfo);

            BufferCopy copyRegion = new()
            {
                Size = bufferSize,
            };

            ctx.Vk!.CmdCopyBuffer(commandBuffer, srcBuffer, dstBuffer, 1, in copyRegion);
            ctx.Vk!.EndCommandBuffer(commandBuffer);

            SubmitInfo submitInfo = new()
            {
                SType = StructureType.SubmitInfo,
                CommandBufferCount = 1,
                PCommandBuffers = &commandBuffer,
            };

            ctx.Vk!.QueueSubmit(ctx.GraphicsQueue, 1, in submitInfo, default);
            ctx.Vk!.QueueWaitIdle(ctx.GraphicsQueue);

            ctx.Vk!.FreeCommandBuffers(ctx.Device, ctx.CommandPool, 1, in commandBuffer);
        }
    }

    internal static uint FindMemoryType(VulkanContext ctx, uint typeFilter, MemoryPropertyFlags properties)
    {
        PhysicalDeviceMemoryProperties memProperties = ctx.Vk!.GetPhysicalDeviceMemoryProperties(ctx.PhysicalDevice);
        for (var i = 0; i < memProperties.MemoryTypeCount; i++)
        {
            if ((typeFilter & (1 << i)) != 0 && (memProperties.MemoryTypes[i].PropertyFlags & properties) == properties)
            {
                return (uint)i;
            }
        }

        throw new Exception("failed to find suitable memory type!");
    }
}