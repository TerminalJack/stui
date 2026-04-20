// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

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