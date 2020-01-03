using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    // Courtesy of Equinox
    public class Dummy
    {
        internal MyEntity Entity;

        private IMyModel _cachedModel;
        private IMyModel _cachedSubpartModel;
        private MyEntity _cachedSubpart;
        private MatrixD? _cachedDummyMatrix;
        private readonly string[] _path;
        private readonly Dictionary<string, IMyModelDummy> _tmp1 = new Dictionary<string, IMyModelDummy>();
        private readonly Dictionary<string, IMyModelDummy> _tmp2 = new Dictionary<string, IMyModelDummy>();

        public Dummy(MyEntity e, params string[] path)
        {
            Entity = e;
            _path = path;
        }

        private bool _failed = true;
        internal void Update()
        {
            _cachedModel = Entity.Model;
            _cachedSubpart = Entity;
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
                if (!(_cachedModel == Entity?.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                if (Entity == null || _cachedSubpart == null)
                {
                    Log.Line($"nullEntity:{Entity == null} - nullSubPart:{_cachedSubpart == null}");
                    return new DummyInfo();
                }
                var dummyMatrix = _cachedDummyMatrix ?? MatrixD.Identity;
                var subPartPos = Vector3D.Transform(dummyMatrix.Translation, _cachedSubpart.WorldMatrix);
                var subPartDir = Vector3D.TransformNormal(dummyMatrix.Forward, _cachedSubpart.WorldMatrix);
                return new DummyInfo { Position = subPartPos, Direction = subPartDir };
            }
        }

        public bool Valid
        {
            get
            {
                if (!(_cachedModel == Entity.Model && _cachedSubpartModel == _cachedSubpart?.Model)) Update();
                return _cachedSubpart != null && _cachedDummyMatrix.HasValue && !_failed;
            }
        }

        public struct DummyInfo
        {
            public Vector3D Position;
            public Vector3D Direction;
        }
    }
}
