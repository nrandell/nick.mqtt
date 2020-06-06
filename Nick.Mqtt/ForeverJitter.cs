using System;
using System.Collections;
using System.Collections.Generic;

using Polly.Contrib.WaitAndRetry;

namespace Nick.Mqtt
{
    public class ForeverJitter : IEnumerable<TimeSpan>
    {
        public TimeSpan MedianFirstRetryDelay { get; }
        public int RetryCount { get; }
        public TimeSpan MaxDelay { get; }

        public ForeverJitter(TimeSpan medianFirstRetryDelay, int retryCount, TimeSpan maxDelay)
        {
            MedianFirstRetryDelay = medianFirstRetryDelay;
            RetryCount = retryCount;
            MaxDelay = maxDelay;
        }

        public IEnumerator<TimeSpan> GetEnumerator()
        {
            var initial = Backoff.DecorrelatedJitterBackoffV2(MedianFirstRetryDelay, RetryCount, fastFirst: true);
            foreach (var value in initial)
            {
                if (value < MaxDelay)
                {
                    yield return value;
                }
            }
            while (true)
            {
                yield return MaxDelay;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
