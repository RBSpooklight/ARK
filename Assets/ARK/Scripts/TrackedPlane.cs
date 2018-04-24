using UnityEngine;

namespace Kouji.ARK
{
    public struct TrackedPlane
    {
        public string id;

        public Vector3 center;
        public Vector2 extents;
        public Quaternion rotation;

        public Vector3 Normal
        {
            get { return rotation * Vector3.up; }
        }

        public Plane Plane
        {
            get { return new Plane(Normal, center); }
        }

        public float Width
        {
            get { return extents.x; }
            set { extents.x = value; }
        }
        
        public float Height
        {
            get { return extents.y; }
            set { extents.y = value; }
        }

        public Vector3[] Quad
        {
            get
            {
                var points = new Vector3[4];
                var right = rotation * Vector3.right * extents.x / 2;
                var forward = rotation * Vector3.forward * extents.y / 2;

                points[0] = center + right - forward;
                points[1] = center + right + forward;
                points[2] = center + (-right) + forward;
                points[3] = center + (-right) - forward;

                return points;
            }
        }
    }
}
