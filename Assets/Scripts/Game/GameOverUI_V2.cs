using System;
using UnityEngine;
using TMPro;

namespace iStick2War_V2
{
    /// <summary>
    /// Place on the Game Over root (or assign <see cref="_root"/>). Hides itself in <see cref="Awake"/>; call
    /// <see cref="Show"/> when the run ends (e.g. from <see cref="WaveManager_V2"/>).
    /// Main menu from game over: add <see cref="MainMenuNavButton_V2"/> + <see cref="Collider2D"/> on
    /// <c>btn_main_menu_gameOver</c> with <see cref="MainMenuNavButton_V2.MenuAction.ReturnToMainMenu"/> (same pattern as Play).
    /// </summary>
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

        /// <summary>Shows the game-over root and the configured TMP labels.</summary>
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
