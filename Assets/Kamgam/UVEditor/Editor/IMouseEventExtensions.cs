using UnityEngine;
using UnityEngine.UIElements;

namespace Kamgam.UVEditor
{
    /// <summary>
    /// This extension adds reliable mouse button methods to IMouseEvents.<br />
    /// // Sadly "evt.button == 0" does NOT always work. Example: for MouseMove event it's always 0, see bug report:
    /// https://issuetracker.unity3d.com/issues/movemouseevent-dot-button-returns-0-both-when-no-button-is-pressed-and-when-the-left-mouse-button-is-pressed
    /// We have to use the more accurate evt.pressedButtons Bitmask instead.
    /// See: https://discussions.unity.com/t/tutorial-resource-ui-toolkit-how-to-get-the-pressed-mouse-button-from-a-mousemoveevent/1554470
    /// </summary>
    public static class IMouseEventExtensions
    {
        /// <summary>
        /// Returns -1 if no button has been found (checks for the first 6 buttons). Returns values are compatible with
        /// the .button property, see: https://docs.unity3d.com/ScriptReference/UIElements.IMouseEvent-button.html
        /// </summary>
        /// <param name="evt"></param>
        /// <returns></returns>
        public static int GetButton(this IMouseEvent evt)
        {
            // Left
            if ((evt.pressedButtons & 1) > 0)
                return 0;

            // Right
            if ((evt.pressedButtons & 2) > 0)
                return 1;

            // Middle
            if ((evt.pressedButtons & 4) > 0)
                return 2;

            // Other
            if ((evt.pressedButtons & 8) > 0)
                return 3;
            if ((evt.pressedButtons & 16) > 0)
                return 4;
            if ((evt.pressedButtons & 32) > 0)
                return 5;
            if ((evt.pressedButtons & 64) > 0)
                return 6;
            if ((evt.pressedButtons & 128) > 0)
                return 7;
            if ((evt.pressedButtons & 256) > 0)
                return 8;

            return -1;
        }

        public static bool IsLeftPressed(this IMouseEvent evt)
        {
            return (evt.pressedButtons & 1) > 0;
        }

        public static bool IsRightPressed(this IMouseEvent evt)
        {
            return (evt.pressedButtons & 2) > 0;
        }

        public static bool IsMiddlePressed(this IMouseEvent evt)
        {
            return (evt.pressedButtons & 4) > 0;
        }
    }
}

