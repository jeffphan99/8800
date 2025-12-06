using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{
    [Header("Door Settings")]
    public Transform doorTransform;
    public Vector3 openPosition = new Vector3(0, 0, 2);
    public float openSpeed = 3f;
    public bool startsOpen = false;

    private Vector3 closedPosition;
    public bool isOpen { get; private set; } 
    private bool isMoving = false;

    void Start()
    {
        if (doorTransform == null)
        {
            doorTransform = transform;
        }

        closedPosition = doorTransform.localPosition;
        isOpen = startsOpen;

        if (startsOpen)
        {
            doorTransform.localPosition = closedPosition + openPosition;
        }
    }

    public void ToggleDoor()
    {
        if (!isMoving)
        {
            StartCoroutine(MoveDoor(!isOpen));
        }
    }

    public void OpenDoor()
    {
        if (!isOpen && !isMoving)
        {
            StartCoroutine(MoveDoor(true));
        }
    }

    public void CloseDoor()
    {
        if (isOpen && !isMoving)
        {
            StartCoroutine(MoveDoor(false));
        }
    }

    IEnumerator MoveDoor(bool open)
    {
        isMoving = true;
        Vector3 targetPosition = open ? closedPosition + openPosition : closedPosition;

        while (Vector3.Distance(doorTransform.localPosition, targetPosition) > 0.01f)
        {
            doorTransform.localPosition = Vector3.Lerp(
                doorTransform.localPosition,
                targetPosition,
                Time.deltaTime * openSpeed
            );
            yield return null;
        }

        doorTransform.localPosition = targetPosition;
        isOpen = open;
        isMoving = false;
    }
}