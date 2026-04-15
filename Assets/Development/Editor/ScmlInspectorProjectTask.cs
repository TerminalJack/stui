using System.Collections;
using UnityEngine;

namespace Spriter2UnityDX
{
    using Importing;

    public abstract class ScmlInspectorProjectTask : ScriptableObject
    {
        public abstract IEnumerator ProcessProject(ScmlObject scmlObject, IBuildTaskContext inspectionCtx);
    }
}
