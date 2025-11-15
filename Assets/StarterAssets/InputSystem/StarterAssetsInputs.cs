using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
namespace StarterAssets
{
    public class StarterAssetsInputs : MonoBehaviour
    {
        [Header("Character Input Values")]
        public Vector2 move;
        public Vector2 look;
        public bool jump;
        public bool sprint;

        [Header("Movement Settings")]
        public bool analogMovement;

        [Header("Mouse Cursor Settings")]
        public bool cursorLocked = true;
        public bool cursorInputForLook = true;

        [Header("Combat Input Values")]
        public bool fire;
        public bool reload;

        [Header("Interaction Input Values")]
        public bool interact;

        [Header("Weapon Switching Input Values")]
        public int weaponSlotSelected = -1; // -1 means no slot selected this frame

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}
		
		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}
		
		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}
		
		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}
		
		public void OnFire(InputValue value)
		{
			FireInput(value.isPressed);
		}
		
		public void OnReload(InputValue value)
		{
			ReloadInput(value.isPressed);
		}
		
		public void OnInteract(InputValue value)
		{
			InteractInput(value.isPressed);
		}
		
		public void OnWeaponSlot1(InputValue value)
		{
			if (value.isPressed)
				WeaponSlotInput(0);
		}
		
		public void OnWeaponSlot2(InputValue value)
		{
			if (value.isPressed)
				WeaponSlotInput(1);
		}
		
		public void OnWeaponSlot3(InputValue value)
		{
			if (value.isPressed)
				WeaponSlotInput(2);
		}
#endif

        public void MoveInput(Vector2 newMoveDirection)
        {
            move = newMoveDirection;
        }

        public void LookInput(Vector2 newLookDirection)
        {
            look = newLookDirection;
        }

        public void JumpInput(bool newJumpState)
        {
            jump = newJumpState;
        }

        public void SprintInput(bool newSprintState)
        {
            sprint = newSprintState;
        }

        public void FireInput(bool newFireState)
        {
            fire = newFireState;
        }

        public void ReloadInput(bool newReloadState)
        {
            reload = newReloadState;
        }

        public void InteractInput(bool newInteractState)
        {
            interact = newInteractState;
        }

        public void WeaponSlotInput(int slotIndex)
        {
            weaponSlotSelected = slotIndex;
        }

        public int GetWeaponSlotInput()
        {
            int slot = weaponSlotSelected;
            weaponSlotSelected = -1; // Reset after reading
            return slot;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            SetCursorState(cursorLocked);
        }

        private void SetCursorState(bool newState)
        {
            Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
        }
    }
}