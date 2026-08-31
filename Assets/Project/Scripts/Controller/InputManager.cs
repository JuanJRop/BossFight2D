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
                SkillTreeMenu.Toggle();
                return;
            }

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (SkillTreeMenu.IsOpen)
            {
                SkillTreeMenu.Close();
                return;
            }

            EscapeMenuController.Toggle();
        }
    }
}
