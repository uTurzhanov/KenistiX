using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Шаг "Захват объекта": завершается по факту реального захвата ЛЮБОГО из нескольких предметов в
/// сцене через стандартный механизм XR Interaction Toolkit (XRBaseInteractable / XRGrabInteractable).
///
/// Важно про namespace: в разных версиях XR Interaction Toolkit 3.x классы могли лежать в разных
/// namespace'ах (были случаи реорганизации между UnityEngine.XR.Interaction.Toolkit и
/// UnityEngine.XR.Interaction.Toolkit.Interactables). Если при компиляции Unity не находит
/// XRBaseInteractable или SelectEnterEventArgs по этому using - откройте любой готовый скрипт
/// XR Interaction Toolkit в вашем проекте и посмотрите, какой именно namespace он использует
/// для этих типов в вашей версии пакета, и поправьте using ниже под него.
/// </summary>
public class ObjectGrabChecker : StepCompletionChecker
{
    [Tooltip("Предметы в сцене, ЛЮБОЙ из которых пользователь может взять - объекты с компонентом XR Grab Interactable")]
    [SerializeField] private XRBaseInteractable[] grabTargets;

    private void OnEnable()
    {
        if (grabTargets == null) return;

        foreach (XRBaseInteractable target in grabTargets)
        {
            if (target != null)
                target.selectEntered.AddListener(OnGrabbed);
        }
    }

    private void OnDisable()
    {
        if (grabTargets == null) return;

        foreach (XRBaseInteractable target in grabTargets)
        {
            if (target != null)
                target.selectEntered.RemoveListener(OnGrabbed);
        }
    }

    private void OnGrabbed(SelectEnterEventArgs args)
    {
        IsCompleted = true;
    }
}