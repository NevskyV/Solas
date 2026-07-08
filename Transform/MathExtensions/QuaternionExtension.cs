using System.Numerics;

namespace Solas.Transform.MathExtensions;

public static class QuaternionExtensions
{
    private const float RadToDeg = (float)(180.0 / Math.PI);
    private const float Epsilon = 1e-6f;
    
    extension(Quaternion q)
    {
        /// <summary>
        /// Converts Quaternion to Euler angles in degrees.
        /// Order: XYZ.
        /// </summary>
        public  Vector3 ToVector3()
        {
            q = Quaternion.Normalize(q);

            Matrix4x4 m = Matrix4x4.CreateFromQuaternion(q);

            float y = MathF.Asin(-m.M13);
            float cy = MathF.Cos(y);

            float x;
            float z;

            if (MathF.Abs(cy) > Epsilon)
            {
                x = MathF.Atan2(m.M23, m.M33);
                z = MathF.Atan2(m.M12, m.M11);
            }
            else
            {
                // Gimbal Lock
                x = 0.0f;

                if (y > 0)
                {
                    // +90°
                    z = MathF.Atan2(m.M21, m.M22);
                }
                else
                {
                    // -90°
                    z = MathF.Atan2(-m.M21, m.M22);
                }
            }

            return new Vector3(
                x * RadToDeg,
                y * RadToDeg,
                z * RadToDeg);
        }
    }
}
