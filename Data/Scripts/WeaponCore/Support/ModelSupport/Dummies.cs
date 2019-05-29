using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    // Courtesy Equinox
    public class Dummy
    {
        private readonly IMyEntity _entity;

        private IMyModel _cachedModel;
        private IMyModel _cachedSubpartModel;
        private IMyEntity _cachedSubpart;
        private MatrixD? _cachedDummyMatrix;
        internal MatrixD CachedMatrix;
        internal DummyInfo CachedInfo;
        private readonly string[] _path;

        public Dummy(IMyEntity e, params string[] path)
        {
            _entity = e;
            _path = path;
        }

        private bool _failed = true;
        private void Update()
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
                    var tmp2 = new Dictionary<string, IMyModelDummy>();
                    _cachedSubpart.Model?.GetDummies(tmp2);
                    _failed = true;
                    return;
                }
            }

            _cachedSubpartModel = _cachedSubpart?.Model;
            _cachedDummyMatrix = null;
            var tmp = new Dictionary<string, IMyModelDummy>();
            _cachedSubpartModel?.GetDummies(tmp);
            IMyModelDummy dummy;
            if (tmp.TryGetValue(_path[_path.Length - 1], out dummy))
            {
                _cachedDummyMatrix = dummy.Matrix;
                _failed = false;
                return;
            }
            _failed = true;
        }


        public Vector3D WorldPosition
        {
            get
            {
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update(); 
                return Vector3D.Transform(_cachedDummyMatrix?.Translation ?? Vector3.Zero,
                    _cachedSubpart?.WorldMatrix ?? _entity.WorldMatrix);
            }
        }

        public MatrixD WorldMatrix
        {
            get
            {
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                CachedMatrix = (_cachedDummyMatrix ?? MatrixD.Identity) * (_cachedSubpart?.WorldMatrix ?? _entity.WorldMatrix);
                return CachedMatrix;
            }
        }

        public DummyInfo Info
        {
            get
            {
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                CachedMatrix = (_cachedDummyMatrix ?? MatrixD.Identity) * (_cachedSubpart?.WorldMatrix ?? _entity.WorldMatrix);
                CachedInfo = new DummyInfo(CachedMatrix.Translation, -Vector3D.Normalize(CachedMatrix.Backward - CachedMatrix.Forward));
                return CachedInfo;
            }
        }


        public bool Valid
        {
            get
            {
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                return _cachedSubpart != null && _cachedDummyMatrix.HasValue && !_failed;
            }
        }

        public override string ToString()
        {
            return $"{_entity.ToStringSmart()}: {string.Join("/", _path)}";
        }
    }

    public struct DummyInfo
    {
        public readonly Vector3D Position;
        public readonly Vector3D Direction;

        public DummyInfo(Vector3D position, Vector3D direction)
        {
            Position = position;
            Direction = direction;
        }
    }
}
