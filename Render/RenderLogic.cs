using System.Runtime.InteropServices;
using Silk.NET.Core;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using Solas.Attributes;
using Solas.Components;
using Solas.Enums;
using Solas.Render.Backend.Vulkan;

namespace Solas.Render;

[Update, LateUpdate]
public class RenderLogic : Logic
{
    private WindowSettings _windowSettings;
    private IWindow _window;
    private IRenderer _renderer;
    private bool _isDestroyed;

    private void GetSettings()
    {
        _windowSettings = Query.GetSettings<WindowSettings>();
    }

    private void CreateWindow()
    {
        var options = WindowOptions.Default with
        {
            Size = new Vector2D<int>(_windowSettings.Width, _windowSettings.Height),
            Title = _windowSettings.WindowTitle,
            VSync = _windowSettings.Vsync,
            API = (GraphicsBackend)_windowSettings.Api == GraphicsBackend.Vulkan
                ? GraphicsAPI.DefaultVulkan
                : GraphicsAPI.Default,
            FramesPerSecond = WorldContext.CoreSettings.TargetFrameTime,
            WindowState = (WindowState)_windowSettings.StartWindowsState,
        };

        _window = Window.Create(options);
        _window.Initialize();

        var loadedIcon = LoadIcon(_windowSettings.IconPath);
        if (loadedIcon != null)
            _window.SetWindowIcon([(RawImage)loadedIcon]);

        if ((GraphicsBackend)_windowSettings.Api == GraphicsBackend.Vulkan && _window.VkSurface is null)
        {
            throw new Exception("Windowing platform doesn't support Vulkan.");
        }
    }

    private RawImage? LoadIcon(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;

        string fullPath;
        try
        {
            fullPath = Query.GetPath(path);
        }
        catch
        {
            return null;
        }

        if (!File.Exists(fullPath)) return null;
        byte[] rawBytes = File.ReadAllBytes(fullPath);

        return new RawImage(256, 256, rawBytes);
    }

    private void CreateRenderer()
    {
        _renderer = (GraphicsBackend)_windowSettings.Api switch
        {
            GraphicsBackend.Vulkan => new VulkanRenderer(),
            _ => throw new NotSupportedException(
                $"Graphics backend {(GraphicsBackend)_windowSettings.Api} is not supported.")
        };

        _renderer.Start(_window);
        _window.Resize += _renderer.OnResize;
    }

    public void Initialize()
    {
        GetSettings();
        CreateWindow();
        CreateRenderer();
    }

    public void Update()
    {
        if (_isDestroyed) return;
        if (!_window.IsClosing) _window.DoEvents();
    }

    public void LateUpdate()
    {
        if (_isDestroyed) return;
        if (!_window.IsClosing) _renderer.DrawFrame();
        else
        {
            Engine.State = GameState.None;
            //Game end
        }
    }

    public override void Dispose()
    {
        if (_isDestroyed) return;
        _renderer.Dispose();
        _window.Dispose();
        _isDestroyed = true;
    }
}