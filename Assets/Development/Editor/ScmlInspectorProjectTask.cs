// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

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
