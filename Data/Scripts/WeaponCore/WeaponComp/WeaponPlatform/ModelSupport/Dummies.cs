using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    // Courtesy of Equinox
    public class Dummy
    {
        private readonly MyEntity _entity;

        private IMyModel _cachedModel;
        private IMyModel _cachedSubpartModel;
        private MyEntity _cachedSubpart;
        private MatrixD? _cachedDummyMatrix;
        private readonly string[] _path;
        private readonly Dictionary<string, IMyModelDummy> _tmp1 = new Dictionary<string, IMyModelDummy>();
        private readonly Dictionary<string, IMyModelDummy> _tmp2 = new Dictionary<string, IMyModelDummy>();
        private readonly bool _isSubPart;

        public Dummy(MyEntity e, params string[] path)
        {
            _entity = e;
            _path = path;
            _isSubPart = (e is MyEntitySubpart);
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
                if (!(_cachedModel == _entity.Model && _cachedSubpartModel == _cachedSubpart.Model)) Update();
                var dummyMatrix = _cachedDummyMatrix ?? MatrixD.Identity;
                var subPartPos = Vector3D.Transform(dummyMatrix.Translation, _cachedSubpart.WorldMatrix);
                var subPartDir = Vector3D.TransformNormal(dummyMatrix.Forward, _cachedSubpart.WorldMatrix);
                Log.Line($"entity is Subpart:{_isSubPart} - dummyOnSubpart:{_cachedSubpart is MyEntitySubpart}");
                return new DummyInfo { Position = subPartPos, Direction = subPartDir };
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

        public struct DummyInfo
        {
            public Vector3D Position;
            public Vector3D Direction;
        }
    }
}
