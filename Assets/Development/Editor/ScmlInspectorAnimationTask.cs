using System.Collections;
using UnityEngine;

namespace Spriter2UnityDX
{
    using Importing;
    using EntityInfo;

    public abstract class ScmlInspectorAnimationTask : ScriptableObject
    {
        public abstract IEnumerator ProcessAnimation(ScmlObject scmlObject, SpriterEntityInfo entityInfo, Entity entity,
            Animation animation, IBuildTaskContext inspectionCtx);
    }
}