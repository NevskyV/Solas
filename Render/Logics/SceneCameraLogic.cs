using System.Numerics;
using Silk.NET.Input;
using Solas.Attributes;
using Solas.Components;
using Solas.Interfaces;
using Solas.Render.Data;
using Solas.Transform;
using Solas.Transform.MathExtensions;

namespace Solas.Render.Logics;

[Update]
public class SceneCameraLogic : Logic, IInitializable
{
    private float _moveSpeed = 10.0f;
    private float _mouseSensitivity = 0.1f;
    private float _zoomSensitivity = 2.0f;

    private TransformData _transformData;
    private CameraData _cameraData;

    private float _currentFov;
    private bool _isRotating;
    private Vector2 _lastMousePosition;

    public IInputContext InputContext { get; set; }

    public void Initialize()
    {
        _transformData = Entity.GetData<TransformData>();
        _cameraData = Entity.GetData<CameraData>();

        _currentFov = _cameraData.FieldOfView;

        for (int i = 0; i < InputContext.Mice.Count; i++)
        {
            InputContext.Mice[i].MouseDown += OnMouseDown;
            InputContext.Mice[i].MouseUp += OnMouseUp;
            InputContext.Mice[i].MouseMove += OnMouseMove;
            InputContext.Mice[i].Scroll += OnMouseScroll;
        }
    }

    private void OnMouseScroll(IMouse mouse, ScrollWheel scrollWheel)
    {
        float fov = _cameraData.FieldOfView;

        fov -= scrollWheel.Y * _zoomSensitivity;

        _cameraData.FieldOfView = Math.Clamp(fov, 1.0f, 120.0f);
    }

    public void Update()
    {
        if (InputContext.Keyboards.Count == 0) return;

        var keyboard = InputContext.Keyboards[0];

        Quaternion rotation = _transformData.Rotation.Value.ToQuaternion();

        Vector3 forward = Vector3.Transform(-Vector3.UnitZ, rotation);
        Vector3 right = Vector3.Transform(Vector3.UnitX, rotation);
        Vector3 up = Vector3.UnitY;

        float speed = (float)(_moveSpeed * (keyboard.IsKeyPressed(Key.ShiftLeft) ? 2.5f : 1.0f) * Time.DeltaTime);
        Vector3 pos = _transformData.Position.Value;

        if (keyboard.IsKeyPressed(Key.W)) pos += forward * speed;
        if (keyboard.IsKeyPressed(Key.S)) pos -= forward * speed;
        if (keyboard.IsKeyPressed(Key.D)) pos += right * speed;
        if (keyboard.IsKeyPressed(Key.A)) pos -= right * speed;
        if (keyboard.IsKeyPressed(Key.E)) pos += up * speed;
        if (keyboard.IsKeyPressed(Key.Q)) pos -= up * speed;

        _transformData.Position.Value = pos;
    }

    private void OnMouseDown(IMouse mouse, MouseButton mouseButton)
    {
        if (mouseButton == MouseButton.Right)
        {
            _isRotating = true;
            _lastMousePosition = mouse.Position;
        }
    }

    private void OnMouseUp(IMouse mouse, MouseButton mouseButton)
    {
        if (mouseButton == MouseButton.Right)
        {
            _isRotating = false;
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 currentPosition)
    {
        if (!_isRotating) return;

        float deltaX = (currentPosition.X - _lastMousePosition.X) * _mouseSensitivity;
        float deltaY = (currentPosition.Y - _lastMousePosition.Y) * _mouseSensitivity;
        _lastMousePosition = currentPosition;

        Vector3 euler = _transformData.Rotation.Value;

        euler.Y -= deltaX;
        euler.X -= deltaY;

        euler.X = Math.Clamp(euler.X, -89.0f, 89.0f);

        _transformData.Rotation.Value = euler;
    }
}