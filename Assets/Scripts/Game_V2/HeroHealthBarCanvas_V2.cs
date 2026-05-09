namespace iStick2War_V2
{
    /*
 * HeroHealthBarCanvas_V2 (Hero-specific convenience subclass)
 *
 * PURPOSE:
 * Thin subclass of HealthBarCanvas_V2 kept for prefab compatibility; new work should prefer HealthBarCanvas_V2 with
 * HealthBarCanvasBindMode.Hero explicitly set.
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * No extra behaviour — avoids breaking serialized prefab type references while the generic bar driver evolves.
 */
    public sealed class HeroHealthBarCanvas_V2 : HealthBarCanvas_V2
    {
        protected override void Awake()
        {
            base.Awake();
        }
    }
}
