using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public abstract class WeaponBase : MonoBehaviour
{
    protected Camera playerCamera;
    protected Text statusText; 
    protected Text actionText; 
    protected StarterAssetsInputs _input;


    public virtual void SetSharedReferences(Camera cam, Text status, Text action)
    {
        playerCamera = cam;
        statusText = status;
        actionText = action;

 
        _input = GetComponentInParent<StarterAssetsInputs>();
        if (_input == null)
        {
            Transform parent = transform.parent;
            while (parent != null && _input == null)
            {
                _input = parent.GetComponent<StarterAssetsInputs>();
                parent = parent.parent;
            }
        }
    }


    public virtual void OnEquip()
    {
        UpdateStatusUI();
        if (actionText != null)
        {
            actionText.enabled = false;
        }
    }

    public virtual void OnUnequip()
    {
        if (actionText != null)
        {
            actionText.enabled = false;
        }
    }

    public abstract void UpdateStatusUI();

    public abstract void PrimaryAction();

    public abstract void SecondaryAction();

    public virtual bool IsBusy()
    {
        return false;
    }
}