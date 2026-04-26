// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System.Collections.Generic;
using UnityEngine;

namespace Stui
{
    [DisallowMultipleComponent]
    public class SpriterString : MonoBehaviour
    {
        public string VariableName;
        public string DefaultValue { get { return GetDefaultValue();  } }
        public List<string> PossibleValues = new List<string>();
        public int ValueIndex = -1;

        public string Value { get { return GetCurrentValue(); } }

        private string GetDefaultValue()
        {
            return PossibleValues.Count > 0
                ? PossibleValues[0]
                : "";
        }

        private string GetCurrentValue()
        {
            return ValueIndex >= 0 && ValueIndex < PossibleValues.Count
                ? PossibleValues[ValueIndex]
                : DefaultValue;
        }
    }
}
