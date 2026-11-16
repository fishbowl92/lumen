using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// 격자 중심을 도는 궤도 카메라. 우클릭 드래그로 돌리고, 휠로 확대·축소하고, WASD·방향키로 옮긴다.
    /// ResetView는 처음 위치로 0.5초 동안 부드럽게 돌아간다(Home 키, 리셋).
    /// </summary>
    public sealed class CameraRig
    {
        private const float MinPitch = 10f;
        private const float MaxPitch = 85f;
        private const float OrbitSpeed = 0.25f;
        private const float DefaultYaw = 45f;
        private const float DefaultPitch = 40f;
        private const float ReturnDuration = 0.5f;

        private readonly Camera _camera;
        private Vector3 _target;
        private float _yaw = DefaultYaw;
        private float _pitch = DefaultPitch;
        private float _distance = 8f;
        private float _minDistance = 3f;
        private float _maxDistance = 30f;

        // 처음 위치(Frame에서 정함)와 이동 범위
        private Vector3 _homeTarget;
        private float _homeDistance = 8f;
        private Vector3 _panMin;
        private Vector3 _panMax;

        // 처음 위치로 돌아가는 중이면 0 이상
        private float _returnTime = -1f;
        private Vector3 _fromTarget;
        private float _fromYaw;
        private float _fromPitch;
        private float _fromDistance;

        public CameraRig(Camera camera)
        {
            _camera = camera;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.08f, 0.09f, 0.13f);
        }

        /// <summary>격자 전체가 보이도록 중심과 거리를 맞춘다. 이 위치가 Home 위치가 된다.</summary>
        public void Frame(Vector3 center, Vector3 extent)
        {
            _target = center;
            var radius = Mathf.Max(extent.x, extent.y, extent.z);
            _distance = radius * 1.6f + 3f;
            _minDistance = radius * 0.6f + 1.5f;
            _maxDistance = radius * 4f + 8f;
            _yaw = DefaultYaw;
            _pitch = DefaultPitch;

            _homeTarget = center;
            _homeDistance = _distance;
            // 격자 가장자리까지 화면 중심을 옮길 수 있다(높이는 그대로).
            _panMin = new Vector3(-0.5f, center.y, -0.5f);
            _panMax = new Vector3(extent.x - 0.5f, center.y, extent.z - 0.5f);
            _returnTime = -1f;
            Apply();
        }

        public void Orbit(Vector2 mouseDelta)
        {
            _returnTime = -1f;
            _yaw += mouseDelta.x * OrbitSpeed;
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * OrbitSpeed, MinPitch, MaxPitch);
            Apply();
        }

        /// <summary>direction > 0이면 가까이, < 0이면 멀리.</summary>
        public void Zoom(float direction)
        {
            _returnTime = -1f;
            var factor = direction > 0f ? 0.9f : 1.1f;
            _distance = Mathf.Clamp(_distance * factor, _minDistance, _maxDistance);
            Apply();
        }

        /// <summary>
        /// 화면 기준으로 카메라를 옮긴다. input.y > 0은 화면 안쪽(앞), input.x > 0은 오른쪽.
        /// 강제하지 않는 편의 기능이다(11/16 결정). 멀리 볼수록 빠르게 움직인다.
        /// </summary>
        public void Pan(Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _returnTime = -1f;
            var yaw = Quaternion.Euler(0f, _yaw, 0f);
            var forward = yaw * Vector3.forward;
            var right = yaw * Vector3.right;
            var speed = _distance * 0.6f;
            var move = (forward * input.y + right * input.x).normalized * (speed * deltaTime);
            _target = Clamp(_target + move);
            Apply();
        }

        /// <summary>처음 위치로 0.5초 동안 부드럽게 돌아간다. 돌아가는 중에 조작하면 그 자리에서 멈춘다.</summary>
        public void ResetView()
        {
            _fromTarget = _target;
            _fromYaw = _yaw;
            _fromPitch = _pitch;
            _fromDistance = _distance;
            _returnTime = 0f;
        }

        /// <summary>매 프레임 호출. 처음 위치로 돌아가는 중일 때만 움직인다.</summary>
        public void Tick(float deltaTime)
        {
            if (_returnTime < 0f)
            {
                return;
            }

            _returnTime += deltaTime;
            var t = Mathf.Clamp01(_returnTime / ReturnDuration);
            var s = t * t * (3f - 2f * t); // 처음과 끝이 느린 곡선
            _target = Vector3.Lerp(_fromTarget, _homeTarget, s);
            _yaw = Mathf.LerpAngle(_fromYaw, DefaultYaw, s);
            _pitch = Mathf.Lerp(_fromPitch, DefaultPitch, s);
            _distance = Mathf.Lerp(_fromDistance, _homeDistance, s);
            Apply();

            if (t >= 1f)
            {
                _yaw = DefaultYaw;
                _returnTime = -1f;
            }
        }

        private Vector3 Clamp(Vector3 target) =>
            new Vector3(
                Mathf.Clamp(target.x, _panMin.x, _panMax.x),
                target.y,
                Mathf.Clamp(target.z, _panMin.z, _panMax.z));

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _target - rotation * Vector3.forward * _distance;
            _camera.transform.rotation = rotation;
        }
    }
}
