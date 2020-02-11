using System;
using System.Collections;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace WeaponCore.Support
{
    // Courtesy Equinox
    /// <summary>
    /// Maintains a list of all recursive subparts of the given entity.  Respects changes to the model.
    /// </summary>
    internal class RecursiveSubparts : IEnumerable<MyEntity>
    {
        private readonly List<MyEntity> _subparts = new List<MyEntity>();
        private readonly Dictionary<string, IMyModelDummy> _tmp = new Dictionary<string, IMyModelDummy>();
        internal readonly Dictionary<string, MyEntity> NameToEntity = new Dictionary<string, MyEntity>();

        private IMyModel _trackedModel;
        internal MyEntity Entity;

        internal void Clean(MyEntity myEntity)
        {
            //GetEnumerator().Dispose(); // Don't this this is needed?
            _subparts.Clear();
            _tmp.Clear();
            NameToEntity.Clear();
            _trackedModel = null;
            Entity = myEntity;
        }

        internal void CheckSubparts()
        {
            if (_trackedModel == Entity?.Model)
                return;
            _trackedModel = Entity?.Model;
            _subparts.Clear();
            NameToEntity.Clear();
            if (Entity != null)
            {
                var head = -1;
                _tmp.Clear();
                while (head < _subparts.Count)
                {
                    var query = head == -1 ? Entity : _subparts[head];
                    head++;
                    if (query.Model == null)
                        continue;
                    _tmp.Clear();
                    ((IMyEntity)query).Model.GetDummies(_tmp);
                    //Log.Line($"next part");
                    foreach (var kv in _tmp)
                    {
                        if (kv.Key.StartsWith("subpart_", StringComparison.Ordinal))
                        {
                            var name = kv.Key.Substring("subpart_".Length);
                            MyEntitySubpart res;
                            if (query.TryGetSubpart(name, out res))
                            {
                                _subparts.Add(res);
                                NameToEntity[name] = res;
                                //Log.Line($"name: {name}");
                            }
                        }
                    }
                    NameToEntity["None"] = Entity; 
                }
            }
        }

        IEnumerator<MyEntity> IEnumerable<MyEntity>.GetEnumerator()
        {
            return GetEnumerator();
        }

        internal List<MyEntity>.Enumerator GetEnumerator()
        {
            CheckSubparts();
            return _subparts.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Sets the emissive value of a specific emissive material on entity, and all recursive subparts.
        /// </summary>
        /// <param name="emissiveName">The name of the emissive material (ie. "Emissive0")</param>
        /// <param name="emissivity">Level of emissivity (0 is off, 1 is full brightness)</param>
        /// <param name="emissivePartColor">Color to emit</param>
        internal void SetEmissiveParts(string emissiveName, Color emissivePartColor, float emissivity)
        {
            Entity.SetEmissiveParts(emissiveName, emissivePartColor, emissivity);
            SetEmissivePartsForSubparts(emissiveName, emissivePartColor, emissivity);
        }

        /// <summary>
        /// Sets the emissive value of a specific emissive material on all recursive subparts.
        /// </summary>
        /// <param name="emissiveName">The name of the emissive material (ie. "Emissive0")</param>
        /// <param name="emissivity">Level of emissivity (0 is off, 1 is full brightness).</param>
        /// <param name="emissivePartColor">Color to emit</param>
        internal void SetEmissivePartsForSubparts(string emissiveName, Color emissivePartColor, float emissivity)
        {
            foreach (var k in this)
                k.SetEmissiveParts(emissiveName, emissivePartColor, emissivity);
        }
    }
}
