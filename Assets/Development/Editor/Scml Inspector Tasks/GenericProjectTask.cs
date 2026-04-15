using System.Collections;
using UnityEngine;
using Dumpify;

namespace Spriter2UnityDX
{
    using Importing;
    using Extensions;

    [CreateAssetMenu(fileName = "NewGenericProjectTask", menuName = "Inspection Tasks/Generic Project Task", order = 1)]
    public class GenericProjectTask : ScmlInspectorProjectTask
    {
        public override IEnumerator ProcessProject(ScmlObject scmlObject, IBuildTaskContext inspectionCtx)
        {
            // Warning!  A maxDepth setting of 3 or greater can take a while.  A minute or more in some cases.
            var task = scmlObject.DumpToUnityConsoleRoutine(inspectionCtx, maxDepth: 2,
                members: new MembersConfig { IncludeFields = true },
                tableConfig: new TableConfig { BorderStyle = TableBorderStyle.Ascii });

            while (task.MoveNext())
            {
                yield return task.Current;
            }
        }
    }
}