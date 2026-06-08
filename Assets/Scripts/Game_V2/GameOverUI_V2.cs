using System;
using UnityEngine;
using TMPro;

namespace iStick2War_V2
{
    /*
 * GameOverUI_V2 (Dedicated game over panel)
 *
 * PURPOSE:
 * Hides its root in Awake; WaveManager_V2 calls Show when the run ends so TMP title/continue labels appear.
 * Pair with MainMenuNavButton_V2 + Collider2D on btn_main_menu_gameOver using MenuAction.ReturnToMainMenu for reload.
 *
 * ---------------------------------------------------------
 * ❌ MUST NOT
 *
 * - Decide when the player loses (WaveManager_V2 owns GameOver transition).
 *
 * ---------------------------------------------------------
 * NAVIGATION (Game_V2)
 *
 * When to show / hide → WaveManager_V2.cs
 * Reload / return UX → MainMenuNavButton_V2.cs (ReturnToMainMenu) + MainMenu_V2.cs
 *
 * ---------------------------------------------------------
 * DESIGN PRINCIPLE
 *
 * Self-contained UI MonoBehaviour separate from hero-death-only overlays managed inline in WaveManager_V2.
 */
    public sealed class GameOverUI_V2 : MonoBehaviour
    {
        private const string DefaultTitleTmpName = "txt_mainmenu_gameOver";
        private const string DefaultContinueTmpName = "txt_mainmenu_gameOver_continue";

        [Tooltip("If null, uses this GameObject.")]
        [SerializeField] private GameObject _root;

        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _continueText;

        private void Awake()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            ResolveReferencesIfNeeded();

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        // Shows the game-over root and the configured TMP labels.
        public void Show()
        {
            ResolveReferencesIfNeeded();

            if (_root != null)
            {
                _root.SetActive(true);
            }

            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(true);
            }

            if (_continueText != null)
            {
                _continueText.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            ResolveReferencesIfNeeded();

            if (_titleText != null)
            {
                _titleText.gameObject.SetActive(false);
            }

            if (_continueText != null)
            {
                _continueText.gameObject.SetActive(false);
            }

            if (_root != null)
            {
                _root.SetActive(false);
            }
        }

        private void ResolveReferencesIfNeeded()
        {
            if (_root == null)
            {
                _root = gameObject;
            }

            Transform rt = _root.transform;

            if (_titleText == null)
            {
                _titleText = FindTmpInHierarchy(rt, DefaultTitleTmpName);
            }

            if (_continueText == null)
            {
                _continueText = FindTmpInHierarchy(rt, DefaultContinueTmpName);
            }
        }

        private static TMP_Text FindTmpInHierarchy(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrWhiteSpace(objectName))
            {
                return null;
            }

            TMP_Text[] tmps = root.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < tmps.Length; i++)
            {
                TMP_Text t = tmps[i];
                if (t != null && t.gameObject.name.Equals(objectName, StringComparison.OrdinalIgnoreCase))
                {
                    return t;
                }
            }

            return null;
        }
    }
}
