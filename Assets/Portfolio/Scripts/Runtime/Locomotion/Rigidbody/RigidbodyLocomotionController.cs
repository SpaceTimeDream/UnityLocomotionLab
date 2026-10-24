using System;
using UnityEngine;

namespace Portfolio.Locomotion.Rigidbody
{
    /// <summary>
    /// Serialization compatibility component for existing scenes and prefabs.
    /// New code should reference PlayerController.
    /// </summary>
    [Obsolete("Use PlayerController. This component remains only to preserve existing scene and prefab script references.")]
    public sealed class RigidbodyLocomotionController : PlayerController
    {
    }
}