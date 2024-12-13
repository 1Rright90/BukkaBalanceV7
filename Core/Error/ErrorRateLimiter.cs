using System;
using System.Collections.Concurrent;

namespace YSBCaptain.Core.Error
{
    internal class ErrorRateLimiter
    {
        private readonly ConcurrentDictionary<string, ErrorBucket> _errorBuckets;
        private readonly TimeSpan _bucketDuration;
        private readonly int _maxErrorsPerBucket;

        public ErrorRateLimiter(TimeSpan bucketDuration, int maxErrorsPerBucket)
        {
            _errorBuckets = new ConcurrentDictionary<string, ErrorBucket>();
            _bucketDuration = bucketDuration;
            _maxErrorsPerBucket = maxErrorsPerBucket;
        }

        public bool ShouldProcessError(string errorKey)
        {
            CleanupExpiredBuckets();

            var bucket = _errorBuckets.GetOrAdd(errorKey, _ => new ErrorBucket(_bucketDuration));
            return bucket.TryIncrementError(_maxErrorsPerBucket);
        }

        private void CleanupExpiredBuckets()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _errorBuckets)
            {
                if (kvp.Value.IsExpired(now))
                {
                    _errorBuckets.TryRemove(kvp.Key, out _);
                }
            }
        }

        private class ErrorBucket
        {
            private readonly DateTime _startTime;
            private readonly TimeSpan _duration;
            private int _errorCount;

            public ErrorBucket(TimeSpan duration)
            {
                _startTime = DateTime.UtcNow;
                _duration = duration;
                _errorCount = 0;
            }

            public bool TryIncrementError(int maxErrors)
            {
                if (IsExpired(DateTime.UtcNow))
                {
                    _errorCount = 1;
                    return true;
                }

                if (_errorCount >= maxErrors)
                {
                    return false;
                }

                _errorCount++;
                return true;
            }

            public bool IsExpired(DateTime now)
            {
                return now - _startTime > _duration;
            }
        }
    }
}
