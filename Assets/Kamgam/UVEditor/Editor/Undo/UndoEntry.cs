using UnityEditor;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Kamgam.UVEditor
{
    public class UndoEntry
    {
        public List<Action> RedoActions = new List<Action>();
        public List<Action> UndoActions = new List<Action>();

        public void Redo()
        {
            foreach (var func in RedoActions)
            {
                func?.Invoke();
            }
        }

        public void Undo()
        {
            for (int i = UndoActions.Count-1; i >= 0; i--)
            {
                UndoActions[i]?.Invoke();
            }
        }

        public void AddRedoAction(System.Action func)
        {
            RedoActions.Add(func);
        }

        public void AddUndoAction(System.Action func)
        {
            UndoActions.Add(func);
        }
    }
}
