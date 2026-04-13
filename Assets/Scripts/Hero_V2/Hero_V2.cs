using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Assets.Scripts.Hero_V2
{
    /*
 * Hero_V2 Architecture Principle
 *
 * Hero_V2 acts as the COMPOSITION ROOT (entry point) of the character system.
 *
 * ❌ It MUST NOT contain any gameplay or runtime logic:
 * - Movement logic
 * - Input handling
 * - Animation logic
 * - State logic
 * - Damage logic
 *
 * ✅ It is ONLY responsible for:
 * - Creating and collecting references
 * - Initializing all subsystems
 * - Wiring dependencies between systems
 * - Providing a clear lifecycle entry point (Init / Tick)
 *
 * ---------------------------------------------------------
 * ARCHITECTURE MODEL
 *
 * Hero_V2   = Composition Root (main entry point / bootstrap)
 * Controller = Brain (decision making / gameplay logic)
 * Systems    = Organs (weapon, damage, state, etc.)
 * Model      = DNA (pure data, no behaviour)
 * View       = Body (visual representation: animation, VFX)
 *
 * ---------------------------------------------------------
 * DESIGN GOAL
 *
 * Hero_V2 should remain extremely thin and stable.
 * It should only coordinate systems, never implement logic.
 *
 * This ensures:
 * - High modularity
 * - Easy testing and debugging
 * - Clear separation of concerns
 * - Scalable architecture for future systems
 */
    public class Hero_V2 : MonoBehaviour
    {
        [Header("Core References")]
        [SerializeField] private HeroModel_V2 model;
        [SerializeField] private HeroView_V2 view;

        private HeroInput_V2 input;
        private HeroController_V2 controller;
        private HeroStateMachine_V2 stateMachine;

        private HeroMovementSystem_V2 movementSystem;
        private HeroWeaponSystem_V2 weaponSystem;

        private HeroDamageReceiver_V2 damageReceiver;
        private HeroDeathHandler_V2 deathHandler;

        private void Awake()
        {
            //BindComponents();
            //CreateSystems();
            //InitSystems();
        }

        /*
         * damageReceiver.OnDamageTaken += view.PlayHitEffect;
damageReceiver.OnDeath += deathHandler.HandleDeath;
        deathHandler.OnDeathHandled += view.PlayDeathEffect;
        deathHandler.OnDeathHandled += () => Debug.Log("GAME OVER");
        */

        //private void Update()
        //{
        //    float dt = Time.deltaTime;

        //    input.Tick();

        //    controller.Tick(dt);
        //    stateMachine.Tick(dt);

        //    movementSystem.Tick(dt);
        //    weaponSystem.Tick(dt);

        //    view.Tick(dt);
        //}

        //private void BindComponents()
        //{
        //    if (model == null)
        //        model = GetComponent<HeroModel_V2>();

        //    if (view == null)
        //        view = GetComponentInChildren<HeroView_V2>();

        //    input = GetComponent<HeroInput_V2>();
        //    damageReceiver = GetComponent<HeroDamageReceiver_V2>();
        //    deathHandler = GetComponent<HeroDeathHandler_V2>();
        //    animationForwarder = GetComponent<HeroAnimationForwarder_V2>();
        //}

        //private void CreateSystems()
        //{
        //    stateMachine = new HeroStateMachine_V2(model);

        //    movementSystem = new HeroMovementSystem_V2(model, stateMachine);
        //    weaponSystem = new HeroWeaponSystem_V2(model, stateMachine, input);

        //    controller = new HeroController_V2(
        //        model,
        //        input,
        //        stateMachine,
        //        movementSystem,
        //        weaponSystem
        //    );
        //}

        //private void InitSystems()
        //{
        //    damageReceiver.Init(model, stateMachine, deathHandler);
        //    deathHandler.Init(model, stateMachine, view);

        //    view.Init(model, stateMachine);
        //    animationForwarder.Init(view, stateMachine);
        //}
    }
}
