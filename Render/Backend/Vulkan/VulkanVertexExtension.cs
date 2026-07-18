using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Solas.Render.Components;

namespace Solas.Render.Backend.Vulkan;

internal static class VulkanVertexExtension
{
    extension(Vertex)
    {
        internal static unsafe VertexInputBindingDescription GetBindingDescription()
        {
            VertexInputBindingDescription bindingDescription = new()
            {
                Binding = 0,
                Stride = (uint)sizeof(Vertex),
                InputRate = VertexInputRate.Vertex,
            };

            return bindingDescription;
        }

        internal static VertexInputAttributeDescription[] GetAttributeDescriptions()
        {
            var attributeDescriptions = new[]
            {
                new VertexInputAttributeDescription()
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Pos)),
                },
                new VertexInputAttributeDescription()
                {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Color)),
                }
            };

            return attributeDescriptions;
        }
    }
}