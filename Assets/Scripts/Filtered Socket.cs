using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

// В новых версиях XRIT (3.0+) может потребоваться раскомментировать строку ниже:
// using UnityEngine.XR.Interaction.Toolkit.Interactors; 

public class TagFilteredSocket : XRSocketInteractor
{
    public SocketController SC;


    [Header("Фильтр по тегам")]
    [Tooltip("Список тегов. Если пуст, сокет принимает любые объекты.")]
    public List<string> allowedTags = new List<string>();

    [Header("Статус сокета (Только для чтения)")]
    [Tooltip("Показывает, вставлен ли сейчас объект в сокет.")]
    public bool isOccupied = false;
    
    [Tooltip("Тег объекта, который сейчас находится в сочете.")]
    public string connectedTag = "";

    protected override void OnEnable()
    {
        base.OnEnable();
        // Подписываемся на события вставки и извлечения объекта
        selectEntered.AddListener(OnObjectSocketed);
        selectExited.AddListener(OnObjectRemoved);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // Отписываемся, чтобы избежать утечек памяти
        selectEntered.RemoveListener(OnObjectSocketed);
        selectExited.RemoveListener(OnObjectRemoved);
    }

    // Вызывается, когда объект подносят к сокету (подсветка/магнит)
    public override bool CanHover(IXRHoverInteractable interactable)
    {
        return base.CanHover(interactable) && HasAllowedTag(interactable);
    }

    // Вызывается при попытке вставить объект в сокет
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

    // Вызывается, когда объект успешно зафиксирован в сокете
    private void OnObjectSocketed(SelectEnterEventArgs args)
    {
        isOccupied = true;
        
        // Получаем тег именно того коллайдера/объекта, который подошел по фильтру
        connectedTag = GetMatchedTag(args.interactableObject);
        if(SC != null)
            SC.updCfg();
    }

    // Вызывается, когда объект достают из сокета
    private void OnObjectRemoved(SelectExitEventArgs args)
    {
        isOccupied = false;
        connectedTag = "";
        if (SC != null)
            SC.updCfg();
    }

    // Вспомогательный метод для поиска точного тега (если на объекте их несколько в иерархии)
    private string GetMatchedTag(IXRInteractable interactable)
    {
        Transform t = interactable.transform;

        while (t != null)
        {
            if (allowedTags.Contains(t.tag)) 
                return t.tag;
            t = t.parent;
        }

        return "Untagged"; // На случай, если список allowedTags пуст, но объект вставился
    }
}