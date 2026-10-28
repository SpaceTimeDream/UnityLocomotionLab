using UnityEngine;

namespace Portfolio.Character
{
    [DisallowMultipleComponent]
    public class BasicCharacterLookTarget2D : CharacterLookTarget2D
    {
        //------------------------------- Private Methods -------------------------------//

        protected override bool CanBeLookedAtBy(CharacterAnimationController2D character)
        {
            return true;
        }
    }
}
