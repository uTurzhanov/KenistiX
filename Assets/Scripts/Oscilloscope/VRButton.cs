using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class VRButton : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Color _activeColor = Color.green;
    [SerializeField] private Vector3 _pressedPositionOffset = new Vector3(-0.013f, 0f, 0f); // Вдавливание вниз (настрой по локальной оси кнопки!)

    [field: SerializeField] public bool IsPressed { get; private set; } = false;

    public event Action<VRButton> OnClicked; // Сюда в инспекторе перетащим методы из Core

    private XRSimpleInteractable _interactable;
    private MeshRenderer _meshRenderer;
    private Material _material;

    private Color _unpressedColor;
    private Vector3 _unpressedPosition;

    private void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        _meshRenderer = GetComponent<MeshRenderer>();

        if (_meshRenderer != null)
        {
            // Клонируем материал, чтобы изменение цвета одной кнопки не меняло цвет всех остальных
            _material = _meshRenderer.material;
            _unpressedColor = _material.color;
        }

        // Запоминаем стартовую позицию
        _unpressedPosition = transform.localPosition;

        ApplyIsPressed();
    }

    private void OnEnable()
    {
        // Подписываемся на события XR: когда луч или рука нажимает "Select" (Курок)
        _interactable.selectEntered.AddListener(OnPressDown);
    }

    private void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnPressDown);
    }

    // Срабатывает в момент нажатия
    private void OnPressDown(SelectEnterEventArgs args)
    {
        IsPressed = !IsPressed; // Переключаем состояние (Toggle)

        ApplyIsPressed();

        // Вызываем событие логики
        OnClicked?.Invoke(this);
    }

    public void SetIsPressed(bool isPressed)
    {
        IsPressed = isPressed;
        ApplyIsPressed();
    }

    private void ApplyIsPressed()
    {
        if (IsPressed)
        {
            // Вдавливаем и красим
            transform.localPosition = _unpressedPosition + _pressedPositionOffset;
            if (_material != null)
                _material.color = _activeColor;
        }
        else
        {
            // Возвращаем обратно
            transform.localPosition = _unpressedPosition;
            if (_material != null)
                _material.color = _unpressedColor;
        }
    }
}