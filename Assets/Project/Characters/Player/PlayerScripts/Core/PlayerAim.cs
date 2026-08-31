using System;
using UnityEngine;

namespace Project.Characters.Player.PlayerScripts.Controller
{
    public class PlayerAim : MonoBehaviour
    {
        private const int CursorSize = 32;
        private static Texture2D aimCursor;

        private void OnEnable()
        {
            Cursor.SetCursor(GetAimCursor(), new Vector2(CursorSize * 0.5f, CursorSize * 0.5f), CursorMode.Auto);
        }

        private void OnDisable()
        {
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }

        private void Update()
        {
            Aim();
        }

        private void Aim()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null) return;

            Vector3 mousePosition = Input.mousePosition;
            mousePosition.z = transform.position.z - mainCamera.transform.position.z;
            Vector3 aimVector = mainCamera.ScreenToWorldPoint(mousePosition);
            aimVector.z = 0;

            Vector3 direction = aimVector - transform.position;
            if (direction.sqrMagnitude < 0.001f) return;

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private static Texture2D GetAimCursor()
        {
            if (aimCursor != null) return aimCursor;

            aimCursor = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new(0f, 0f, 0f, 0f);
            Color dark = new(0.02f, 0.02f, 0.02f, 0.95f);
            Color bright = new(0.15f, 0.95f, 1f, 1f);
            for (int y = 0; y < CursorSize; y++)
            {
                for (int x = 0; x < CursorSize; x++)
                {
                    aimCursor.SetPixel(x, y, clear);
                }
            }

            DrawCursorLine(16, 3, 16, 10, dark);
            DrawCursorLine(16, 22, 16, 29, dark);
            DrawCursorLine(3, 16, 10, 16, dark);
            DrawCursorLine(22, 16, 29, 16, dark);
            DrawCursorLine(16, 4, 16, 9, bright);
            DrawCursorLine(16, 23, 16, 28, bright);
            DrawCursorLine(4, 16, 9, 16, bright);
            DrawCursorLine(23, 16, 28, 16, bright);
            DrawCursorPixel(15, 15, dark);
            DrawCursorPixel(16, 15, dark);
            DrawCursorPixel(15, 16, dark);
            DrawCursorPixel(16, 16, bright);

            aimCursor.Apply(false, true);
            return aimCursor;
        }

        private static void DrawCursorLine(int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Math.Abs(x1 - x0);
            int dy = -Math.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;

            while (true)
            {
                DrawCursorPixel(x0, y0, color);
                if (x0 == x1 && y0 == y1) break;
                int error2 = 2 * error;
                if (error2 >= dy)
                {
                    error += dy;
                    x0 += sx;
                }

                if (error2 <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        private static void DrawCursorPixel(int x, int y, Color color)
        {
            if (aimCursor == null || x < 0 || x >= CursorSize || y < 0 || y >= CursorSize) return;
            aimCursor.SetPixel(x, y, color);
        }
    }
}
