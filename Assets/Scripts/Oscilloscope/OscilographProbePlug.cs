using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class OscilloscopePatchCable : MonoBehaviour
{
    [SerializeField] private OscilloscopeCore oscilloscope;
    [SerializeField] private XRGrabInteractable plugA;
    [SerializeField] private XRGrabInteractable plugB;

    [Header("Теги BNC-гнезд на осциллографе")]
    [SerializeField] private string ch1SocketTag = "CH1";
    [SerializeField] private string ch2SocketTag = "CH2";

    private EditChannel _activeChannel = EditChannel.None;
    private ISignalSource _activeSource;

    private void Awake()
    {
        plugA.selectEntered.AddListener(OnPlugEntered);
        plugA.selectExited.AddListener(OnPlugExited);
        plugB.selectEntered.AddListener(OnPlugEntered);
        plugB.selectExited.AddListener(OnPlugExited);
    }

    private void OnPlugEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is not MonoBehaviour socket) return;

        // Проверяем, вставлен ли конец в гнездо осциллографа (по тегу)
        if (socket.tag == ch1SocketTag)
            _activeChannel = EditChannel.CH1;
        else if (socket.tag == ch2SocketTag)
            _activeChannel = EditChannel.CH2;

        // Проверяем, вставлен ли конец в источник сигнала
        if (socket.TryGetComponent(out SignalOutputPort port) && port.Source != null)
            _activeSource = port.Source;

        UpdateConnection();
    }

    private void OnPlugExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is not MonoBehaviour socket) return;

        if (socket.tag == ch1SocketTag || socket.tag == ch2SocketTag)
        {
            ClearCurrentChannel();
            _activeChannel = EditChannel.None;
        }

        if (socket.TryGetComponent(out SignalOutputPort _))
        {
            ClearCurrentChannel();
            _activeSource = null;
        }
    }

    private void UpdateConnection()
    {
        if (_activeChannel == EditChannel.None || _activeSource == null) return;

        if (_activeChannel == EditChannel.CH1)
            oscilloscope.SetChannel1Source(_activeSource);
        else if (_activeChannel == EditChannel.CH2)
            oscilloscope.SetChannel2Source(_activeSource);
    }

    private void ClearCurrentChannel()
    {
        if (_activeChannel == EditChannel.CH1)
            oscilloscope.ClearChannel1Source();
        else if (_activeChannel == EditChannel.CH2)
            oscilloscope.ClearChannel2Source();
    }

    private void OnDestroy()
    {
        if (plugA != null)
        {
            plugA.selectEntered.RemoveListener(OnPlugEntered);
            plugA.selectExited.RemoveListener(OnPlugExited);
        }
        if (plugB != null)
        {
            plugB.selectEntered.RemoveListener(OnPlugEntered);
            plugB.selectExited.RemoveListener(OnPlugExited);
        }
    }
}