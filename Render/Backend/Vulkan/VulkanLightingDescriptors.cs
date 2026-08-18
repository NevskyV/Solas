using Silk.NET.Vulkan;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanLightingDescriptors : VulkanInjectable
{
    internal void CreateLayouts()
    {
        DescriptorSetLayoutBinding b0Lights = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b1LightIndices = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b2TileGrid = new()
        {
            Binding = 2,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b3IndexCounter = new()
        {
            Binding = 3,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b4ShadowDepthArray = new()
        {
            Binding = 4,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b5PointShadowCubeArray = new()
        {
            Binding = 5,
            DescriptorType = DescriptorType.CombinedImageSampler,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b6ShadowMatrices = new()
        {
            Binding = 6,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding b7ShadowFrameParams = new()
        {
            Binding = 7,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutBinding[] set0Bindings =
        [
            b0Lights, b1LightIndices, b2TileGrid, b3IndexCounter, b4ShadowDepthArray, b5PointShadowCubeArray,
            b6ShadowMatrices, b7ShadowFrameParams
        ];

        fixed (DescriptorSetLayoutBinding* pBindings0 = set0Bindings)
        {
            DescriptorSetLayoutCreateInfo createInfo0 = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)set0Bindings.Length,
                PBindings = pBindings0
            };

            if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &createInfo0, null, out Ctx.LightingGlobalSet0Layout) !=
                Result.Success)
            {
                throw new Exception("failed to create set 0 layout!");
            }
        }

        DescriptorSetLayoutBinding b0FrameParams = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.UniformBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit | ShaderStageFlags.VertexBit | ShaderStageFlags.FragmentBit
        };

        DescriptorSetLayoutCreateInfo createInfo1 = new()
        {
            SType = StructureType.DescriptorSetLayoutCreateInfo,
            BindingCount = 1,
            PBindings = &b0FrameParams
        };

        if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &createInfo1, null, out Ctx.LightingFrameSet1Layout) !=
            Result.Success)
        {
            throw new Exception("failed to create set 1 layout!");
        }

        DescriptorSetLayoutBinding b0Objects = new()
        {
            Binding = 0,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit
        };

        DescriptorSetLayoutBinding b1Indirect = new()
        {
            Binding = 1,
            DescriptorType = DescriptorType.StorageBuffer,
            DescriptorCount = 1,
            StageFlags = ShaderStageFlags.ComputeBit
        };

        DescriptorSetLayoutBinding[] geomBindings = [b0Objects, b1Indirect];
        fixed (DescriptorSetLayoutBinding* pGeomBindings = geomBindings)
        {
            DescriptorSetLayoutCreateInfo createInfoGeom = new()
            {
                SType = StructureType.DescriptorSetLayoutCreateInfo,
                BindingCount = (uint)geomBindings.Length,
                PBindings = pGeomBindings
            };

            if (Ctx.Vk!.CreateDescriptorSetLayout(Ctx.Device, &createInfoGeom, null,
                    out Ctx.GeometryCullingSet0Layout) != Result.Success)
            {
                throw new Exception("failed to create geom culling set 0 layout!");
            }
        }
    }

    internal void AllocateAndWriteSets()
    {
        Ctx.LightingGlobalSetsSet0 = new DescriptorSet[Ctx.Settings.MaxFramesInFlight];
        Ctx.LightingFrameSetsSet1 = new DescriptorSet[Ctx.Settings.MaxFramesInFlight];
        Ctx.GeometryCullingSetsSet0 = new DescriptorSet[Ctx.Settings.MaxFramesInFlight];

        for (var i = 0; i < Ctx.Settings.MaxFramesInFlight; i++)
        {
            var l0 = Ctx.LightingGlobalSet0Layout;
            DescriptorSetAllocateInfo alloc0 = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &l0
            };
            Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &alloc0, out Ctx.LightingGlobalSetsSet0[i]);

            var l1 = Ctx.LightingFrameSet1Layout;
            DescriptorSetAllocateInfo alloc1 = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &l1
            };
            Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &alloc1, out Ctx.LightingFrameSetsSet1[i]);

            var lGeom = Ctx.GeometryCullingSet0Layout;
            DescriptorSetAllocateInfo allocGeom = new()
            {
                SType = StructureType.DescriptorSetAllocateInfo,
                DescriptorPool = Ctx.DescriptorPool,
                DescriptorSetCount = 1,
                PSetLayouts = &lGeom
            };
            Ctx.Vk!.AllocateDescriptorSets(Ctx.Device, &allocGeom, out Ctx.GeometryCullingSetsSet0[i]);

            DescriptorBufferInfo infoLights = new() { Buffer = Ctx.LightBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoIndices = new()
                { Buffer = Ctx.GlobalLightIndicesBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoGrid = new() { Buffer = Ctx.TileGridBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoCounter = new()
                { Buffer = Ctx.GlobalIndexCounterBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoShadowMatrices = new()
                { Buffer = Ctx.ShadowMatrixBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoShadowFrameParams = new()
                { Buffer = Ctx.FrameParamsBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorImageInfo infoShadowDepthArray = new()
            {
                Sampler = Ctx.ShadowSampler,
                ImageView = Ctx.ShadowDepthArrayView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };
            DescriptorImageInfo infoPointShadowCubeArray = new()
            {
                Sampler = Ctx.ShadowSampler,
                ImageView = Ctx.PointShadowCubeArrayView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };

            WriteDescriptorSet w0 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.LightingGlobalSetsSet0[i], DstBinding = 0,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoLights
            };
            WriteDescriptorSet w1 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.LightingGlobalSetsSet0[i], DstBinding = 1,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoIndices
            };
            WriteDescriptorSet w2 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.LightingGlobalSetsSet0[i], DstBinding = 2,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoGrid
            };
            WriteDescriptorSet w3 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.LightingGlobalSetsSet0[i], DstBinding = 3,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoCounter
            };

            WriteDescriptorSet w4 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[i],
                DstBinding = 4,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &infoShadowDepthArray
            };
            WriteDescriptorSet w5 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[i],
                DstBinding = 5,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.CombinedImageSampler,
                PImageInfo = &infoPointShadowCubeArray
            };
            WriteDescriptorSet w6 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[i],
                DstBinding = 6,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &infoShadowMatrices
            };

            WriteDescriptorSet w7 = new()
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[i],
                DstBinding = 7,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.UniformBuffer,
                PBufferInfo = &infoShadowFrameParams
            };

            WriteDescriptorSet[] writes0 = [w0, w1, w2, w3, w4, w5, w6, w7];
            fixed (WriteDescriptorSet* pWrites0 = writes0)
            {
                Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, (uint)writes0.Length, pWrites0, 0, null);
            }

            DescriptorBufferInfo infoFrame = new()
                { Buffer = Ctx.FrameParamsBuffers[i], Offset = 0, Range = Vk.WholeSize };
            WriteDescriptorSet wFrame = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.LightingFrameSetsSet1[i], DstBinding = 0,
                DescriptorCount = 1, DescriptorType = DescriptorType.UniformBuffer, PBufferInfo = &infoFrame
            };
            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, 1, &wFrame, 0, null);

            DescriptorBufferInfo infoObjects = new()
                { Buffer = Ctx.ObjectDataBuffers[i], Offset = 0, Range = Vk.WholeSize };
            DescriptorBufferInfo infoIndirect = new()
                { Buffer = Ctx.IndirectDrawBuffers[i], Offset = 0, Range = Vk.WholeSize };

            WriteDescriptorSet wGeom0 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.GeometryCullingSetsSet0[i], DstBinding = 0,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoObjects
            };
            WriteDescriptorSet wGeom1 = new()
            {
                SType = StructureType.WriteDescriptorSet, DstSet = Ctx.GeometryCullingSetsSet0[i], DstBinding = 1,
                DescriptorCount = 1, DescriptorType = DescriptorType.StorageBuffer, PBufferInfo = &infoIndirect
            };

            WriteDescriptorSet[] writesGeom = [wGeom0, wGeom1];
            fixed (WriteDescriptorSet* pWritesGeom = writesGeom)
            {
                Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, (uint)writesGeom.Length, pWritesGeom, 0, null);
            }
        }
    }

    internal void UpdateShadowImageBindings()
    {
        for (var frame = 0; frame < Ctx.LightingGlobalSetsSet0.Length; frame++)
        {
            var depthImageInfo = new DescriptorImageInfo
            {
                Sampler = Ctx.ShadowSampler,
                ImageView = Ctx.ShadowDepthArrayView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };
            var cubeImageInfo = new DescriptorImageInfo
            {
                Sampler = Ctx.ShadowSampler,
                ImageView = Ctx.PointShadowCubeArrayView,
                ImageLayout = ImageLayout.ShaderReadOnlyOptimal
            };
            WriteDescriptorSet[] writes =
            [
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = Ctx.LightingGlobalSetsSet0[frame],
                    DstBinding = 4,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &depthImageInfo
                },
                new()
                {
                    SType = StructureType.WriteDescriptorSet,
                    DstSet = Ctx.LightingGlobalSetsSet0[frame],
                    DstBinding = 5,
                    DescriptorCount = 1,
                    DescriptorType = DescriptorType.CombinedImageSampler,
                    PImageInfo = &cubeImageInfo
                }
            ];
            fixed (WriteDescriptorSet* writesPointer = writes)
            {
                Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, (uint)writes.Length, writesPointer, 0, null);
            }
        }
    }

    internal void UpdateShadowMatrixBindings()
    {
        for (var frame = 0; frame < Ctx.LightingGlobalSetsSet0.Length; frame++)
        {
            var matrixBufferInfo = new DescriptorBufferInfo
            {
                Buffer = Ctx.ShadowMatrixBuffers[frame],
                Offset = 0,
                Range = Vk.WholeSize
            };
            var write = new WriteDescriptorSet
            {
                SType = StructureType.WriteDescriptorSet,
                DstSet = Ctx.LightingGlobalSetsSet0[frame],
                DstBinding = 6,
                DescriptorCount = 1,
                DescriptorType = DescriptorType.StorageBuffer,
                PBufferInfo = &matrixBufferInfo
            };
            Ctx.Vk!.UpdateDescriptorSets(Ctx.Device, 1, &write, 0, null);
        }
    }
}