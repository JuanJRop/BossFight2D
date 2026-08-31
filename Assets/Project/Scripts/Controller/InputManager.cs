using UnityEngine;

namespace Project.Scripts.Controller
{
    public class InputManager : MonoBehaviour
    {
        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                EscapeMenuController.Toggle();
            }
        }
    }
}
