using System.Runtime.InteropServices;
using Silk.NET.Vulkan;
using Solas.Render.Components;

namespace Solas.Render.Vulkan.Extensions;

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
            var attributeDescriptions = new VertexInputAttributeDescription[]
            {
                new()
                {
                    Binding = 0,
                    Location = 0,
                    Format = Format.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Pos)),
                },
                new()
                {
                    Binding = 0,
                    Location = 1,
                    Format = Format.R32G32B32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.Color)),
                },
                new()
                {
                    Binding = 0,
                    Location = 2,
                    Format = Format.R32G32Sfloat,
                    Offset = (uint)Marshal.OffsetOf<Vertex>(nameof(Vertex.TexCoord)),
                }
            };

            return attributeDescriptions;
        }
    }
}