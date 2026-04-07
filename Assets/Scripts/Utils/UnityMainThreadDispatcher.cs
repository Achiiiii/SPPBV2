using System;
using System.Collections.Generic;
using UnityEngine;

namespace SPPB.Utils
{
    /// <summary>
    /// Dispatches actions to the Unity main thread.
    /// Attach this to a GameObject in the scene, or it will auto-create via Instance.
    /// </summary>
    public class UnityMainThreadDispatcher : Singleton<UnityMainThreadDispatcher>
    {
        private static readonly Queue<Action> _executionQueue = new Queue<Action>();

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        /// <summary>
        /// Enqueue an action to be executed on the main thread.
        /// </summary>
        public void Enqueue(Action action)
        {
            if (action == null) return;

            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
    }
}
