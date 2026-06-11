using ABI_RC.API;
using ABI_RC.Core.Savior;
using ABI_RC.Core.Player;
using ABI_RC.Systems.Movement;
using UnityEngine;

namespace Bunnyhop
{
    [DisallowMultipleComponent]
    sealed class BunnyhopComponent : MonoBehaviour
    {
        BetterBetterCharacterController _controller;
        bool _wasGrounded;
        float _groundedSince = -1f;

        void Awake()
        {
            AvatarAPI.OnLocalPlayerAvatarLoaded += OnLocalAvatarLoaded;
            AvatarAPI.OnLocalPlayerAvatarClear += OnLocalAvatarClear;
            UpdateCurrentAvatar(MetaPort.Instance != null ? MetaPort.Instance.currentAvatarGuid : null);
        }

        void OnDestroy()
        {
            AvatarAPI.OnLocalPlayerAvatarLoaded -= OnLocalAvatarLoaded;
            AvatarAPI.OnLocalPlayerAvatarClear -= OnLocalAvatarClear;
        }

        void LateUpdate()
        {
            var playerSetup = PlayerSetup.Instance;
            var controller = playerSetup != null ? playerSetup.CharacterController : null;

            if (controller == null)
            {
                ResetState();
                return;
            }

            if (_controller != controller)
            {
                _controller = controller;
                _wasGrounded = controller.IsGrounded();
                _groundedSince = _wasGrounded ? Time.time : -1f;
                return;
            }

            bool grounded = controller.IsGrounded();

            if (!Settings.ActiveForCurrentAvatar || !CanBunnyhop(controller))
            {
                _wasGrounded = grounded;
                _groundedSince = grounded ? Time.time : -1f;
                return;
            }

            if (grounded)
            {
                if (!_wasGrounded)
                    _groundedSince = Time.time;

                _wasGrounded = true;
                return;
            }

            bool jumpedThisFrame = _wasGrounded && controller.IsFalling();
            bool chainExpired = _groundedSince >= 0f &&
                Time.time - _groundedSince > Settings.GroundResetDelay;

            _wasGrounded = false;
            _groundedSince = -1f;

            if (!jumpedThisFrame || chainExpired)
                return;

            ApplySpeedBoost(controller);
        }

        static bool CanBunnyhop(BetterBetterCharacterController controller)
        {
            return controller.CanMove() &&
                !controller.IsFlying() &&
                !controller.IsSwimming() &&
                !controller.IsSitting();
        }

        static void ApplySpeedBoost(BetterBetterCharacterController controller)
        {
            Vector3 velocity = controller.GetVelocity();
            Vector3 up = controller.GetUpVector();

            if (Vector3.Dot(velocity, up) <= 0f)
                return;

            Vector3 verticalVelocity = Vector3.Project(velocity, up);
            Vector3 horizontalVelocity = velocity - verticalVelocity;

            if (horizontalVelocity.sqrMagnitude < 0.01f)
                return;

            float maxSpeed = controller.GetMaxSpeed() * Settings.MaxSpeedMultiplier;
            Vector3 boostedVelocity = Vector3.ClampMagnitude(
                horizontalVelocity * Settings.JumpMultiplier,
                maxSpeed);

            controller.SetVelocity(boostedVelocity + verticalVelocity);
        }

        void ResetState()
        {
            _controller = null;
            _wasGrounded = false;
            _groundedSince = -1f;
        }

        void OnLocalAvatarLoaded(Avatar avatar, ABI_RC.API.Player player)
        {
            UpdateCurrentAvatar(avatar != null ? avatar.AvatarID : null);
        }

        void OnLocalAvatarClear(Avatar avatar, ABI_RC.API.Player player)
        {
            UpdateCurrentAvatar(null);
        }

        static void UpdateCurrentAvatar(string avatarId)
        {
            Settings.SetCurrentAvatarId(avatarId);
            QuickMenu.UpdateCurrentAvatarToggle();
        }
    }
}
