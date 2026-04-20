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
    using EntityInfo;
    using Extensions;

    [CreateAssetMenu(fileName = "NewGenericAnimationTask", menuName = "Inspection Tasks/Generic Animation Task", order = 3)]
    public class GenericAnimationTask : ScmlInspectorAnimationTask
    {
        public override IEnumerator ProcessAnimation(ScmlObject scmlObject, SpriterEntityInfo entityInfo, Entity entity,
            Animation animation, IBuildTaskContext inspectionCtx)
        {
            var task = animation.DumpToUnityConsoleRoutine(inspectionCtx, maxDepth: 2,
                members: new MembersConfig { IncludeFields = true },
                tableConfig: new TableConfig { BorderStyle = TableBorderStyle.Ascii });

            while (task.MoveNext())
            {
                yield return task.Current;
            }
        }
    }
}