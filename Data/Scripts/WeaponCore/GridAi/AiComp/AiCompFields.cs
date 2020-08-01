using Sandbox.Game.Entities;

namespace WeaponCore.Support
{
    public partial class AiComponent
    {
        internal readonly GridAi Ai;
        public AiComponent(GridAi ai)
        {
            Ai = ai;
        }        
    }
}
