using UnityEngine;
using UnityEngine.SocialPlatforms;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ButtonFollowVisual : MonoBehaviour
{
    public Transform VisualTarget;
    public Vector3 localAxis;
    public float resetSpeed = 5;

    private Vector3 InitialLocalPos;
    private Vector3 offset;
    private Transform pokeAttachTransform;
    private bool isFreeze = false;

    private XRBaseInteractable interactable;
    private bool isFollowing = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        interactable = GetComponent<XRBaseInteractable>();
        interactable.hoverEntered.AddListener(Follow);
        interactable.hoverExited.AddListener(resetPos);
        interactable.selectEntered.AddListener(Freeze); 

        InitialLocalPos = VisualTarget.localPosition;
    }

    public void Follow(BaseInteractionEventArgs hover)
    {
        if(hover.interactorObject is XRPokeInteractor)
        {
            
            XRPokeInteractor interactor = (XRPokeInteractor)hover.interactorObject;
            isFollowing = true;
            isFreeze = false;

            pokeAttachTransform = interactor.attachTransform;
            offset = VisualTarget.position - pokeAttachTransform.position;
        }

    }

    // Update is called once per frame
    void Update()
    {

        if(isFreeze)
        return;

        if (isFollowing)
        {

            Vector3 localTargetPosition = VisualTarget.InverseTransformPoint(pokeAttachTransform.position + offset);

            Vector3 ConstrainedVectorPosition = Vector3.Project(localTargetPosition,localAxis);
            VisualTarget.position = VisualTarget.TransformPoint(ConstrainedVectorPosition);
        }
        else
        {
            VisualTarget.localPosition = Vector3.Lerp(VisualTarget.localPosition, InitialLocalPos,Time.deltaTime * resetSpeed);


        }
    }

    public void resetPos(BaseInteractionEventArgs hover)
    {
        if(hover.interactorObject is XRPokeInteractor)
        {
            isFollowing = false;
            isFreeze = false;
        }

    }



    public void Freeze(BaseInteractionEventArgs hover)
    {
        if(hover.interactorObject is XRPokeInteractor)
        {
            isFreeze = true;
        }

    }
}
