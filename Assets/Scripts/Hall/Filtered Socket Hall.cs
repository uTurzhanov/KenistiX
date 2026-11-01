using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class TagFilteredSocketHall : XRSocketInteractor
{

    [Header("Фильтр по тегам")]
    [Tooltip("Список тегов. Если пуст, сокет принимает любые объекты.")]
    public List<string> allowedTags = new List<string>();

    [Header("Статус сокета (Только для чтения)")]
    [Tooltip("Показывает, вставлен ли сейчас объект в сокет.")]
    public bool isOccupied = false;

    [Tooltip("Объекта, который сейчас находится в сочете.")]
    public string connectedTag = "";
    public GameObject connectedGameObject;

    [Header("Является ли полярность определяющим девайсом(plus/minus)")]
    public bool isPSU = false;
    public string terminal = "";

    public CircuitControllerHall CCH;

    

    [Header("Случайное смещение при вставке")]
    public bool enableRandomOffset = false;
    public Vector3 minPositionOffset;
    public Vector3 maxPositionOffset;
    public Vector3 minRotationOffset;
    public Vector3 maxRotationOffset;

    private Transform effectiveAttachTransform;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;

    protected override void Awake()
    {
        base.Awake();
        // Определяем, какой трансформ используется для крепления, и запоминаем его базу
        effectiveAttachTransform = attachTransform != null ? attachTransform : transform;
        originalLocalPos = effectiveAttachTransform.localPosition;
        originalLocalRot = effectiveAttachTransform.localRotation;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        selectEntered.AddListener(OnObjectSocketed);
        selectExited.AddListener(OnObjectRemoved);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        selectEntered.RemoveListener(OnObjectSocketed);
        selectExited.RemoveListener(OnObjectRemoved);
    }

    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return base.CanHover(interactable) && HasAllowedTag(interactable);
    }

    public override bool CanSelect(IXRSelectInteractable interactable)
    {
        return base.CanSelect(interactable) && HasAllowedTag(interactable);
    }

    private bool HasAllowedTag(IXRInteractable interactable)
    {
        if (allowedTags == null || allowedTags.Count == 0)
            return true;

        Transform t = interactable.transform;
        while (t != null)
        {
            if (allowedTags.Contains(t.tag))
                return true;
            t = t.parent;
        }

        return false;
    }

    private void OnObjectSocketed(SelectEnterEventArgs args)
    {
        isOccupied = true;
        connectedTag = GetMatchedTag(args.interactableObject);
        connectedGameObject = args.interactableObject.transform.gameObject;
        CCH.checkCircuit();


        if (isPSU)
        {
            connectedGameObject.GetComponent<WirePolarity>().polarity = terminal;
            connectedGameObject.GetComponent<WirePolarity>().otherEnd.polarity = terminal;
        }

        // Применяем случайное смещение к точке крепления
        if (enableRandomOffset)
        {
            Vector3 randomPos = new Vector3(
                Random.Range(minPositionOffset.x, maxPositionOffset.x),
                Random.Range(minPositionOffset.y, maxPositionOffset.y),
                Random.Range(minPositionOffset.z, maxPositionOffset.z)
            );

            Vector3 randomRot = new Vector3(
                Random.Range(minRotationOffset.x, maxRotationOffset.x),
                Random.Range(minRotationOffset.y, maxRotationOffset.y),
                Random.Range(minRotationOffset.z, maxRotationOffset.z)
            );

            effectiveAttachTransform.localPosition = originalLocalPos + randomPos;
            effectiveAttachTransform.localRotation = originalLocalRot * Quaternion.Euler(randomRot);
        }

    }

    private void OnObjectRemoved(SelectExitEventArgs args)
    {
        isOccupied = false;
        connectedTag = "";
        CCH.checkCircuit();

        // Возвращаем точку крепления на исходное место
        if (enableRandomOffset)
        {
            effectiveAttachTransform.localPosition = originalLocalPos;
            effectiveAttachTransform.localRotation = originalLocalRot;
        }
        if (isPSU)
        {
            connectedGameObject.GetComponent<WirePolarity>().polarity = "";
            connectedGameObject.GetComponent<WirePolarity>().otherEnd.polarity = "";
        }
    }

    private string GetMatchedTag(IXRInteractable interactable)
    {
        Transform t = interactable.transform;
        while (t != null)
        {
            if (allowedTags.Contains(t.tag))
                return t.tag;
            t = t.parent;
        }
        return "Untagged";
    }
}