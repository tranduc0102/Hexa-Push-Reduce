using UnityEditor;
using System.Collections.Generic;
using UnityEngine;

namespace Kamgam.UVEditor
{
    public class UndoStack
    {
        static UndoStack _instance;
        public static UndoStack Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new UndoStack();
                return _instance;
            }
        }

        /// <summary>
        /// List of lists of undo entry lists (inner list is to support grouping)
        /// </summary>
        protected LinkedList<List<UndoEntry>> _undoStack = new LinkedList<List<UndoEntry>>();
        protected LinkedList<List<UndoEntry>> _redoStack = new LinkedList<List<UndoEntry>>();

        /// <summary>
        /// Parameters: start, bool undo (true = undpo, false = redo)
        /// </summary>
        public int MaxEntries;
        protected UndoEntry _currentEntry = null;
        protected int _groupCounter = 0;
        protected bool _isNewGroup = false;
        protected bool _isGrouping = false;

        public UndoStack(int maxEntries = 40)
        {
            MaxEntries = maxEntries;
        }

        /// <summary>
        /// There can only every be one group. Grouping is active for as long a the group counter is > 0. Every call to StartGroup() increases it. Every call to EndGroup() decreases it.
        /// </summary>
        public void StartGroup()
        {
            _groupCounter++;

            if(_groupCounter == 1)
                _isNewGroup = true;

            _isGrouping = true;
        }

        /// <summary>
        /// Will end the grouping only if EndGroup() has been called as often as StartGroup().
        /// </summary>
        public void EndGroup()
        {
            // Ensure we can stack groups (the outer most group will override all other groups)
            if (_groupCounter > 0)
                _groupCounter--;

            if (_groupCounter == 0)
            {
                _isNewGroup = false;
                _isGrouping = false;
            }
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public UndoEntry Peek()
        {
            if (HasUndoActions())
            {
                return _undoStack.Last.Value[0];
            }    
            else
            {
                return default;
            }
        }

        public List<UndoEntry> PeekGroup()
        {
            if (HasUndoActions())
            {
                return _undoStack.Last.Value;
            }
            else
            {
                return default;
            }
        }

        public UndoEntry PeekRedo()
        {
            if (HasRedoActions())
            {
                return _redoStack.Last.Value[0];
            }
            else
            {
                return default;
            }
        }

        public List<UndoEntry> PeekRedoGroup()
        {
            if (HasRedoActions())
            {
                return _redoStack.Last.Value;
            }
            else
            {
                return default;
            }
        }

        public bool HasUndoActions()
        {
            return _undoStack.Count > 0;
        }

        public bool IsEmpty()
        {
            return !HasUndoActions() && !HasRedoActions();
        }

        public bool HasRedoActions()
        {
            return _redoStack.Count > 0;
        }

        public void StartEntry()
        {
            if (_currentEntry != null)
            {
                Debug.LogWarning("Undo entry already opened. Will force end the previous one.");
                EndEntry();
            }

            _currentEntry = new UndoEntry();
        }

        public void AddUndoAction(System.Action undoAction)
        {
            if (_currentEntry == null)
                throw new System.Exception("No undo entry to add to. Start a new UndoEntry first with StartUndoEntry()!");

            _currentEntry.AddUndoAction(undoAction);
        }

        public void AddRedoAction(System.Action redoAction)
        {
            if (_currentEntry == null)
                throw new System.Exception("No undo entry to add to. Start a new UndoEntry first with StartUndoEntry()!");

            _currentEntry.AddRedoAction(redoAction);
        }

        public void CancelEntry()
        {
            _currentEntry = null;
        }

        public void EndEntry()
        {
            if (_currentEntry == null)
            {
                Debug.LogWarning("No undo entry to end. Aborting undo entry.");
                return;
            }

            // Cancel if entry has not steps.
            if (_currentEntry.UndoActions.Count == 0 && _currentEntry.RedoActions.Count == 0)
            {
                _currentEntry = null;
                return;
            }

            if (!_isGrouping || _isNewGroup)
            {
                if (_undoStack.Count > MaxEntries)
                {
                    _undoStack.RemoveFirst();
                }

                _undoStack.AddLast(new List<UndoEntry>() { _currentEntry });
                _isNewGroup = false;
            }
            else
            {
                if (_undoStack.Last != null && _undoStack.Last.Value != null)
                {
                    _undoStack.Last.Value.Add(_currentEntry);
                }
            }

            _currentEntry = null;
        }

        public void RemoveLastUndo()
        {
            if (_undoStack.Count > 0)
                _undoStack.RemoveLast();
        }

        public void Undo()
        {
            if (_undoStack.Count > 0) 
            {
                var last = _undoStack.Last.Value;
                _undoStack.RemoveLast();
                _redoStack.AddLast(last);
                for (int i = last.Count-1; i >= 0; i--)
                {
                    last[i].Undo();
                }
            }

            //Debug.Log("Undo: ");
            //debugLogStack(" UndoStack", _undoStack);
            //debugLogStack(" RedoStack", _redoStack);
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var last = _redoStack.Last.Value;
                _redoStack.RemoveLast();
                _undoStack.AddLast(last);
                for (int i = 0; i < last.Count; i++)
                {
                    last[i].Redo();
                }
            }

            //Debug.Log("Redo: ");
            //debugLogStack(" UndoStack", _undoStack);
            //debugLogStack(" RedoStack", _redoStack);
        }

        private void debugLogStack(string prefix, LinkedList<List<UndoEntry>> stack)
        {
            Debug.Log(prefix + " Count: " + stack.Count);
            foreach (var redo in stack)
            {
                Debug.Log("  * Entry (count: "+ (redo as List<UndoEntry>).Count + ")");
                foreach (var subState in (redo as List<UndoEntry>))
                {
                    Debug.Log("    > " + subState);
                }
            }
        }
    }
}
