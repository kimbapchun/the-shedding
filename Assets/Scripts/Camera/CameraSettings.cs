using System;
using UnityEngine;

namespace TheShedding
{
    [Serializable]
    public class CameraSettings
    {
        public float standingPivotHeight = 1.5f;
        public float sittingPivotHeight  = 0.9f;
        public float lyingPivotHeight    = 0.3f;
        public float distance            = 5f;
        public float fov                 = 60f;
    }
}
