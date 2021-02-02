using Sandbox.Game.Entities;

namespace WeaponCore.Support
{
    public partial class AiComponent
    {
        internal readonly Ai Ai;
        public AiComponent(Ai ai)
        {
            Ai = ai;
        }        
    }
}
