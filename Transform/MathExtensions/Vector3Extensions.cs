using System.Numerics;

namespace Solas.Transform.MathExtensions;

public static class Vector3Extensions
{
    private const float RadToDeg = (float)(180.0 / Math.PI);
    private const float DegToRad = (float)(Math.PI / 180.0);
    
    extension(Vector3 eulerDegrees)
    {
        /// <summary>
        /// Converts Euler angles (degrees) to Quaternion.
        /// Order: XYZ.
        /// </summary>
        public Quaternion ToQuaternion()
        {
            var x   = eulerDegrees.X * DegToRad;
            var y = eulerDegrees.Y * DegToRad;
            var z  = eulerDegrees.Z * DegToRad;

            var qx = Quaternion.CreateFromAxisAngle(Vector3.UnitX, x);
            var qy = Quaternion.CreateFromAxisAngle(Vector3.UnitY, y);
            var qz = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, z);

            return Quaternion.Normalize(qz * qy * qx);
        }

        /// <summary>
        /// Rotates current Euler angles by delta.
        /// </summary>
        /// <param name="delta">Euler of delta rotation (degrees).</param>
        public Vector3 Rotate(Vector3 delta)
        {
            var current = eulerDegrees.ToQuaternion();
            var offset = delta.ToQuaternion();

            var result = Quaternion.Normalize(current * offset);

            return result.ToVector3();
        }
        
        public Vector3 RotateWorld(Vector3 delta)
        {
            var current = eulerDegrees.ToQuaternion();
            var offset = delta.ToQuaternion();

            var result = Quaternion.Normalize(offset * current);

            return result.ToVector3();
        }
        
        /// <summary>
        /// Rotates current Euler angles towards target by amount of t.
        /// </summary>
        /// <param name="target">Target rotation vector (degrees)</param>
        /// <param name="t">Amount of rotation</param>
        public Vector3 RotateTowards(Vector3 target, float t)
        {
            var q1 = eulerDegrees.ToQuaternion();
            var q2 = target.ToQuaternion();

            var q = Quaternion.Slerp(q1, q2, t);

            return q.ToVector3();
        }
        
        /// <summary>
        /// Rotates current Euler angles towards target by maxDegreesDelta.
        /// </summary>
        /// <param name="target">Target rotation vector (degrees)</param>
        /// <param name="maxDegreesDelta">Maximum of degrees to rotate towards a vector</param>
        public Vector3 RotateTowardsLimited(Vector3 target, float maxDegreesDelta)
        {
            var q1 = eulerDegrees.ToQuaternion();
            var q2 = target.ToQuaternion();

            var angle = Quaternion.Dot(q1, q2);

            angle = Math.Clamp(angle, -1f, 1f);

            var radians = MathF.Acos(MathF.Abs(angle)) * 2f;
            var degrees = radians * RadToDeg;

            if (degrees <= maxDegreesDelta)
                return target;

            var t = maxDegreesDelta / degrees;

            var q = Quaternion.Slerp(q1, q2, t);

            return q.ToVector3();
        }
    }
}
