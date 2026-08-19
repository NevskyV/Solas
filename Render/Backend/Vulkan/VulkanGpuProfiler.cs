using System.Diagnostics;
using Silk.NET.Vulkan;
using Solas.Settings;

namespace Solas.Render.Vulkan;

internal unsafe class VulkanGpuProfiler : VulkanInjectable, IDisposable
{
    internal const uint QueryCountPerFrame = 14;

    private const uint FrameBeginQuery = 0;
    private const uint LightCullingBeginQuery = 1;
    private const uint LightCullingEndQuery = 2;
    private const uint ShadowBeginQuery = 3;
    private const uint ShadowEndQuery = 4;
    private const uint MainBeginQuery = 5;
    private const uint StencilBeginQuery = 6;
    private const uint StencilEndQuery = 7;
    private const uint BaseBeginQuery = 8;
    private const uint BaseEndQuery = 9;
    private const uint OverlayBeginQuery = 10;
    private const uint OverlayEndQuery = 11;
    private const uint MainEndQuery = 12;
    private const uint FrameEndQuery = 13;

    private double _timestampPeriodNanoseconds;
    private long _lastReportTimestamp;
    private uint _sampleCount;
    private double _frameMilliseconds;
    private double _lightCullingMilliseconds;
    private double _shadowMilliseconds;
    private double _mainMilliseconds;
    private double _baseMilliseconds;
    private double _stencilMilliseconds;
    private double _overlayMilliseconds;

    internal void Create()
    {
        PhysicalDeviceProperties properties;
        Ctx.Vk!.GetPhysicalDeviceProperties(Ctx.PhysicalDevice, &properties);
        _timestampPeriodNanoseconds = properties.Limits.TimestampPeriod;

        Ctx.GpuTimestampFrameWritten = new bool[Ctx.Settings.MaxFramesInFlight];

        var createInfo = new QueryPoolCreateInfo
        {
            SType = StructureType.QueryPoolCreateInfo,
            QueryType = QueryType.Timestamp,
            QueryCount = QueryCountPerFrame * Ctx.Settings.MaxFramesInFlight
        };

        if (Ctx.Vk.CreateQueryPool(Ctx.Device, &createInfo, null, out Ctx.GpuTimestampQueryPool) != Result.Success)
        {
            throw new InvalidOperationException("Failed to create the Vulkan timestamp query pool.");
        }
    }

    internal void CollectCompletedFrame(uint frameIndex)
    {
        if (!Query.GetSettings<CoreSettings>().IsProfilingEnabled)
        {
            ResetAggregation();
            return;
        }

        if (!Ctx.GpuTimestampFrameWritten[frameIndex])
        {
            return;
        }

        ulong[] timestamps = new ulong[QueryCountPerFrame];
        fixed (ulong* timestampPointer = timestamps)
        {
            var result = Ctx.Vk!.GetQueryPoolResults(
                Ctx.Device,
                Ctx.GpuTimestampQueryPool,
                frameIndex * QueryCountPerFrame,
                QueryCountPerFrame,
                (nuint)(timestamps.Length * sizeof(ulong)),
                timestampPointer,
                sizeof(ulong),
                QueryResultFlags.Result64Bit);
            if (result != Result.Success)
            {
                return;
            }
        }

        _sampleCount++;
        _frameMilliseconds += ToMilliseconds(timestamps[FrameEndQuery] - timestamps[FrameBeginQuery]);
        _lightCullingMilliseconds += ToMilliseconds(timestamps[LightCullingEndQuery] - timestamps[LightCullingBeginQuery]);
        _shadowMilliseconds += ToMilliseconds(timestamps[ShadowEndQuery] - timestamps[ShadowBeginQuery]);
        _mainMilliseconds += ToMilliseconds(timestamps[MainEndQuery] - timestamps[MainBeginQuery]);
        _baseMilliseconds += ToMilliseconds(timestamps[BaseEndQuery] - timestamps[BaseBeginQuery]);
        _stencilMilliseconds += ToMilliseconds(timestamps[StencilEndQuery] - timestamps[StencilBeginQuery]);
        _overlayMilliseconds += ToMilliseconds(timestamps[OverlayEndQuery] - timestamps[OverlayBeginQuery]);

        var timestamp = Stopwatch.GetTimestamp();
        if (timestamp - _lastReportTimestamp < Stopwatch.Frequency)
        {
            return;
        }

        var inverseSampleCount = 1.0 / _sampleCount;
        Console.WriteLine(
            $"[Vulkan GPU] avg-frame={_frameMilliseconds * inverseSampleCount:F3} ms | " +
            $"light-culling={_lightCullingMilliseconds * inverseSampleCount:F3} ms | " +
            $"shadows={_shadowMilliseconds * inverseSampleCount:F3} ms | " +
            $"main-render={_mainMilliseconds * inverseSampleCount:F3} ms | " +
            $"base={_baseMilliseconds * inverseSampleCount:F3} ms | " +
            $"stencil={_stencilMilliseconds * inverseSampleCount:F3} ms | " +
            $"overlay={_overlayMilliseconds * inverseSampleCount:F3} ms | " +
            $"samples={_sampleCount}");
        _lastReportTimestamp = timestamp;
        ResetAggregation();
    }

    internal void BeginFrame(CommandBuffer commandBuffer, uint frameIndex)
    {
        if (!Query.GetSettings<CoreSettings>().IsProfilingEnabled)
        {
            Ctx.GpuTimestampFrameWritten[frameIndex] = false;
            return;
        }

        var firstQuery = frameIndex * QueryCountPerFrame;
        Ctx.Vk!.CmdResetQueryPool(commandBuffer, Ctx.GpuTimestampQueryPool, firstQuery, QueryCountPerFrame);
        WriteTimestamp(commandBuffer, PipelineStageFlags2.TopOfPipeBit, firstQuery + FrameBeginQuery);
        Ctx.GpuTimestampFrameWritten[frameIndex] = true;
    }

    internal void WriteLightCullingBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.ComputeShaderBit, GetQueryIndex(frameIndex, LightCullingBeginQuery));
    }

    internal void WriteLightCullingEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.ComputeShaderBit, GetQueryIndex(frameIndex, LightCullingEndQuery));
    }

    internal void WriteShadowBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllCommandsBit, GetQueryIndex(frameIndex, ShadowBeginQuery));
    }

    internal void WriteShadowEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllCommandsBit, GetQueryIndex(frameIndex, ShadowEndQuery));
    }

    internal void WriteMainBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, MainBeginQuery));
    }

    internal void WriteStencilBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, StencilBeginQuery));
    }

    internal void WriteStencilEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, StencilEndQuery));
    }

    internal void WriteBaseBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, BaseBeginQuery));
    }

    internal void WriteBaseEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, BaseEndQuery));
    }

    internal void WriteOverlayBegin(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, OverlayBeginQuery));
    }

    internal void WriteOverlayEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, OverlayEndQuery));
    }

    internal void WriteMainEnd(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllGraphicsBit, GetQueryIndex(frameIndex, MainEndQuery));
    }

    internal void EndFrame(CommandBuffer commandBuffer, uint frameIndex)
    {
        WriteTimestamp(commandBuffer, PipelineStageFlags2.AllCommandsBit, GetQueryIndex(frameIndex, FrameEndQuery));
    }

    public void Dispose()
    {
        if (Ctx.GpuTimestampQueryPool.Handle != 0)
        {
            Ctx.Vk!.DestroyQueryPool(Ctx.Device, Ctx.GpuTimestampQueryPool, null);
            Ctx.GpuTimestampQueryPool = default;
        }
    }

    private uint GetQueryIndex(uint frameIndex, uint queryIndex)
    {
        return frameIndex * QueryCountPerFrame + queryIndex;
    }

    private void WriteTimestamp(CommandBuffer commandBuffer, PipelineStageFlags2 stageMask, uint queryIndex)
    {
        if (Query.GetSettings<CoreSettings>().IsProfilingEnabled)
        {
            Ctx.Vk!.CmdWriteTimestamp2(commandBuffer, stageMask, Ctx.GpuTimestampQueryPool, queryIndex);
        }
    }

    private double ToMilliseconds(ulong ticks)
    {
        return ticks * _timestampPeriodNanoseconds * 0.000001;
    }

    private void ResetAggregation()
    {
        _sampleCount = 0;
        _frameMilliseconds = 0.0;
        _lightCullingMilliseconds = 0.0;
        _shadowMilliseconds = 0.0;
        _mainMilliseconds = 0.0;
        _baseMilliseconds = 0.0;
        _stencilMilliseconds = 0.0;
        _overlayMilliseconds = 0.0;
    }
}
