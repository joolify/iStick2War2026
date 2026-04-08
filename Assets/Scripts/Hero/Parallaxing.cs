using UnityEngine;

namespace iStick2War
{
    public class Parallaxing : MonoBehaviour
    {
        public Transform[] backgrounds;
        private float[] parallaxScales; // The proportion of the camera's movement to move the backgrounds by.
        public float smoothing = 1f;    // How smooth the parallax is going to be . Make sure to set this to above 0.

        private Transform cam;
        private Vector3 previousCamPos; // This is going to store the position of the camera in the previous frame

        // Is called before Start(). Great for references.
        void Awake()
        {
            cam = Camera.main.transform;
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            // The previous frame had the current frame's camera position
            previousCamPos = cam.position;

            parallaxScales = new float[backgrounds.Length];

            // Assigning corresponding parallaxScales
            for (int i = 0; i < backgrounds.Length; i++)
            {
                parallaxScales[i] = backgrounds[i].position.z * -1;
            }
        }

        // Update is called once per frame
        void Update()
        {
            for (int i = 0; i < backgrounds.Length; ++i)
            {
                // The parallax is the opposite of the camera movement because the previous frame multiplied by the scale
                var parallax = (previousCamPos.x - cam.position.x) * parallaxScales[i];

                // Set a target x position, which is the current position plus the parallax
                var backgroundTargetPosX = backgrounds[i].position.x + parallax;

                // Create a target position, which is the background's current position with its target x position
                var backgroundTargetPos = new Vector3(backgroundTargetPosX, backgrounds[i].position.y, backgrounds[i].position.z);

                // Fade between current position and the target position using Lerp
                backgrounds[i].position = Vector3.Lerp(backgrounds[i].position, backgroundTargetPos, smoothing * Time.deltaTime);
            }

            // Set the previousCamPos to the camera's positionat the end of the frame
            previousCamPos = cam.position;
        }
    }
}
