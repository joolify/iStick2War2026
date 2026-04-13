using System;
using System.Collections.Generic;
using System.Text;

namespace Assets.Scripts.Hero_V2
{
    /// <summary>
    /// HeroSpineEventForwarder_V2 (Animation Event Bridge)
    /// </summary>
    /// <remarks>
    /// Acts as a bridge between Spine animation events and the gameplay systems.
    /// This component listens to Spine events and forwards them to the Controller
    /// without interpreting their meaning.
    ///
    /// ---------------------------------------------------------
    /// EVENT FLOW:
    ///
    /// Spine → HeroSpineEventForwarder_V2 → HeroController_V2 → Gameplay Systems
    ///
    /// ---------------------------------------------------------
    /// RESPONSIBILITIES:
    ///
    /// - Listens to Spine animation events
    /// - Forwards raw event identifiers to HeroController_V2
    ///
    /// ---------------------------------------------------------
    /// CONSTRAINTS:
    ///
    /// - MUST NOT contain gameplay logic
    /// - MUST NOT interpret event meaning
    /// - MUST NOT trigger systems directly (movement, combat, state changes)
    /// - MUST remain a thin forwarding layer only
    ///
    /// ---------------------------------------------------------
    /// ARCHITECTURAL ROLE:
    ///
    /// - Part of the View layer (acts as a "sensor")
    /// - Keeps animation system fully decoupled from gameplay logic
    /// - Allows animators/designers to trigger gameplay events safely via Spine
    ///
    /// ---------------------------------------------------------
    /// VISUAL FLOW:
    ///
    /// Animator (Spine)
    ///    ↓
    /// Event("Attack", "Jump", "Land", etc.)
    ///    ↓
    /// HeroSpineEventForwarder_V2
    ///    ↓
    /// HeroController_V2
    ///    ↓
    /// Gameplay Systems
    /// </remarks>
    internal class HeroSpineEventForwarder_V2
    {
    }
}
