using UnityEngine;

namespace Lumen.Game
{
    /// <summary>격자 중심을 도는 궤도 카메라. 우클릭 드래그로 돌리고 휠로 확대·축소한다.</summary>
    public sealed class CameraRig
    {
        private const float MinPitch = 10f;
        private const float MaxPitch = 85f;
        private const float OrbitSpeed = 0.25f;

        private readonly Camera _camera;
        private Vector3 _target;
        private float _yaw = 45f;
        private float _pitch = 40f;
        private float _distance = 8f;
        private float _minDistance = 3f;
        private float _maxDistance = 30f;

        public CameraRig(Camera camera)
        {
            _camera = camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
        }

        /// <summary>격자 전체가 보이도록 중심과 거리를 맞춘다.</summary>
        public void Frame(Vector3 center, Vector3 extent)
        {
            _target = center;
            var radius = Mathf.Max(extent.x, extent.y, extent.z);
            _distance = radius * 1.6f + 3f;
            _minDistance = radius * 0.6f + 1.5f;
            _maxDistance = radius * 4f + 8f;
            _yaw = 45f;
            _pitch = 40f;
            Apply();
        }

        public void Orbit(Vector2 mouseDelta)
        {
            _yaw += mouseDelta.x * OrbitSpeed;
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * OrbitSpeed, MinPitch, MaxPitch);
            Apply();
        }

        /// <summary>direction > 0이면 가까이, < 0이면 멀리.</summary>
        public void Zoom(float direction)
        {
            var factor = direction > 0f ? 0.9f : 1.1f;
            _distance = Mathf.Clamp(_distance * factor, _minDistance, _maxDistance);
            Apply();
        }

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _target - rotation * Vector3.forward * _distance;
            _camera.transform.rotation = rotation;
        }
    }
}
