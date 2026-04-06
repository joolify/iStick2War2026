using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace iStick2War
{
    public class ButtonPressed : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {

        public bool buttonPressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            buttonPressed = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            buttonPressed = false;
        }
    }
}
