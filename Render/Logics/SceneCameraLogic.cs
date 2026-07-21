using System.Numerics;
using Silk.NET.Input;
using Solas.Components;
using Solas.Interfaces;
using Solas.Render.Data;
using Solas.Transform;

namespace Solas.Render.Logics;

public class SceneCameraLogic : Logic, IInitializable
{
    private readonly float _offset = 5;
    private TransformData _transformData;
    private CameraData _cameraData;

    private bool _isRotating;
    private Vector2 _lastMousePosition;
    private bool _isFirstMove = true;
    public IInputContext InputContext { get; set; }

    public void Initialize()
    {
        _transformData = Entity.GetData<TransformData>();
        _cameraData = Entity.GetData<CameraData>();
        for (int i = 0; i < InputContext.Keyboards.Count; i++)
            InputContext.Keyboards[i].KeyDown += OnMove;
        for (int i = 0; i < InputContext.Mice.Count; i++)
        {
            InputContext.Mice[i].MouseDown += OnMouseDown;
            InputContext.Mice[i].MouseUp += OnMouseUp;
            InputContext.Mice[i].MouseMove += OnMouseMove;
        }
    }

    private void OnMove(IKeyboard keyboard, Key key, int keyCode)
    {
        var pos = _transformData.Position.Value;
        _transformData.Position.Value = key switch
        {
            Key.D => pos with { X = pos.X + _offset },
            Key.A => pos with { X = pos.X - _offset },
            Key.W => pos with { Z = pos.Z + _offset },
            Key.S => pos with { Z = pos.Z - _offset },
            Key.E => pos with { Y = pos.Y + _offset },
            Key.Q => pos with { Y = pos.Y - _offset },
            _ => _transformData.Position.Value
        };
    }

    private void OnMouseDown(IMouse mouse, MouseButton mouseButton)
    {
        if (mouseButton == MouseButton.Right)
            _isRotating = true;
    }

    private void OnMouseUp(IMouse mouse, MouseButton mouseButton)
    {
        if (mouseButton == MouseButton.Right)
            _isRotating = false;
    }

    private void OnMouseMove(IMouse mouse, Vector2 currentPosition)
    {
        if (_isFirstMove)
        {
            _lastMousePosition = currentPosition;
            _isFirstMove = false;
            return;
        }

        float deltaX = currentPosition.X - _lastMousePosition.X;
        float deltaY = currentPosition.Y - _lastMousePosition.Y;

        _lastMousePosition = currentPosition;

        var rot = _transformData.Rotation.Value;
        _transformData.Rotation.Value = rot with { X = rot.X + deltaX, Y = rot.Y + deltaY };
    }
}