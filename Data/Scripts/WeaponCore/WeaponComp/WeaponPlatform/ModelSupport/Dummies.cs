using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using WeaponCore.Platform;

namespace WeaponCore.Support
{
    // based on code of Equinox's
    public class Dummy
    {

        internal MyEntity Entity
        {
            get
            {
                if (_entity?.Model == null) {
                    if (_weapon.System.Session.LocalVersion) Log.Line($"reset parts");
                    _weapon.Comp.Platform.ResetParts(_weapon.Comp);
                    if (_entity?.Model == null)
                        Log.Line($"Dummy Entity/Model null");
                }

                return _entity;
            }
            set
            {
                if (value?.Model == null)
                    Log.Line($"DummyModel null for weapon on set: {_weapon.System.WeaponName}");
                _entity = value; 

            }
        }
        //internal MyEntity Entity ;

        private IMyModel _cachedModel;
        private IMyModel _cachedSubpartModel;
        private MyEntity _cachedSubpart;
        private MatrixD? _cachedDummyMatrix;
        internal Vector3D CachedPos;
        internal Vector3D CachedDir;
        private readonly string[] _path;
        private readonly Dictionary<string, IMyModelDummy> _tmp1 = new Dictionary<string, IMyModelDummy>();
        private readonly Dictionary<string, IMyModelDummy> _tmp2 = new Dictionary<string, IMyModelDummy>();
        private readonly Weapon _weapon;
        private MyEntity _entity;
        public Dummy(MyEntity e, Weapon w, params string[] path)
        {
            _weapon = w;
            Entity = e;
            _path = path;
        }

        private bool _failed = true;
        internal void Update()
        {
            _cachedModel = _entity.Model;
            _cachedSubpart = _entity;
            _cachedSubpartModel = _cachedSubpart?.Model;
            for (var i = 0; i < _path.Length - 1; i++)
            {
                MyEntitySubpart part;
                if (_cachedSubpart.TryGetSubpart(_path[i], out part))
                    _cachedSubpart = part;
                else
                {
                    _tmp2.Clear();
                    ((IMyModel)_cachedSubpart.Model)?.GetDummies(_tmp2);
                    _failed = true;
                    return;
                }
            }

            _cachedSubpartModel = _cachedSubpart?.Model;
            _cachedDummyMatrix = null;
            _tmp1.Clear();
            _cachedSubpartModel?.GetDummies(_tmp1);

            IMyModelDummy dummy;
            if (_tmp1.TryGetValue(_path[_path.Length - 1], out dummy))
            {
                _cachedDummyMatrix = MatrixD.Normalize(dummy.Matrix);
                _failed = false;
                return;
            }
            _failed = true;
        }

        public DummyInfo Info
        {
            get
            {
                if (_entity != null && _entity.Model == null && Entity.Model == null)
                    Log.Line($"DummyInfo reset and still has invalid enity/model");

                if (!(_cachedModel == _entity?.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                if (_entity == null || _cachedSubpart == null)
                {
                    Log.Line($"DummyInfo invalid");
                    return new DummyInfo();
                }

                var dummyMatrix = _cachedDummyMatrix ?? MatrixD.Identity;
                CachedPos = Vector3D.Transform(dummyMatrix.Translation, _cachedSubpart.WorldMatrix);
                CachedDir = Vector3D.TransformNormal(dummyMatrix.Forward, _cachedSubpart.WorldMatrix);
                return new DummyInfo { Position = CachedPos, Direction = CachedDir, DummyMatrix = _cachedSubpart.WorldMatrix };
            }
        }

        public struct DummyInfo
        {
            public Vector3D Position;
            public Vector3D Direction;
            public MatrixD DummyMatrix;
        }
    }
}
