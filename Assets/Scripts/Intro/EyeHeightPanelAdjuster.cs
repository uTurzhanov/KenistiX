using UnityEngine;

namespace VRIntro
{
    /// <summary>
    /// Вешается на InstructionPanel (Canvas). При каждом включении объекта
    /// подстраивает его высоту (Y) под текущий уровень глаз игрока —
    /// берёт Y-позицию VR-камеры и переносит панель на ту же высоту
    /// (с необязательным смещением), сохраняя X/Z такими, как заданы в сцене.
    ///
    /// Работает и для разного роста игроков, и если игрок присел/встал
    /// перед тем, как эта панель показалась (высота берётся заново при каждом SetActive(true)).
    /// </summary>
    public class EyeHeightPanelAdjuster : MonoBehaviour
    {
        [Tooltip("Смещение по высоте относительно уровня глаз (отрицательное — ниже глаз, обычно комфортнее для чтения)")]
        [SerializeField] private float verticalOffset = -0.1f;

        [Tooltip("На каком расстоянии перед игроком должна держаться панель (метры)")]
        [SerializeField] private float distanceFromPlayer = 1.5f;

        [Tooltip("Можно указать камеру вручную. Если пусто — компонент найдёт её сам по Camera.main (тег MainCamera)")]
        [SerializeField] private Transform xrCamera;

        [Tooltip("Разворачивать панель лицом к игроку (billboard)")]
        [SerializeField] private bool faceThePlayer = true;

        [Tooltip("Включи, если после разворота панель показывает игроку свою заднюю сторону")]
        [SerializeField] private bool flip180 = false;

        [Tooltip("Постоянно следовать за поворотом головы (вкл) — панель всегда перед глазами. " +
                 "Выключи, чтобы вернуть старое поведение: панель ставится один раз при появлении и дальше стоит на месте")]
        [SerializeField] private bool continuousFollow = true;

        [Tooltip("Скорость, с которой панель 'догоняет' игрока (чем больше — тем резче/быстрее)")]
        [SerializeField] private float followSpeed = 6f;

        private void OnEnable()
        {
            // Сразу ставим панель на нужное место без сглаживания —
            // чтобы не было эффекта "подлетания" издалека при появлении
            SnapToTarget();
        }

        private void Update()
        {
            if (continuousFollow)
            {
                FollowSmoothly();
            }
            else
            {
                // Старое поведение: только высота и разворот, позиция X/Z не трогается
                UpdateHeightAndFacing();
            }
        }

        private void SnapToTarget()
        {
            var cam = GetCamera();
            if (cam == null) return;

            transform.position = GetTargetPosition(cam);
            if (faceThePlayer)
                transform.rotation = GetTargetRotation(cam);
        }

        private void FollowSmoothly()
        {
            var cam = GetCamera();
            if (cam == null) return;

            Vector3 targetPos = GetTargetPosition(cam);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            if (faceThePlayer)
            {
                Quaternion targetRot = GetTargetRotation(cam);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * followSpeed);
            }
        }

        private Vector3 GetTargetPosition(Transform cam)
        {
            // Направление взгляда игрока по горизонтали (без вертикальной составляющей),
            // чтобы панель не улетала в потолок/пол, если игрок смотрит вверх/вниз
            Vector3 forwardFlat = cam.forward;
            forwardFlat.y = 0f;

            if (forwardFlat.sqrMagnitude < 0.0001f)
                forwardFlat = Vector3.forward;

            forwardFlat.Normalize();

            Vector3 targetPos = cam.position + forwardFlat * distanceFromPlayer;
            targetPos.y = cam.position.y + verticalOffset;
            return targetPos;
        }

        private Quaternion GetTargetRotation(Transform cam)
        {
            Vector3 lookTarget = cam.position;
            lookTarget.y = transform.position.y; // разворот только по Y, без наклона панели вверх/вниз

            Vector3 direction = lookTarget - transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return transform.rotation;

            Quaternion rot = Quaternion.LookRotation(direction);
            if (flip180)
                rot *= Quaternion.Euler(0f, 180f, 0f);
            return rot;
        }

        private void UpdateHeightAndFacing()
        {
            var cam = GetCamera();
            if (cam == null) return;

            var pos = transform.position;
            pos.y = cam.position.y + verticalOffset;
            transform.position = pos;

            if (faceThePlayer)
                transform.rotation = GetTargetRotation(cam);
        }

        private Transform GetCamera()
        {
            if (xrCamera != null)
                return xrCamera;

            var mainCam = Camera.main;
            if (mainCam == null)
            {
                Debug.LogWarning("[EyeHeightPanelAdjuster] Не найдена VR-камера (Camera.main). " +
                                  "Проверь, что на камере XR Origin стоит тег MainCamera, либо укажи Xr Camera вручную.");
                return null;
            }
            return mainCam.transform;
        }
    }
}