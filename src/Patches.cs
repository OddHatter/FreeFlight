using HarmonyLib;
using UnityEngine;

namespace Oddhatter.FreeFlight
{
    /// <summary>Replaces the local player's movement step while flying.</summary>
    [HarmonyPatch(typeof(Character), nameof(Character.UpdateMotion))]
    internal static class Character_UpdateMotion_Patch
    {
        private static bool Prefix(Character __instance, float dt)
        {
            if (__instance != Player.m_localPlayer)
            {
                return true;
            }
            return !Flight.UpdateMotion((Player)__instance, dt);
        }
    }

    /// <summary>
    /// Flight holds m_lastGroundTouch at 0 (so the game treats you as grounded, as debug fly does), which would let
    /// Space also trigger a real jump. Space means "climb" while flying.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Jump))]
    internal static class Character_Jump_Patch
    {
        private static bool Prefix(Character __instance)
        {
            return !Flight.IsLocalFlyer(__instance);
        }
    }

    /// <summary>
    /// "Carry anything": while flying you are never overweight. Otherwise moving while overweight drains stamina and,
    /// at zero stamina, Player.CanMove stops you mid-air.
    /// </summary>
    [HarmonyPatch(typeof(Player), nameof(Player.IsEncumbered))]
    internal static class Player_IsEncumbered_Patch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (__result && Flight.IsLocalFlyer(__instance))
            {
                __result = false;
            }
        }
    }

    /// <summary>
    /// The harpoon's pull runs on whichever game owns the hit creature. Other players don't have this mod, so when the
    /// local player harpoons a creature, take ownership first: Character.Damage then routes the hit to this game, the
    /// harpoon effect is created here, and the tow below can run. Same handover vanilla uses for item pickups.
    /// </summary>
    [HarmonyPatch(typeof(Character), nameof(Character.Damage))]
    internal static class Character_Damage_Patch
    {
        private static void Prefix(Character __instance, HitData hit)
        {
            // Character.Damage runs for every hit in the game: cheapest checks first.
            if (hit == null || hit.m_statusEffectHash == 0
                || !FreeFlightPlugin.Enabled.Value || !FreeFlightPlugin.DragHarpooned.Value)
            {
                return;
            }
            Player local = Player.m_localPlayer;
            if (local == null || __instance.IsPlayer() || __instance.IsBoss()) // never take over another player's character
            {
                return;
            }
            ZNetView nview = __instance.m_nview;
            if (nview == null || !nview.IsValid() || nview.IsOwner())
            {
                return;
            }
            if (hit.GetAttacker() != local || ObjectDB.instance == null
                || !(ObjectDB.instance.GetStatusEffect(hit.m_statusEffectHash) is SE_Harpooned))
            {
                return;
            }
            nview.ClaimOwnership();
        }
    }

    /// <summary>
    /// Tows a harpooned creature through the air while the local player flies. Vanilla pulls only sideways
    /// (noUpForce), snaps the line 4 m past its starting length, drains stamina and lets go at zero stamina; all of
    /// that is replaced while flying. Releasing by attacking or blocking (SE_Harpooned.IsDone) is unchanged.
    /// </summary>
    [HarmonyPatch(typeof(SE_Harpooned), nameof(SE_Harpooned.UpdateStatusEffect))]
    internal static class SE_Harpooned_UpdateStatusEffect_Patch
    {
        /// <summary>How fast the line closes the gap beyond rope length (1/s), and the cap on that catch-up speed.</summary>
        private const float Stiffness = 5f;
        private const float MaxCatchUpSpeed = 50f;

        /// <summary>Past this much beyond rope length (lag, very high speed), move the creature straight to the rope end.</summary>
        private const float SnapDistance = 40f;

        private static bool Prefix(SE_Harpooned __instance, float dt)
        {
            if (!FreeFlightPlugin.Enabled.Value || !FreeFlightPlugin.DragHarpooned.Value || !Flight.Active)
            {
                return true;
            }
            Character attacker = __instance.m_attacker;
            Character target = __instance.m_character;
            if (attacker == null || attacker != Player.m_localPlayer || target == null || target.m_body == null)
            {
                return true;
            }

            BaseUpdate(__instance, dt);

            Rigidbody body = target.m_body;
            float rope = FreeFlightPlugin.RopeLength.Value;
            Vector3 toAttacker = attacker.transform.position - body.position;
            float distance = toAttacker.magnitude;
            if (distance > rope)
            {
                Vector3 dir = toAttacker / distance;
                float excess = distance - rope;
                if (excess > SnapDistance)
                {
                    body.position = attacker.transform.position - dir * rope;
                    body.linearVelocity = attacker.m_body.linearVelocity;
                }
                else
                {
                    body.linearVelocity = attacker.m_body.linearVelocity + dir * Mathf.Min(excess * Stiffness, MaxCatchUpSpeed);
                }
            }

            // Keep the vanilla line length in step with ours, so after landing the vanilla harpoon carries on from
            // here instead of snapping at once (it breaks when distance exceeds this by 4 m).
            __instance.m_baseDistance = rope;

            if (__instance.m_line != null)
            {
                __instance.m_line.SetSlack((1f - Utils.LerpStep(rope / 2f, rope, distance)) * __instance.m_maxLineSlack);
            }
            return false;
        }

        /// <summary>StatusEffect.UpdateStatusEffect (the base call the skipped original would have made).</summary>
        private static void BaseUpdate(StatusEffect se, float dt)
        {
            se.m_time += dt;
            if (se.m_repeatInterval > 0f && !string.IsNullOrEmpty(se.m_repeatMessage))
            {
                se.m_msgTimer += dt;
                if (se.m_msgTimer > se.m_repeatInterval)
                {
                    se.m_msgTimer = 0f;
                    se.m_character.Message(se.m_repeatMessageType, se.m_repeatMessage);
                }
            }
        }
    }
}
