using Assets.Scripts.Enemies.Paratrooper_V2;
using iStick2War;
using Spine.Unity;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.XR;
/*
 * <summary>
     * PARATROOPER ARCHITECTURE (COMPOSITION ROOT)
     *
     * Paratrooper (ROOT / BRAIN)
     * ├── ParatrooperModel
     * ├── ParatrooperController (AI + State Machine)
     * ├── ParatrooperDamageReceiver
     * ├── ParatrooperStateMachine
     * ├── ParatrooperView (Spine + VFX)
     * ├── BodyParts (multiple)
     * │     └── ParatrooperBodyPart
     * └── ParatrooperDeathHandler
</summary>
<remarks>
     *
     * NOTES:
     * - This class should NOT contain gameplay logic.
     * - It only coordinates sub-components.
     * - Prefer dependency references via inspector or GetComponent in Awake().
     * - Binds everything together
     * - Owns lifecycle
     * - References all sub systems

--------------------------------

# SHOOT FLOW

Player Weapon
   ↓
Raycast Hit
   ↓
ParatrooperBodyPart
   ↓
ParatrooperDamageReceiver
   ↓
ParatrooperModel (HP reduces)
   ↓
ParatrooperStateMachine (maybe change state)
   ↓
ParatrooperView (hit animation)
   ↓
ParatrooperDeathHandler (if HP <= 0)

--------------------------------

# Spine Event Flow

Spine Animation Event
        ↓
ParatrooperView (ONLY forwards event)
        ↓
ParatrooperController (interprets event)
        ↓
WeaponSystem / AI / StateMachine

--------------------------------

PARATROOPER (ROOT)
│
├── ParatrooperController  ← receives animation events
├── ParatrooperModel
├── ParatrooperStateMachine
├── ParatrooperDamageReceiver
│
├── ParatrooperView (Spine bridge ONLY)
│       └── SpineEventForwarder
│
├── WeaponSystem
├── BodyParts
│
└── ParatrooperDeathHandler

--------------------------------

# AI FLOW

ParatrooperController (Update tick)
   ↓
StateMachine decides state
   ↓
Model updates data
   ↓
View reacts visually

</remarks>
     */
public class Paratrooper : MonoBehaviour
{
    [Header("Core Systems")]
    private ParatrooperModel_V2 _model;
    [SerializeField] private ParatrooperController_V2 _controller;
    [SerializeField] private ParatrooperStateMachine_V2 _stateMachine;
    [SerializeField] private ParatrooperDamageReceiver_V2 _damageReceiver;
    [SerializeField] private ParatrooperDeathHandler_V2 _deathHandler;
    [SerializeField] private ParatrooperWeaponSystem_V2 _weaponSystem;
    [SerializeField] private ParatrooperSpineEventForwarder_V2 _spineEventForwarder;
    [SerializeField] private SkeletonAnimation _skeletonAnimation;

    [Header("View")]
    [SerializeField] private ParatrooperView_V2 _view;

    [Header("Body Parts")]
    [SerializeField] private ParatrooperBodyPart_V2[] _bodyParts;



    /*
     * Paratrooper.cs
     *  ↓
     * Initialize systems
     *    ↓
     * Controller starts AI
     */
    private void Awake()
    {
        InitializeDependencies();
        ValidateBodyPartSetup();

        WireSystems();

        _controller.StartGame();
    }

    private void InitializeDependencies()
    {
        // Ensure references exist (safe setup pattern)
        if (_model == null) _model = GetComponent<ParatrooperModel_V2>();
        if (_controller == null) _controller = GetComponent<ParatrooperController_V2>();
        if (_view == null) _view = GetComponent<ParatrooperView_V2>();
        if (_damageReceiver == null) _damageReceiver = GetComponent<ParatrooperDamageReceiver_V2>();
        if (_stateMachine == null) _stateMachine = GetComponent<ParatrooperStateMachine_V2>();
        if (_deathHandler == null) _deathHandler = GetComponent<ParatrooperDeathHandler_V2>();
        if (_spineEventForwarder == null) _spineEventForwarder = GetComponent<ParatrooperSpineEventForwarder_V2>();
        if (_weaponSystem == null) _weaponSystem = GetComponent<ParatrooperWeaponSystem_V2>();
    }

    private void WireSystems()
    {
        // Inject dependencies manually (clean + fast in Unity)
        if (_model == null)
        {
            Debug.LogError("[Paratrooper_V2] Missing ParatrooperModel_V2 component.");
            return;
        }

        // 2. Init StateMachine
        _stateMachine.Initialize(_model);

        // 3. Init DamageReceiver
        //_damageReceiver.Initialize(_model, _stateMachine);
        //FIXME

        // 4. Init Controller (brain)
        _controller.Initialize(_model, _stateMachine, _damageReceiver, _weaponSystem);

        // 5. Init View
        _view.Initialize(_stateMachine);

        // 6. Init DeathHandler
        _deathHandler.Initialize(_stateMachine);

        // 7. Wire BodyParts → DamageReceiver
        //foreach (var part in _bodyParts)
        //{
        //    part.Initialize(_damageReceiver);
        //}
        //FIXME

        // 8. Wire Spine Events → Controller
        _spineEventForwarder.Init(_controller, _skeletonAnimation);

        // Init WeaponSystem
        //FIXME
        //_weaponSystem.Initialize(_controller, _stateMachine);

        // 9. Hook events (StateMachine → View & Death)
        _stateMachine.OnStateChanged += HandleStateChanged;
    }

    private void OnDestroy()
    {
        if (_stateMachine != null)
            _stateMachine.OnStateChanged -= HandleStateChanged;
    }

    /*
     * Controller → ChangeState()
     *             ↓
     * StateMachine updates state
     *             ↓
     * OnStateChanged EVENT fires
     *             ↓
     * View / DeathHandler / others react
    */
    private void HandleStateChanged(StickmanBodyState from, StickmanBodyState to)
    {
        Debug.Log($"HandleStateChanged: State changed: {from} → {to}");
        // Death handling
        if (to == StickmanBodyState.Die)
        {
            _deathHandler.Die();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
    }


    /* Update is called once per frame
     * Paratrooper.Update()
     * ↓
     * Controller.Tick()
     * ↓
     * StateMachine decides
     * ↓
     * View updates animation
     * */
    void Update()
    {
        _controller.Tick(Time.deltaTime);
    }

    private void ValidateBodyPartSetup()
    {
        if (_bodyParts == null || _bodyParts.Length == 0)
        {
            _bodyParts = GetComponentsInChildren<ParatrooperBodyPart_V2>(true);
        }

        int total = _bodyParts != null ? _bodyParts.Length : 0;
        int missingCollider = 0;
        int missingReceiver = 0;
        int wrongLayer = 0;

        for (int i = 0; i < total; i++)
        {
            var part = _bodyParts[i];
            if (part == null)
            {
                continue;
            }

            if (part.GetComponent<Collider2D>() == null)
            {
                missingCollider++;
                Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' has no Collider2D.");
            }

            if (part.GetComponentInParent<ParatrooperDamageReceiver_V2>() == null)
            {
                missingReceiver++;
                Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' cannot find ParatrooperDamageReceiver_V2 in parents.");
            }

            int expectedLayer = LayerMask.NameToLayer("EnemyBodyPart");
            if (expectedLayer >= 0 && part.gameObject.layer != expectedLayer)
            {
                wrongLayer++;
                Debug.LogWarning($"[Paratrooper_V2] Body part '{part.name}' layer is '{LayerMask.LayerToName(part.gameObject.layer)}', expected 'EnemyBodyPart'.");
            }
        }

        Debug.Log($"[Paratrooper_V2] BodyPart setup: total={total}, missingCollider={missingCollider}, missingReceiver={missingReceiver}, wrongLayer={wrongLayer}");
    }
}
