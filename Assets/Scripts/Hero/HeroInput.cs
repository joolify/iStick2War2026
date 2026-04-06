using Spine;
using Spine.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace iStick2War
{
    public class HeroInput : MonoBehaviour
    {
        //https://sharpcoderblog.com/blog/unity-3d-how-to-make-mobile-touch-controls
        //https://www.youtube.com/watch?v=bp2PiFC9sSs - brackeys
        #region Inspector
        public string horizontalAxis = "Horizontal";
        public string verticalAxis = "Vertical";
        public string attackButton = "Fire1";
        public string jumpButton = "Jump";
        private string scrollWheel = "Mouse ScrollWheel";

        public int selectedWeapon = 0;

        //*public HeroModel model;

        //*public HeroView view;

        public Camera cam;

        private Skeleton skeleton;
        //Crosshair
        //*[SpineBone(dataField: "skeletonAnimation")]

        private SkeletonMecanim skeletonMecanim;

        public string crosshairBoneName;
        Bone crosshairBone;

        public EventDataReferenceAsset grenadeEvent;
        public EventDataReferenceAsset reloadEvent;
        public EventDataReferenceAsset startShootEevent;
        public EventDataReferenceAsset stopShootEevent;

        //*private Cache<int, WeaponBase> _cache = new Cache<int, WeaponBase>();
        //*private Tesla tesla;
        //*private Flamethrower flamethrower;

        //*private Flippable flippable;

        //*public Mk2 mk2;

        //*private Hero hero;

        //Buttons 
        public Button leftWeaponButton;
        public ButtonPressed leftWeaponButtonPressed;
        public RectTransform leftWeaponButtonRect;
        public Button rightWeaponButton;
        public ButtonPressed rightWeaponButtonPressed;
        public RectTransform rightWeaponButtonRect;
        public Button reloadButton;
        public ButtonPressed reloadButtonPressed;
        public RectTransform reloadButtonRect;
        public Button grenadeButton;
        public ButtonPressed grenadeButtonPressed;
        public RectTransform grenadeButtonRect;

        //Touch joystick
        //*public Joystick joystick;
        public RectTransform joystickBgRect;
        public RectTransform joystickHandleRect;

        private Vector2 touchPos = Vector2.zero;
        private Touch touch;

        void OnValidate()
        {
            //*if (model == null)
            //*model = GetComponent<HeroModel>();
        }
        #endregion

        void Start()
        {
            skeletonMecanim = GetComponent<SkeletonMecanim>();

            var skeleton = skeletonMecanim.Skeleton;

            //*if (skeletonAnimation == null) skeletonAnimation = //FIXME
            crosshairBone = skeleton.FindBone("crosshair");

            //skeletonAnimation.AnimationState.Event += HandleEvent;

            //*hero = model.transform.GetComponent<Hero>();
            //*flippable = model.transform.GetComponentInChildren<Flippable>();
            //*if (tesla == null) tesla = transform.GetComponentInChildren<Tesla>();
            //*if (flamethrower == null) flamethrower = transform.GetComponentInChildren<Flamethrower>();

            leftWeaponButton.onClick.AddListener(LeftWeaponButton);
            rightWeaponButton.onClick.AddListener(RightWeaponButton);
            reloadButton.onClick.AddListener(ReloadButton);
            grenadeButton.onClick.AddListener(GrenadeButton);
            if (leftWeaponButtonPressed == null) leftWeaponButtonPressed = leftWeaponButton.GetComponent<ButtonPressed>();
            if (rightWeaponButtonPressed == null) rightWeaponButtonPressed = rightWeaponButton.GetComponent<ButtonPressed>();
            if (reloadButtonPressed == null) reloadButtonPressed = reloadButton.GetComponent<ButtonPressed>();
            if (grenadeButtonPressed == null) grenadeButtonPressed = grenadeButton.GetComponent<ButtonPressed>();
            if (leftWeaponButtonRect == null) leftWeaponButtonRect = leftWeaponButton.GetComponent<RectTransform>();
            if (rightWeaponButtonRect == null) rightWeaponButtonRect = rightWeaponButton.GetComponent<RectTransform>();
            if (reloadButtonRect == null) reloadButtonRect = reloadButton.GetComponent<RectTransform>();
            if (grenadeButtonRect == null) grenadeButtonRect = grenadeButton.GetComponent<RectTransform>();

            //*if (joystickBgRect == null) joystickBgRect = joystick.GetComponent<FixedJoystick>().background;
            //*if (joystickHandleRect == null) joystickHandleRect = joystick.GetComponent<FixedJoystick>().handle;

            SelectWeapon();

            FaceMouse();
        }

        protected virtual void HandleEvent(Spine.TrackEntry trackEntry, Spine.Event e)
        {
            if (e.Data == startShootEevent.EventData)
            {
                //*model.isShooting = true;
                //* var weapon = GetWeapon();
                //* if (weapon != null) weapon.StartShoot(touchPos);
            }

            if (e.Data == stopShootEevent.EventData)
            {
                bool isTriggerDown = false;

#if (UNITY_IPHONE  || UNITY_ANDROID) && !UNITY_EDITOR
                isTriggerDown = Input.touchCount > 0 && (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Stationary);
#else
                isTriggerDown = Input.GetMouseButton(0);
#endif

                if (!isTriggerDown)
                {
                    //*model.isShooting = false;
                    //*if (model.isCrouching)
                    //*{
                    //*model.StopCrouchShoot();
                    //*}
                    //*else
                    //*{
                    //*model.StopShoot();
                    //*}
                }
            }

            if (e.Data == reloadEvent.EventData)
            {
                //* (model.isCrouching)
                //*{
                //*model.StopCrouchReload();
                //*}
                //*else
                //*{
                //*model.StopReload();
                //*}
                //*var weapon = GetWeapon();
                //* if (weapon != null) weapon.StartReload();
            }

            if (e.Data == grenadeEvent.EventData)
            {
                //*mk2.StartShoot(touchPos);
            }
        }



        void Update()
        {
            //*if (skeletonAnimation == null) return;

            //*if (model == null) return;

            FaceMouse();

#if (UNITY_IPHONE  || UNITY_ANDROID) && !UNITY_EDITOR
            Touch();
#else
            touchPos = new Vector2(Camera.main.ScreenToWorldPoint(Input.mousePosition).x, Camera.main.ScreenToWorldPoint(Input.mousePosition).y);
            SetCrosshair(touchPos);
#endif

            Move();

            Jump();

            Shoot();

            Crouch();

            Grenade();

            Reload();

            WeaponSwitching();
        }

        private bool IsButtonPressed()
        {
            return leftWeaponButtonPressed.buttonPressed || rightWeaponButtonPressed.buttonPressed || reloadButtonPressed.buttonPressed || grenadeButtonPressed.buttonPressed;
        }

        private bool AreButtonsPressed(int i)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(leftWeaponButtonRect, Input.GetTouch(i).position, null) ||
                RectTransformUtility.RectangleContainsScreenPoint(rightWeaponButtonRect, Input.GetTouch(i).position, null) ||
                RectTransformUtility.RectangleContainsScreenPoint(reloadButtonRect, Input.GetTouch(i).position, null) ||
                RectTransformUtility.RectangleContainsScreenPoint(grenadeButtonRect, Input.GetTouch(i).position, null);
        }

        private void Touch()
        {
            int i = 0;

            while (i < Input.touchCount)
            {
                touch = Input.GetTouch(i);

                if ((touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary) && !JoystickIsUsed(i) && !IsButtonPressed())
                {
                    touchPos = GetTouchPosition(touch.position);
                    SetCrosshair(touchPos);
                }
                i++;
            }
        }

        private void SetCrosshair(Vector2 localTouchPos)
        {

            Debug.Log("SetCrosshair: " + localTouchPos);
        //*    if (IsButtonPressed()) return;
            //*var skeletonSpacePoint = skeletonAnimation.transform.InverseTransformPoint(localTouchPos);
            //*skeletonSpacePoint.x *= skeletonAnimation.Skeleton.ScaleX;
            //*skeletonSpacePoint.y *= skeletonAnimation.Skeleton.ScaleY;

            // Use the skeletonMecanim's transform
            var skeletonSpacePoint = skeletonMecanim.transform.InverseTransformPoint(localTouchPos);

            // Adjust for Spine scale
            skeletonSpacePoint.x *= skeletonMecanim.Skeleton.ScaleX;
            skeletonSpacePoint.y *= skeletonMecanim.Skeleton.ScaleY;

            crosshairBone.SetLocalPosition(skeletonSpacePoint);
        }

        private bool JoystickIsUsed(int i)
        {
            return RectTransformUtility.RectangleContainsScreenPoint(joystickBgRect, Input.GetTouch(i).position, null) ||
                                    RectTransformUtility.RectangleContainsScreenPoint(joystickHandleRect, Input.GetTouch(i).position, null);
        }

        Vector2 GetTouchPosition(Vector2 touchPosition)
        {
            return cam.ScreenToWorldPoint(new Vector3(touchPosition.x, touchPosition.y, 0f));
        }

        private void Shoot()
        {
            if (IsButtonPressed()) return;

            bool isTriggerDown = false;
            bool isTriggerUp = false;

#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            if (Input.touchCount == 1 && (joystick.Vertical > 0 || joystick.Horizontal > 0))
            {
                isTriggerDown = false;
            }
            else
            {
                isTriggerDown = Input.touchCount > 0 && (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary);
            }
            isTriggerUp = Input.touchCount > 0 && touch.phase == TouchPhase.Ended;
#else

            isTriggerDown = Input.GetMouseButtonDown(0);
            isTriggerUp = Input.GetMouseButtonUp(0);
#endif

            //*if (isTriggerDown && !model.isShooting && !MustReload())
            //*{
            //*if (model.currentGunState == StickmanGunState.Tesla)
            //*{
            //*model.shootTesla = true;
            //*StartCoroutine(ShootTesla());
            //*}
            //*if (model.currentGunState == StickmanGunState.Flamethrower)
            //*{
            //*model.shootFlameThrower = true;
            //*StartCoroutine(ShootFlamethrower());
            //*}
            //*if (model.isCrouching)
            //*{
            //*//*model.isShooting = true;
            //*model.StartCrouchShoot();
            //*}
            //*else
            //*{
            //*model.isShooting = true;
            //*model.StartShoot();
            //*}
            //*}


            if (isTriggerUp)
            {
                //*if (model.currentGunState == StickmanGunState.Tesla)
                //*{
                //* StopCoroutine(ShootTesla());
                //* model.shootTesla = false;
                //*if (tesla != null) tesla.StopShoot();
                //*model.isShooting = false;
                //*}
                //* if (model.currentGunState == StickmanGunState.Flamethrower)
                //* {
                //*     StopCoroutine(ShootFlamethrower());
                //*   model.shootFlameThrower = false;
                //*   if (flamethrower != null) flamethrower.StopShoot();
                //*   model.isShooting = false;
                //* }
            }
        }

        private void Jump()
        {
            var jump = false;
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            jump = joystick.Vertical >= 0.5f;
#else
            jump = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W);
#endif

            if (jump)
            {
                //*if (model.isCrouching)
                //*{
                //*if (model.isShooting)
                //*{
                //*del.StartShoot();
                //*}
                //*else
                //* {
                //* model.Stand();
                //*}
                //* }
                //* else
                //*    {
                //*   hero.OnJumpInputDown();
                //*   StartCoroutine(model.TryJump());
                //*}
            }
            if (Input.GetKeyUp(KeyCode.Space) || Input.GetKeyUp(KeyCode.W))
            {
                //*hero.OnJumpInputUp();
            }
        }

        //*private WeaponBase GetWeapon()
        //*{
        //*var weapon = _cache.Get(selectedWeapon);
        //* if (weapon == null)
        //* {
        //*    if (transform.GetChild(selectedWeapon).TryGetComponent<WeaponBase>(out var temp))
        //*   {
        //* weapon = temp;
        //* _cache.Store(selectedWeapon, weapon, TimeSpan.FromMinutes(5));
        //*}
        //* }

        //* return weapon;
        //*}

        //*   IEnumerator ShootTesla()
        //*{
        //*   while (model.shootTesla)
        //*   {
        //*model.isShooting = true;
        //*      if (tesla == null) yield return null;
        //*       tesla.StartShoot(touchPos);
        //*yield return new WaitForSeconds(0.1f);
        //* }
        //*     model.isShooting = false;
        //*  }

        //*IEnumerator ShootFlamethrower()
        //*{
        //* while (model.shootFlameThrower)
        //*  {
        //*model.isShooting = true;
        //*     if (flamethrower != null) flamethrower.StartShoot(touchPos);
        //* yield return new WaitForSeconds(0.1f);
        //*}
        //*   model.isShooting = false;
        //* }

        //*private bool MustReload()
        //*{
        //*var weapon = GetWeapon();
        //*if (weapon.shouldReload)
        //*{
        //*model.isShooting = false;
        //*return true;
        //*}
        //* return false;
        //*  }

        private void Crouch()
        {
            var crouch = false;
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            crouch = joystick.Vertical <= -0.5f;
#else
            crouch = Input.GetKeyDown(KeyCode.S);
#endif

            if (crouch)
            {
                //* if (model.isShooting)
                //* {
                //*model.StartCrouchShoot();
                //*}
                //*else
                //*    {
                //*  model.CrouchIdle();
                //*}
            }
        }

        private void Move()
        {
            Vector2 directionalInput = Vector2.zero;
#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            if (joystick.Horizontal >= .2f || joystick.Horizontal <= -.2f )
            {
               directionalInput = new Vector2(joystick.Horizontal, 0f);
            }
#else
            directionalInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#endif

            //*if (model.isCrouching)
            //*{
            //*    model.TryCrouchMove(directionalInput.x);
            //*}
            //*  else
            //*  {
            //*  model.TryMove(directionalInput.x);
            //*}

            //*  hero.SetDirectionalInput(directionalInput);
        }

        private void WeaponSwitching()
        {
            int previousSelectedWeapon = selectedWeapon;

            if (Input.GetAxis(scrollWheel) < 0f)
            {
                if (selectedWeapon >= transform.childCount - 1)
                {
                    selectedWeapon = 0;
                }
                else
                {
                    selectedWeapon++;
                }
            }

            if (Input.GetAxis(scrollWheel) > 0f)
            {
                if (selectedWeapon <= 0)
                {
                    selectedWeapon = transform.childCount - 1;
                }
                else
                {
                    selectedWeapon--;
                }
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                selectedWeapon = 0;
            }

            if (Input.GetKeyDown(KeyCode.Alpha2) && transform.childCount >= 2)
            {
                selectedWeapon = 1;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) && transform.childCount >= 3)
            {
                selectedWeapon = 2;
            }

            if (Input.GetKeyDown(KeyCode.Alpha3) && transform.childCount >= 4)
            {
                selectedWeapon = 3;
            }

            if (Input.GetKeyDown(KeyCode.Alpha4) && transform.childCount >= 5)
            {
                selectedWeapon = 4;
            }

            ChangeWeapon(previousSelectedWeapon);
        }

        private void ChangeWeapon(int previousSelectedWeapon)
        {
            if (previousSelectedWeapon != selectedWeapon)
            {
                SelectWeapon();
                //* model.isShooting = false;
                //* if (model.isCrouching)
                //*  {
                //*  model.StopCrouchShoot();
                //*}
                //*else
                //*  {
                //*       model.StopShoot();
                //*}
            }
        }

        private void LeftWeaponButton()
        {
            int previousSelectedWeapon = selectedWeapon;

            if (selectedWeapon <= 0)
            {
                selectedWeapon = transform.childCount - 1;
            }
            else
            {
                selectedWeapon--;
            }

            ChangeWeapon(previousSelectedWeapon);
        }
        private void RightWeaponButton()
        {
            int previousSelectedWeapon = selectedWeapon;

            if (selectedWeapon >= transform.childCount - 1)
            {
                selectedWeapon = 0;
            }
            else
            {
                selectedWeapon++;
            }

            ChangeWeapon(previousSelectedWeapon);
        }

        private void GrenadeButton()
        {
            ThrowGrenade();
        }

        private void ReloadButton()
        {
            ReloadWeapon();
        }

        private void Grenade()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                ThrowGrenade();
            }
        }

        private void ThrowGrenade()
        {
            //*  if (model.isCrouching)
            //*  {
            //*  model.StartCrouchGrenade();
            //*}
            //*  else
            //*  {
            //*  model.Grenade();
            //*}
        }

        private void Reload()
        {
            //* if (Input.GetKeyDown(KeyCode.R) && !model.isReloading)
            //*{
            //*ReloadWeapon();
            //*}
        }

        private void ReloadWeapon()
        {
            //* model.isShooting = false;
            //*if (model.isCrouching)
            //*  {
            //*  model.StartCrouchReload();
            //*}
            //*  else
            //*  {
            //* model.StartReload();
            //*}
        }

        void FaceMouse()
        {

            int i = 0;

            Vector3 delta = Vector3.zero;

#if (UNITY_IPHONE || UNITY_ANDROID) && !UNITY_EDITOR
            while (i < Input.touchCount)
            {
                var localTouch = Input.GetTouch(i);
                if (!JoystickIsUsed(i) && !AreButtonsPressed(i))
                {
                    delta = GetTouchPosition(localTouch.position) - new Vector2(view.transform.position.x, view.transform.position.y);
                }
                i++;
            }
#else

            //*   delta = cam.ScreenToWorldPoint(Input.mousePosition) - view.transform.position;
#endif

            //* if (delta.x > 0 && !flippable.facingRight && !IsButtonPressed())
            //*{
            //*skeletonAnimation.Skeleton.ScaleX *= -1;
            //*flippable.facingRight = true;
            //*}
            //*  else if (delta.x < 0 && flippable.facingRight && !IsButtonPressed())
            //*  {
            //* skeletonAnimation.Skeleton.ScaleX *= -1;
            //* flippable.facingRight = false;
            //*}
        }

        private void SelectWeapon()
        {
            //*var previousGunState = model.currentGunState;
            //*var currentGunState = GetWeapon().gunState;

            //* if (previousGunState == StickmanGunState.Tesla && previousGunState != currentGunState)
            //* {
            //*if (model.shootTesla)
            //*{
            //*model.shootTesla = false;
            //* StopCoroutine(ShootTesla());
            //*if (tesla != null) tesla.StopShoot();
            //* }
            //*  }
            //*   if (previousGunState == StickmanGunState.Flamethrower && previousGunState != currentGunState)
            //*   {
            //*   if (model.shootFlameThrower)
            //*{
            //*     model.shootFlameThrower = false;
            //*StopCoroutine(ShootFlamethrower());
            //*if (flamethrower != null) flamethrower.StopShoot();
            //*}
            //*}

            //*    model.SwitchWeapon(currentGunState);

            //* int i = 0;
            //* foreach (Transform weapon in transform)
            //* {
            //*if (i == selectedWeapon)
            //*{
            //*weapon.gameObject.SetActive(true);
            //*}
            //*   else
            //* {
            //*weapon.gameObject.SetActive(false);
            //*}
            //*  i++;
            //* }
        }
    }
}
