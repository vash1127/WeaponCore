using System.Collections.Generic;
using VRageMath;
using WeaponCore.Projectiles;

namespace WeaponCore.Support
{
    internal class DynTrees
    {
        internal static void RegisterProjectile(Projectile projectile)
        {
            if (projectile.PruningProxyId != -1)
                return;
            BoundingSphereD sphere = new BoundingSphereD(projectile.Position, projectile.T.System.AreaEffectSize);
            BoundingBoxD result;
            BoundingBoxD.CreateFromSphere(ref sphere, out result);
            projectile.PruningProxyId = Session.Instance.ProjectileTree.AddProxy(ref result, projectile, 0U, true);
        }

        internal static void UnregisterProjectile(Projectile projectile)
        {
            if (projectile.PruningProxyId == -1)
                return;
            Session.Instance.ProjectileTree.RemoveProxy(projectile.PruningProxyId);
            projectile.PruningProxyId = -1;
        }

        internal static void OnProjectileMoved(Projectile projectile, ref Vector3D velocity)
        {
            if (projectile.PruningProxyId == -1)
                return;
            BoundingSphereD sphere = new BoundingSphereD(projectile.Position, projectile.T.System.AreaEffectSize);
            BoundingBoxD result;
            BoundingBoxD.CreateFromSphere(ref sphere, out result);
            Session.Instance.ProjectileTree.MoveProxy(projectile.PruningProxyId, ref result, velocity);
        }

        internal static void GetAllProjectilesInSphere(ref BoundingSphereD sphere, List<Projectile> result, bool clearList = true)
        {
            Session.Instance.ProjectileTree.OverlapAllBoundingSphere(ref sphere, result, clearList);
        }
    }
}
