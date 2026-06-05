using UnityEngine;

namespace iStick2War_V2
{
    // Shared LifeOver click routing so root continue handlers do not steal Go to shop clicks.
    internal static class LifeOverUiClickRouting_V2
    {
        internal static bool IsBlanketLifeOverRoot(GameObject gameObject)
        {
            return gameObject != null &&
                   gameObject.name.Equals("LifeOver V2", System.StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsPointerOverGoToShopButton()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return false;
            }

            Vector3 world = camera.ScreenToWorldPoint(Input.mousePosition);
            Collider2D[] hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];
                if (hit != null && hit.GetComponentInParent<LifeOverGoToShopButton_V2>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
