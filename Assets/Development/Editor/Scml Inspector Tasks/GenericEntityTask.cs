using System.Collections;
using UnityEngine;
using Dumpify;

namespace Spriter2UnityDX
{
    using Importing;
    using EntityInfo;
    using Extensions;

    [CreateAssetMenu(fileName = "NewGenericEntityTask", menuName = "Inspection Tasks/Generic Entity Task", order = 2)]
    public class GenericEntityTask : ScmlInspectorEntityTask
    {
        public override IEnumerator ProcessEntity(ScmlObject scmlObject, SpriterEntityInfo entityInfo,
            Entity entity, IBuildTaskContext inspectionCtx)
        {
            // Warning!  A maxDepth setting of 3 or greater can take a while.  A minute or more in some cases.
            var task = entity.DumpToUnityConsoleRoutine(inspectionCtx, maxDepth: 2,
                members: new MembersConfig { IncludeFields = true },
                tableConfig: new TableConfig { BorderStyle = TableBorderStyle.Ascii });

            while (task.MoveNext())
            {
                yield return task.Current;
            }
        }
    }
}