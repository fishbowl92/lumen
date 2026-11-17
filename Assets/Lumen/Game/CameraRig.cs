using UnityEngine;

namespace Lumen.Game
{
    /// <summary>
    /// 격자 중심을 도는 궤도 카메라. 우클릭 드래그로 돌리고, 휠로 확대·축소하고, WASD·방향키로 옮긴다.
    /// ResetView는 처음 위치로 0.5초 동안 부드럽게 돌아간다(Home 키, 리셋).
    /// 11/17 외부 테스트 반영: 위아래 회전 제한을 없애 360° 돌 수 있고, WASD는 바라보는 방향 그대로 움직인다.
    /// </summary>
    public sealed class CameraRig
    {
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
            // 격자 상자 안이라면 어디로든 화면 중심을 옮길 수 있다(바라보는 방향으로 움직이므로 높이도 바뀐다).
            _panMin = new Vector3(-0.5f, -0.5f, -0.5f);
            _panMax = new Vector3(extent.x - 0.5f, extent.y - 0.5f, extent.z - 0.5f);
            _returnTime = -1f;
            Apply();
        }

        public void Orbit(Vector2 mouseDelta)
        {
            _returnTime = -1f;
            // 위로 넘어가 화면이 뒤집혀 있으면 좌우 드래그 방향도 뒤집어, 손 방향과 화면 방향이 맞게 한다.
            var upsideDown = Mathf.Cos(_pitch * Mathf.Deg2Rad) < 0f;
            _yaw += mouseDelta.x * OrbitSpeed * (upsideDown ? -1f : 1f);
            _pitch = Mathf.Repeat(_pitch - mouseDelta.y * OrbitSpeed + 180f, 360f) - 180f;
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
        /// 화면 기준으로 카메라를 옮긴다. input.y > 0은 바라보는 방향으로 전진, input.x > 0은 화면 오른쪽.
        /// 위아래로 기울여 보고 있으면 그 각도 그대로 들어가고 나온다(11/17 외부 테스트 반영, 이전에는 수평으로만 움직임).
        /// 강제하지 않는 편의 기능이다(11/16 결정). 멀리 볼수록 빠르게 움직인다.
        /// </summary>
        public void Pan(Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _returnTime = -1f;
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var forward = rotation * Vector3.forward;
            var right = rotation * Vector3.right;
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
            _pitch = Mathf.LerpAngle(_fromPitch, DefaultPitch, s);
            _distance = Mathf.Lerp(_fromDistance, _homeDistance, s);
            Apply();

            if (t >= 1f)
            {
                _yaw = DefaultYaw;
                _pitch = DefaultPitch;
                _returnTime = -1f;
            }
        }

        private Vector3 Clamp(Vector3 target) =>
            new Vector3(
                Mathf.Clamp(target.x, _panMin.x, _panMax.x),
                Mathf.Clamp(target.y, _panMin.y, _panMax.y),
                Mathf.Clamp(target.z, _panMin.z, _panMax.z));

        private void Apply()
        {
            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _camera.transform.position = _target - rotation * Vector3.forward * _distance;
            _camera.transform.rotation = rotation;
        }
    }
}
