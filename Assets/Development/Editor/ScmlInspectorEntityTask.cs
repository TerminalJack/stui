using System.Collections;
using UnityEngine;

namespace Spriter2UnityDX
{
    using Importing;
    using EntityInfo;

    public abstract class ScmlInspectorEntityTask : ScriptableObject
    {
        public abstract IEnumerator ProcessEntity(ScmlObject scmlObject, SpriterEntityInfo entityInfo,
            Entity entity, IBuildTaskContext inspectionCtx);
    }
}
