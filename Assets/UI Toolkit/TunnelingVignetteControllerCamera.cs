using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine.Assertions;
using UnityEngine.Scripting.APIUpdating;

namespace UnityEngine.XR.Interaction.Toolkit.Locomotion.Comfort
{
    [Serializable]
    [MovedFrom("UnityEngine.XR.Interaction.Toolkit")]
    public sealed class VignetteParameters
    {
        [SerializeField]
        [Range(0f, Defaults.apertureSizeMax)]
        float m_ApertureSize = Defaults.apertureSizeDefault;

        public float apertureSize
        {
            get => m_ApertureSize;
            set => m_ApertureSize = value;
        }

        [SerializeField]
        [Range(0f, Defaults.featheringEffectMax)]
        float m_FeatheringEffect = Defaults.featheringEffectDefault;

        public float featheringEffect
        {
            get => m_FeatheringEffect;
            set => m_FeatheringEffect = value;
        }

        [SerializeField]
        float m_EaseInTime = Defaults.easeInTimeDefault;

        public float easeInTime
        {
            get => m_EaseInTime;
            set => m_EaseInTime = value;
        }

        [SerializeField]
        float m_EaseOutTime = Defaults.easeOutTimeDefault;

        public float easeOutTime
        {
            get => m_EaseOutTime;
            set => m_EaseOutTime = value;
        }

        [SerializeField]
        bool m_EaseInTimeLock = true; // По умолчанию true для корректного срабатывания импульса затвора

        public bool easeInTimeLock
        {
            get => m_EaseInTimeLock;
            set => m_EaseInTimeLock = value;
        }

        [SerializeField]
        float m_EaseOutDelayTime = Defaults.easeOutDelayTimeDefault;

        public float easeOutDelayTime
        {
            get => m_EaseOutDelayTime;
            set => m_EaseOutDelayTime = value;
        }

        [SerializeField]
        Color m_VignetteColor = Defaults.vignetteColorDefault;

        public Color vignetteColor
        {
            get => m_VignetteColor;
            set => m_VignetteColor = value;
        }

        [SerializeField]
        Color m_VignetteColorBlend = Defaults.vignetteColorBlendDefault;

        public Color vignetteColorBlend
        {
            get => m_VignetteColorBlend;
            set => m_VignetteColorBlend = value;
        }

        [SerializeField]
        [Range(Defaults.apertureVerticalPositionMin, Defaults.apertureVerticalPositionMax)]
        float m_ApertureVerticalPosition = Defaults.apertureVerticalPositionDefault;

        public float apertureVerticalPosition
        {
            get => m_ApertureVerticalPosition;
            set => m_ApertureVerticalPosition = value;
        }

        internal static class Defaults
        {
            public const float apertureSizeMax = 1f;
            public const float featheringEffectMax = 1f;
            public const float apertureVerticalPositionMax = 0.2f;
            public const float apertureVerticalPositionMin = -apertureVerticalPositionMax;

            public const float apertureSizeDefault = 0f; // Полное закрытие для эффекта затвора/вспышки
            public const float featheringEffectDefault = 0.2f;
            public const float easeInTimeDefault = 0.1f;
            public const float easeOutTimeDefault = 0.25f;
            public const bool easeInTimeLockDefault = true;
            public const float easeOutDelayTimeDefault = 0.05f;

            public static readonly Color vignetteColorDefault = Color.black;
            public static readonly Color vignetteColorBlendDefault = Color.black;
            public const float apertureVerticalPositionDefault = 0f;

            public static readonly VignetteParameters defaultEffect = new VignetteParameters
            {
                apertureSize = apertureSizeDefault,
                featheringEffect = featheringEffectDefault,
                easeInTime = easeInTimeDefault,
                easeOutTime = easeOutTimeDefault,
                easeInTimeLock = easeInTimeLockDefault,
                easeOutDelayTime = easeOutDelayTimeDefault,
                vignetteColor = vignetteColorDefault,
                vignetteColorBlend = vignetteColorBlendDefault,
                apertureVerticalPosition = apertureVerticalPositionDefault,
            };

            public static readonly VignetteParameters noEffect = new VignetteParameters
            {
                apertureSize = apertureSizeMax,
                featheringEffect = 0f,
                easeInTime = 0f,
                easeOutTime = 0f,
                easeInTimeLock = false,
                easeOutDelayTime = 0f,
                vignetteColor = vignetteColorDefault,
                vignetteColorBlend = vignetteColorBlendDefault,
                apertureVerticalPosition = apertureVerticalPositionDefault,
            };
        }

        public void CopyFrom(VignetteParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            apertureSize = parameters.apertureSize;
            featheringEffect = parameters.featheringEffect;
            easeInTime = parameters.easeInTime;
            easeOutTime = parameters.easeOutTime;
            easeInTimeLock = parameters.easeInTimeLock;
            easeOutDelayTime = parameters.easeOutDelayTime;
            vignetteColor = parameters.vignetteColor;
            vignetteColorBlend = parameters.vignetteColorBlend;
            apertureVerticalPosition = parameters.apertureVerticalPosition;
        }
    }

    [MovedFrom("UnityEngine.XR.Interaction.Toolkit")]
    public enum EaseState
    {
        NotEasing,
        EasingIn,
        EasingInHoldBeforeEasingOut,
        EasingOutDelay,
        EasingOut,
    }

    [MovedFrom("UnityEngine.XR.Interaction.Toolkit")]
    public interface ITunnelingVignetteProvider
    {
        VignetteParameters vignetteParameters { get; }
    }

    [AddComponentMenu("XR/Comfort/Tunneling Vignette Controller", 11)]
    [MovedFrom("UnityEngine.XR.Interaction.Toolkit")]
    public class TunnelingVignetteControllerCamera : MonoBehaviour, ITunnelingVignetteProvider
    {
        const string k_DefaultShader = "VR/TunnelingVignette";

        static class ShaderPropertyLookup
        {
            public static readonly int apertureSize = Shader.PropertyToID("_ApertureSize");
            public static readonly int featheringEffect = Shader.PropertyToID("_FeatheringEffect");
            public static readonly int vignetteColor = Shader.PropertyToID("_VignetteColor");
            public static readonly int vignetteColorBlend = Shader.PropertyToID("_VignetteColorBlend");
        }

        [SerializeField]
        VignetteParameters m_DefaultParameters = new VignetteParameters();

        public VignetteParameters defaultParameters
        {
            get => m_DefaultParameters;
            set => m_DefaultParameters = value;
        }

#if UNITY_EDITOR
        [SerializeField]
        [Tooltip("Включить отображение виньетки в окне редактора при изменении параметров")]
        bool m_PreviewInEditor = true;
#endif
        
        [SerializeField]
        VignetteParameters m_CurrentParameters = new VignetteParameters();

        public VignetteParameters currentParameters => m_CurrentParameters;

        // Реализация ITunnelingVignetteProvider для самого контроллера
        public VignetteParameters vignetteParameters => m_DefaultParameters;

        class ProviderRecord
        {
            public ITunnelingVignetteProvider provider { get; }
            public EaseState easeState { get; set; } = EaseState.NotEasing;
            public float dynamicApertureSize { get; set; } = VignetteParameters.Defaults.apertureSizeMax;
            public bool easeInLockEnded { get; set; }
            public float dynamicEaseOutDelayTime { get; set; }

            public ProviderRecord(ITunnelingVignetteProvider provider)
            {
                this.provider = provider;
            }
        }

        readonly List<ProviderRecord> m_ProviderRecords = new List<ProviderRecord>();

        MeshRenderer m_MeshRender;
        MeshFilter m_MeshFilter;
        Material m_SharedMaterial;
        MaterialPropertyBlock m_VignettePropertyBlock;

        internal static event Action<ITunnelingVignetteProvider> vignetteProviderQueued;

        /// <summary>
        /// Запускает одиночный импульс виньетки (закрытие и автоматическое открытие) для эффекта скриншота.
        /// </summary>
        public void TriggerVignettePulse()
        {
            UnityEngine.Debug.Log("[Vignette] TriggerVignettePulse вызван!");

            m_DefaultParameters.easeInTimeLock = true;

            ProviderRecord targetRecord = null;
            foreach (var record in m_ProviderRecords)
            {
                if (record.provider == this)
                {
                    targetRecord = record;
                    break;
                }
            }

            if (targetRecord == null)
            {
                targetRecord = new ProviderRecord(this);
                m_ProviderRecords.Add(targetRecord);
            }

            // Полный сброс состояния перед стартом импульса
            targetRecord.dynamicApertureSize = VignetteParameters.Defaults.apertureSizeMax;
            targetRecord.easeInLockEnded = false;
            targetRecord.dynamicEaseOutDelayTime = 0f;
            targetRecord.easeState = EaseState.EasingInHoldBeforeEasingOut;
        }

        public void BeginTunnelingVignette(ITunnelingVignetteProvider provider)
        {
            foreach (var record in m_ProviderRecords)
            {
                if (record.provider == provider)
                {
                    record.easeState = EaseState.EasingIn;
                    record.easeInLockEnded = false;
                    record.dynamicEaseOutDelayTime = 0f;
                    return;
                }
            }

            m_ProviderRecords.Add(new ProviderRecord(provider) { easeState = EaseState.EasingIn });
            vignetteProviderQueued?.Invoke(provider);
        }

        public void EndTunnelingVignette(ITunnelingVignetteProvider provider)
        {
            var parameters = provider.vignetteParameters ?? m_DefaultParameters;

            foreach (var record in m_ProviderRecords)
            {
                if (record.provider == provider)
                {
                    record.easeState = parameters.easeInTimeLock && !record.easeInLockEnded
                        ? EaseState.EasingInHoldBeforeEasingOut
                        : parameters.easeOutDelayTime > 0f &&
                        record.dynamicEaseOutDelayTime < parameters.easeOutDelayTime
                            ? EaseState.EasingOutDelay
                            : EaseState.EasingOut;
                    return;
                }
            }

            var easeState = parameters.easeInTimeLock
                ? EaseState.EasingInHoldBeforeEasingOut
                : parameters.easeOutDelayTime > 0f
                    ? EaseState.EasingOutDelay
                    : EaseState.EasingOut;

            m_ProviderRecords.Add(new ProviderRecord(provider) { easeState = easeState });
        }

        [Conditional("UNITY_EDITOR")]
        internal void PreviewInEditor(VignetteParameters previewParameters)
        {
            if (!Application.isPlaying && gameObject.activeInHierarchy)
                UpdateTunnelingVignette(previewParameters);
        }

        protected virtual void Awake()
        {
#if UNITY_EDITOR
            UnityEditor.SceneVisibilityManager.instance.DisablePicking(gameObject, false);
#endif
            m_CurrentParameters.CopyFrom(VignetteParameters.Defaults.noEffect);
            UpdateTunnelingVignette(VignetteParameters.Defaults.noEffect);
        }

        [Conditional("UNITY_EDITOR")]
        protected virtual void Reset()
        {
            m_DefaultParameters.CopyFrom(VignetteParameters.Defaults.defaultEffect);
            m_CurrentParameters.CopyFrom(VignetteParameters.Defaults.noEffect);
            UpdateTunnelingVignette(m_DefaultParameters);
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (m_PreviewInEditor)
                PreviewInEditor(m_DefaultParameters);
            else
                PreviewInEditor(VignetteParameters.Defaults.noEffect);
        }
#endif
        
        protected virtual void Update()
        {
            if (m_ProviderRecords.Count == 0)
                return;

            const float apertureSizeMax = VignetteParameters.Defaults.apertureSizeMax;

            foreach (var record in m_ProviderRecords)
            {
                var provider = record.provider;
                var parameters = provider.vignetteParameters ?? m_DefaultParameters;
                var currentSize = record.dynamicApertureSize;

                switch (record.easeState)
                {
                    case EaseState.NotEasing:
                        {
                            record.dynamicApertureSize = apertureSizeMax;
                            record.dynamicEaseOutDelayTime = 0f;
                            record.easeInLockEnded = false;
                            break;
                        }

                    case EaseState.EasingIn:
                        {
                            var desiredEaseInTime = Mathf.Max(parameters.easeInTime, 0f);
                            var desiredEaseInSize = parameters.apertureSize;
                            record.easeInLockEnded = false;

                            if (desiredEaseInTime > 0f && currentSize > desiredEaseInSize)
                            {
                                var updatedSize = currentSize + (desiredEaseInSize - apertureSizeMax) / desiredEaseInTime * Time.unscaledDeltaTime;
                                record.dynamicApertureSize = updatedSize < desiredEaseInSize ? desiredEaseInSize : updatedSize;
                            }
                            else
                            {
                                record.dynamicApertureSize = desiredEaseInSize;
                            }
                            break;
                        }

                    case EaseState.EasingInHoldBeforeEasingOut:
                        {
                            if (!record.easeInLockEnded)
                            {
                                var desiredEaseInTime = Mathf.Max(parameters.easeInTime, 0f);
                                var desiredEaseInSize = parameters.apertureSize;

                                if (desiredEaseInTime > 0f && currentSize > desiredEaseInSize)
                                {
                                    var updatedSize = currentSize + (desiredEaseInSize - apertureSizeMax) / desiredEaseInTime * Time.unscaledDeltaTime;
                                    record.dynamicApertureSize = updatedSize < desiredEaseInSize ? desiredEaseInSize : updatedSize;
                                }
                                else
                                {
                                    record.easeInLockEnded = true;
                                    if (parameters.easeOutDelayTime > 0f &&
                                        record.dynamicEaseOutDelayTime < parameters.easeOutDelayTime)
                                    {
                                        record.easeState = EaseState.EasingOutDelay;
                                        goto case EaseState.EasingOutDelay;
                                    }

                                    record.easeState = EaseState.EasingOut;
                                    goto case EaseState.EasingOut;
                                }
                            }
                            else
                            {
                                if (parameters.easeOutDelayTime > 0f)
                                {
                                    record.easeState = EaseState.EasingOutDelay;
                                    goto case EaseState.EasingOutDelay;
                                }

                                record.easeState = EaseState.EasingOut;
                                goto case EaseState.EasingOut;
                            }
                            break;
                        }

                    case EaseState.EasingOutDelay:
                        {
                            var currentDelayTime = record.dynamicEaseOutDelayTime;
                            var desiredEaseOutDelayTime = Mathf.Max(parameters.easeOutDelayTime, 0f);

                            if (desiredEaseOutDelayTime > 0f && currentDelayTime < desiredEaseOutDelayTime)
                            {
                                currentDelayTime += Time.unscaledDeltaTime;
                                record.dynamicEaseOutDelayTime = currentDelayTime > desiredEaseOutDelayTime
                                    ? desiredEaseOutDelayTime
                                    : currentDelayTime;
                            }

                            if (record.dynamicEaseOutDelayTime >= desiredEaseOutDelayTime)
                            {
                                record.easeState = EaseState.EasingOut;
                                goto case EaseState.EasingOut;
                            }
                            break;
                        }

                    case EaseState.EasingOut:
                        {
                            var desiredEaseOutTime = Mathf.Max(parameters.easeOutTime, 0f);
                            var startSize = parameters.apertureSize;

                            if (desiredEaseOutTime > 0f && currentSize < apertureSizeMax)
                            {
                                var updatedSize = currentSize + (apertureSizeMax - startSize) / desiredEaseOutTime * Time.unscaledDeltaTime;
                                record.dynamicApertureSize = updatedSize > apertureSizeMax ? apertureSizeMax : updatedSize;
                            }
                            else
                            {
                                record.dynamicApertureSize = apertureSizeMax;
                            }

                            if (record.dynamicApertureSize >= apertureSizeMax)
                                record.easeState = EaseState.NotEasing;

                            break;
                        }

                    default:
                        Assert.IsTrue(false, $"Unhandled {nameof(EaseState)}={record.easeState}");
                        break;
                }
            }

            var minDynamicApertureSize = apertureSizeMax;
            ProviderRecord minRecord = null;
            foreach (var record in m_ProviderRecords)
            {
                var apertureSize = record.dynamicApertureSize;
                if (apertureSize < minDynamicApertureSize)
                {
                    minRecord = record;
                    minDynamicApertureSize = apertureSize;
                }
            }

            if (minRecord != null)
                m_CurrentParameters.CopyFrom(minRecord.provider.vignetteParameters ?? m_DefaultParameters);

            m_CurrentParameters.apertureSize = minDynamicApertureSize;

            UpdateTunnelingVignette(m_CurrentParameters);
        }

        void UpdateTunnelingVignette(VignetteParameters parameters)
        {
            if (parameters == null)
                parameters = m_DefaultParameters;

            if (TrySetUpMaterial())
            {
                m_MeshRender.GetPropertyBlock(m_VignettePropertyBlock);
                m_VignettePropertyBlock.SetFloat(ShaderPropertyLookup.apertureSize, parameters.apertureSize);
                m_VignettePropertyBlock.SetFloat(ShaderPropertyLookup.featheringEffect, parameters.featheringEffect);
                m_VignettePropertyBlock.SetColor(ShaderPropertyLookup.vignetteColor, parameters.vignetteColor);
                m_VignettePropertyBlock.SetColor(ShaderPropertyLookup.vignetteColorBlend, parameters.vignetteColorBlend);
                m_MeshRender.SetPropertyBlock(m_VignettePropertyBlock);
            }

            var thisTransform = transform;
            var localPosition = thisTransform.localPosition;
            if (!Mathf.Approximately(localPosition.y, parameters.apertureVerticalPosition))
            {
                localPosition.y = parameters.apertureVerticalPosition;
                thisTransform.localPosition = localPosition;
            }
        }

        bool TrySetUpMaterial()
        {
            if (m_MeshRender == null)
                m_MeshRender = GetComponent<MeshRenderer>();
            if (m_MeshRender == null)
                m_MeshRender = gameObject.AddComponent<MeshRenderer>();

            if (m_VignettePropertyBlock == null)
                m_VignettePropertyBlock = new MaterialPropertyBlock();

            if (m_MeshFilter == null)
                m_MeshFilter = GetComponent<MeshFilter>();
            if (m_MeshFilter == null)
                m_MeshFilter = gameObject.AddComponent<MeshFilter>();

            if (m_MeshFilter.sharedMesh == null)
            {
                Debug.LogWarning("The default mesh for the TunnelingVignetteController is not set.", this);
                return false;
            }

            if (m_MeshRender.sharedMaterial == null)
            {
                var defaultShader = Shader.Find(k_DefaultShader);
                if (defaultShader == null)
                {
                    Debug.LogWarning("The default material for the TunnelingVignetteController is not set, and the default Shader: " + k_DefaultShader + " cannot be found.", this);
                    return false;
                }

                m_SharedMaterial = new Material(defaultShader)
                {
                    name = "DefaultTunnelingVignette",
                };
                m_MeshRender.sharedMaterial = m_SharedMaterial;
            }
            else
            {
                m_SharedMaterial = m_MeshRender.sharedMaterial;
            }

            return true;
        }
    }
}