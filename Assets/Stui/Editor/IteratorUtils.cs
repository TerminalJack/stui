// Modifications Copyright (c) 2026 TerminalJack
// Licensed under the MIT License. See the LICENSE.TXT file in the project root for details.
//
// Portions of this file are derived from the Spriter2UnityDX project.
// The original author provided an open-use permission statement, preserved in THIRD_PARTY_NOTICES.md.

using System;
using System.Collections;

namespace Spriter2UnityDX
{
    public static class IteratorUtils
    {
        public static IEnumerator SafeEnumerable(
            Func<IEnumerator> enumeratorFactory,
            Action<Exception> onError = null,
            Action onCompleted = null)
        {
            IEnumerator iterator;

            try
            {
                iterator = enumeratorFactory();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                yield break;
            }

            while (true)
            {
                bool hasNext;

                try
                {
                    hasNext = iterator.MoveNext();
                }
                catch (Exception ex)
                {
                    onError?.Invoke(ex);
                    yield break;
                }

                if (!hasNext)
                {
                    break;
                }

                yield return iterator.Current;
            }

            onCompleted?.Invoke();
        }
    }
}