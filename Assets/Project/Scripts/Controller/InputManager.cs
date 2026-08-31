using UnityEngine;
using Project.Scripts.Progression;

namespace Project.Scripts.Controller
{
    public class InputManager : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.P))
            {
                CharacterStatusMenu.Toggle();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (CharacterStatusMenu.IsOpen)
            {
                CharacterStatusMenu.Close();
                return;
            }

            EscapeMenuController.Toggle();
        }
    }
}
