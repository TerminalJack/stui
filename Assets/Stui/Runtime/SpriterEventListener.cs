// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Spriter2UnityDX
{
    [DisallowMultipleComponent]
    public class SpriterEventListener : MonoBehaviour
    {
        [Serializable]
        private class SpriterEventBinding
        {
            public UnityEvent callback;
        }

        // Hook events up in the Inspector via this property.
        [SerializeField] private List<SpriterEventBinding> _bindings = new List<SpriterEventBinding>();

        [HideInInspector] public string _eventName; // The importer will set this.

        private EventController _controller;
        private bool showMissingControllerWarningMsg = true;

        // Public API for user scripts to register/unregister handlers programmatically.
        public void Register(Action callback)
        {
            AcquireController();

            if (_controller != null)
            {
                _controller.AddHandler(_eventName, callback);
            }
        }

        public void Unregister(Action callback)
        {
            AcquireController();

            if (_controller != null)
            {
                _controller.RemoveHandler(_eventName, callback);
            }
        }

        private void OnEnable()
        {
            AcquireController();

            if (_controller != null)
            {
                // Register all bindings.
                foreach (var binding in _bindings)
                {
                    if (binding != null && binding.callback != null)
                    {
                        _controller.AddHandler(_eventName, binding.callback.Invoke);
                    }
                }
            }
        }

        private void OnDisable()
        {
            AcquireController();

            if (_controller != null)
            {
                // Unregister all bindings
                foreach (var binding in _bindings)
                {
                    if (binding != null && binding.callback != null)
                    {
                        _controller.RemoveHandler(_eventName, binding.callback.Invoke);
                    }
                }
            }
        }

        private void AcquireController()
        {
            if (_controller == null)
            {
                // Find the EventController on the root of the prefab.
                _controller = GetComponentInParent<EventController>();

                if (_controller == null && showMissingControllerWarningMsg)
                {
                    Debug.LogError($"SpriterEventListener on {gameObject.name} could not find an EventController in its parents.");
                    showMissingControllerWarningMsg = false;
                }
            }
        }
    }
}
