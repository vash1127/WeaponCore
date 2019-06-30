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
        private readonly Dictionary<string, IMyModelDummy> _tmp1 = new Dictionary<string, IMyModelDummy>();
        private readonly Dictionary<string, IMyModelDummy> _tmp2 = new Dictionary<string, IMyModelDummy>();

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
                    _tmp2.Clear();
                    _cachedSubpart.Model?.GetDummies(_tmp2);
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
                //CachedMatrix = (_cachedDummyMatrix ?? MatrixD.Identity) * (_cachedSubpart?.WorldMatrix ?? _entity.WorldMatrix);
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart.Model)) Update();
                var dummyMatrix = _cachedDummyMatrix ?? MatrixD.Identity;
                var pos = Vector3D.Transform(dummyMatrix.Translation, _cachedSubpart.WorldMatrix); 
                var dir = Vector3D.TransformNormal(dummyMatrix.Forward, _cachedSubpart.WorldMatrix);
                CachedInfo = new DummyInfo(pos, dir);
                //Log.Line($"{CachedInfo.Position} - {_cachedSubpartModel == _cachedSubpart.Model} - {_cachedModel == _entity.Model} - {_cachedDummyMatrix.HasValue}");
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
