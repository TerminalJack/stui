using UnityEngine;

namespace Spriter2UnityDX
{
    [DisallowMultipleComponent]
    public class SpriterTag : MonoBehaviour
    {
        [Tooltip("The name of this tag.")]
        public string tagName;

        [Tooltip("Is this tag currently active?  This is the animated property.  Use the isActive property to get the " +
            "tag's state.")]
        [SerializeField] private float isActiveFloat = 0f;

        public bool isActive => isActiveFloat > 0.5f;
    }
}
