// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Dumpify;

namespace Spriter2UnityDX
{
    using Importing;
    using EntityInfo;
    using Extensions;

    [CreateAssetMenu(fileName = "NewVarDefsEntityTask", menuName = "Inspection Tasks/Entity Variable Definitions Task", order = 4)]
    public class VarDefsEntityTask : ScmlInspectorEntityTask
    {
        public override IEnumerator ProcessEntity(ScmlObject scmlObject, SpriterEntityInfo entityInfo,
            Entity entity, IBuildTaskContext inspectionCtx)
        {
            if (entity.variableDefs.Count > 0)
            {
                yield return $"Entity '{entity.name}' has the following variable definitions:";

                foreach (var variableDef in entity.variableDefs)
                {
                    if (inspectionCtx.IsCanceled) { yield break; }

                    yield return $"    variable name: {variableDef.name}, type: {variableDef.type}, " +
                        "default value: {variableDef.defaultValue}";

                    if (variableDef.type == VarType.String)
                    {   // Figure out what all of the possible string values this string variable can have.
                        var possibleStringValues =
                            entity.animations?
                                .SelectMany(a => a.metadata?.varlines ?? Enumerable.Empty<Varline>())
                                .Where(v => v.varDefId == variableDef.id)
                                .SelectMany(v => v.keys ?? Enumerable.Empty<VarlineKey>())
                                .Select(k => k.value)
                                .Where(v => v != null)
                                .Distinct()
                                .OrderBy(v => v)
                                .ToList()
                            ?? new List<string>();

                        yield return "    possibleStringValues before modification:";

                        possibleStringValues.DumpToUnityConsole(
                            linePrefix: "        ",
                            maxDepth: 2,
                            members: new MembersConfig { IncludeFields = true },
                            tableConfig: new TableConfig { BorderStyle = TableBorderStyle.Ascii });

                        // Make sure the default value is the first element of the list...

                        possibleStringValues.RemoveAll(s => s == variableDef.defaultValue);
                        possibleStringValues.Insert(0, variableDef.defaultValue);

                        variableDef.possibleStringValues = possibleStringValues;

                        yield return "    possibleStringValues after modification:";

                        possibleStringValues.DumpToUnityConsole(
                            linePrefix: "        ",
                            maxDepth: 2,
                            members: new MembersConfig { IncludeFields = true },
                            tableConfig: new TableConfig { BorderStyle = TableBorderStyle.Ascii });

                        yield return $"    '{variableDef.name}' has the possible string values:";

                        foreach (var s in variableDef.possibleStringValues)
                        {
                            yield return $"        '{s}'";
                        }
                    }
                }
            }
            else
            {
                yield return $"Entity '{entity.name}' has no variable definitions.";
            }
        }
    }
}