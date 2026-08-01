using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan.Extensions;

internal static unsafe class VulkanShaderModuleExtension
{
    extension(ShaderModule)
    {
        internal static ShaderModule Create(VulkanContext ctx, byte[] code)
        {
            ShaderModuleCreateInfo createInfo = new()
            {
                SType = StructureType.ShaderModuleCreateInfo,
                CodeSize = (nuint)code.Length,
            };

            ShaderModule shaderModule;

            fixed (byte* codePtr = code)
            {
                createInfo.PCode = (uint*)codePtr;

                if (ctx.Vk!.CreateShaderModule(ctx.Device, in createInfo, null, out shaderModule) != Result.Success)
                {
                    throw new Exception();
                }
            }

            return shaderModule;
        }
    }
}