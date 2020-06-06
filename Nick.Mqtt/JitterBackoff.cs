using System;
using System.Collections.Generic;

namespace Nick.Mqtt
{
    public class JitterBackoff
    {
        private readonly ForeverJitter _source;
        private IEnumerator<TimeSpan>? _enumerator;

        public JitterBackoff(ForeverJitter foreverJitter)
        {
            _source = foreverJitter;
        }

        private void Refresh()
        {
            _enumerator = _source.GetEnumerator();
        }

        public void Clear()
        {
            _enumerator = null;
        }

        public TimeSpan Next()
        {
            if (_enumerator == null)
            {
                Refresh();
            }

            var enumerator = _enumerator!;
            enumerator.MoveNext();
            return enumerator.Current;
        }
    }
}
