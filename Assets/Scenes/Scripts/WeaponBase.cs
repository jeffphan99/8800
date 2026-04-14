using UnityEngine;
using UnityEngine.UI;
using StarterAssets;

public abstract class WeaponBase : MonoBehaviour
{
    protected Camera playerCamera;
    protected Text statusText;
    protected Text actionText;
    protected StarterAssetsInputs _input;

    private Animator _playerAnimator;

    protected void TriggerShootAnim()
    {
        if (_playerAnimator == null)
            _playerAnimator = GetComponentInParent<Animator>();
        if (_playerAnimator != null)
            _playerAnimator.SetTrigger("Shoot");

        // Broadcast shoot animation to other clients
        var switcher = GetComponentInParent<StarterAssets.WeaponSwitcher>();
        if (switcher != null)
            switcher.NotifyShoot(switcher.OwnerClientId);
    }


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