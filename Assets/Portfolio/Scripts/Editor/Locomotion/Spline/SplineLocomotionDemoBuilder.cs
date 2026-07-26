#if UNITY_EDITOR
using Portfolio.Locomotion.Demo.Editor;
using UnityEditor;

namespace Portfolio.Locomotion.Spline.Editor
{
    public static class SplineLocomotionDemoBuilder
    {
        // Fields
        private const string MenuPath = "Tools/Portfolio/Create Spline Locomotion Demo";

        // Public Methods
        [MenuItem(MenuPath)]
        public static void CreateDemo()
        {
            LocomotionDemoBuilder.CreateSimulatedDemo();
        }
    }
}
#endif
