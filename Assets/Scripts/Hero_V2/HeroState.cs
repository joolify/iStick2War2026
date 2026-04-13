using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Hero_V2
{
    /*
 * HeroState (Gameplay State Enumeration)
 *
 * PURPOSE:
 * Defines the high-level gameplay states of the Hero character.
 *
 * ---------------------------------------------------------
 * STATES:
 *
 * - Idle      : Hero is stationary and not performing actions
 * - Moving    : Hero is currently moving
 * - Shooting  : Hero is actively firing a weapon
 * - Reloading : Hero is reloading a weapon
 * - Dead      : Hero is in a death state and no longer active
 *
 * ---------------------------------------------------------
 * DESIGN NOTES:
 *
 * - This enum represents ONLY gameplay state
 * - It should be used by StateMachine and Controller systems
 * - It does NOT contain animation or visual logic
 * - It is a pure logical representation of character state
 */
    public enum HeroState
    {
        Idle,
        Moving,
        Shooting,
        Reloading,
        Dead
    }
}
