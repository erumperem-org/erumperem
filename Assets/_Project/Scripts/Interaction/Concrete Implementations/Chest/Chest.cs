using UnityEngine;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using DetectionSystem.Core;
using CharacterSystem.Core;

[RequireComponent(typeof(Animator))]
public class Chest : DetectionReceiver, IInteractable
{
    public bool IsOpen;
    public bool OnRange;
    public bool CanInteract => !IsOpen && OnRange;
    public Transform Transform => this.transform;
    public InteractionType InteractionType => InteractionType.Pickup;
    public Animator chestAnimator;

    public void Interact()
    {
        switch (CanInteract)
        {
            case true:
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.World, true, $"Chest [{this.name.ToUpper()}], can be opened");
                IsOpen = !IsOpen;
                this.GetComponent<Renderer>().material.color = Color.green;
                break;
            case false:
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.World, false, $"Chest [{this.name.ToUpper()}], can not be opened");
                this.GetComponent<Renderer>().material.color = Color.red;
                break;
        }
        chestAnimator.SetBool("IsOpen", IsOpen);
    }
    protected override void OnDetectionEnter(Detector detector, string shapeLabel, int shapeIndex)
    {
        base.OnDetectionEnter(detector, shapeLabel, shapeIndex);
        this.GetComponent<Renderer>().material.color = Color.yellow;

        OnRange = true;
    }

    protected override void OnDetectionExit(Detector detector, string shapeLabel, int shapeIndex)
    {
        base.OnDetectionExit(detector, shapeLabel, shapeIndex);
        this.GetComponent<Renderer>().material.color = Color.blue;
        OnRange = false;
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.E))
        {
            Interact();
        }
    }
    public static void ResetChest(Chest chest) { }
}

