#nullable enable

using UnityEngine;

namespace Toast
{
    /// <summary>
    /// Base class for toast views.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public abstract class ToastViewBase : MonoBehaviour
    {
        private RectTransform? _rectTransform;

        /// <summary>
        /// Gets the RectTransform component attached to this GameObject. Caches the reference for future use.
        /// </summary>
        public RectTransform RectTransform => _rectTransform == null ? _rectTransform = (RectTransform)transform : _rectTransform;
    }
}