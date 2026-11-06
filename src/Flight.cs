using UnityEngine;

namespace Oddhatter.FreeFlight
{
    /// <summary>
    /// Flight state and movement for the local player. This deliberately does not use vanilla debug fly
    /// (Player.m_debugFly): while debug flying you can't be damaged, stats stop updating, and every enemy you hit
    /// is flagged as cheated. Here the player stays a normal player; only the movement step is replaced.
    /// </summary>
    internal static class Flight
    {
        /// <summary>The local player is flying.</summary>
        internal static bool Active { get; private set; }

        /// <summary>Flight just ended in the air: suppress fall damage until the next real ground contact or water.</summary>
        private static bool _safeLanding;

        internal static bool IsLocalFlyer(Character character)
        {
            return Active && character != null && character == Player.m_localPlayer;
        }

        /// <summary>Called every frame from the plugin's Update.</summary>
        internal static void UpdateToggle()
        {
            Player player = Player.m_localPlayer;
            if (player == null)
            {
                // Main menu / logged out: start the next session on foot.
                Active = false;
                _safeLanding = false;
                return;
            }

            bool cantFly = player.IsDead() || player.IsAttached() || player.IsAttachedToShip();
            if (Active && (!FreeFlightPlugin.Enabled.Value || cantFly))
            {
                SetActive(player, false);
                return;
            }

            if (FreeFlightPlugin.Enabled.Value && !cantFly && player.TakeInput()
                && (ZInput.GetKeyDown(FreeFlightPlugin.ToggleKey.Value, logWarning: false)
                    || (FreeFlightPlugin.GamepadToggle.Value && BothSticksPressed())))
            {
                SetActive(player, !Active);
            }
        }

        /// <summary>
        /// L3 + R3: one stick clicked this frame while the other is held, so the two needn't land on the same frame.
        /// JoyLStick/JoyRStick are registered for every controller layout.
        /// </summary>
        private static bool BothSticksPressed()
        {
            return (ZInput.GetButtonDown("JoyLStick") && ZInput.GetButton("JoyRStick"))
                || (ZInput.GetButtonDown("JoyRStick") && ZInput.GetButton("JoyLStick"));
        }

        private static void SetActive(Player player, bool on)
        {
            Active = on;
            if (!on)
            {
                _safeLanding = true;
                // Flight keeps m_lastGroundTouch at 0, which reads as "on ground". Mark the player airborne so the
                // next real ground contact (which resets it) is what ends the safe landing.
                player.m_lastGroundTouch = 1f;
            }
            player.Message(MessageHud.MessageType.TopLeft, on ? "Flight on" : "Flight off");
        }

        /// <summary>
        /// Runs at the start of the local player's Character.UpdateMotion. Returns true when flight handled the step
        /// and the vanilla movement must be skipped.
        /// </summary>
        internal static bool UpdateMotion(Player player, float dt)
        {
            if (Active && !player.IsDead() && !player.IsTeleporting())
            {
                Move(player, dt);
                return true;
            }

            if (_safeLanding)
            {
                if (player.IsDead() || player.InLiquidSwimDepth() || player.m_lastGroundTouch < 0.1f)
                {
                    _safeLanding = false; // landed (UpdateGroundContact already ran this step, from a safe height)
                }
                else
                {
                    // A stick click (L3 + R3 toggle) or Ctrl in the air would otherwise toggle sneak for the landing.
                    if (player.m_crouchToggled)
                    {
                        player.SetCrouch(false);
                    }
                    // Fall damage is (m_maxAirAltitude - landing height); keep the peak at the current height.
                    player.m_maxAirAltitude = player.transform.position.y;
                }
            }
            return false;
        }

        /// <summary>Same movement model as vanilla Character.UpdateDebugFly, with our speeds and the user's key bindings.</summary>
        private static void Move(Player player, float dt)
        {
            // What vanilla UpdateMotion does before it branches.
            player.UpdateBodyFriction();
            player.m_sliding = false;
            player.m_wallRunning = false;
            player.m_running = false;
            player.m_walking = false;
            // Crouch is a press-to-toggle sneak in vanilla; here it means "descend", so never leave sneak switched on.
            if (player.m_crouchToggled)
            {
                player.SetCrouch(false);
            }

            float speed = FreeFlightPlugin.FlySpeed.Value * (player.m_run ? FreeFlightPlugin.SprintMultiplier.Value : 1f);
            Vector3 target = player.m_moveDir * speed;
            if (player.TakeInput())
            {
                if ((ZInput.GetButton("Jump") || ZInput.GetButton("JoyJump")) && !Hud.IsPieceSelectionVisible())
                {
                    target.y = speed;
                }
                else if (ZInput.GetButton("Crouch") || ZInput.GetButtonPressedTimer("JoyCrouch") > 0.33f)
                {
                    target.y = -speed;
                }
            }

            Rigidbody body = player.m_body;
            player.m_currentVel = Vector3.Lerp(player.m_currentVel, target, 0.5f);
            body.linearVelocity = player.m_currentVel;
            body.useGravity = false; // vanilla walking turns it back on every step once flight ends
            body.rotation = Quaternion.RotateTowards(player.transform.rotation, player.m_lookYaw, player.m_turnSpeed * dt);
            body.angularVelocity = Vector3.zero;

            player.m_lastGroundTouch = 0f;
            player.m_maxAirAltitude = player.transform.position.y;
            player.m_fallTimer = 0f;
            player.m_zanim.SetBool(Character.s_animatorFalling, value: false);
            player.UpdateEyeRotation();
        }
    }
}
