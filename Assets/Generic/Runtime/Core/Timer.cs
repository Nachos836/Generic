using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Cysharp.Threading.Tasks.Linq;
using UnityEngine;

namespace Generic.Core
{
    public readonly struct Timer
    {
        private readonly TimeSpan _duration;
        private readonly TimeSpan _tick;

        public static readonly Func<TimeSpan> DeltaTimeTicker = static () => TimeSpan.FromSeconds(Time.deltaTime);
        public static readonly Func<TimeSpan> SmoothDeltaTimeTicker = static () => TimeSpan.FromSeconds(Time.smoothDeltaTime);
        public static readonly Func<TimeSpan> DeltaFixedTimeTicker = static () => TimeSpan.FromSeconds(Time.fixedDeltaTime);

        public Timer(TimeSpan duration)
        {
            _duration = duration;
            _tick = TimeSpan.Zero;
        }

        public Timer(TimeSpan duration, TimeSpan tick)
        {
            _duration = duration;
            _tick = tick;
        }

        public IUniTaskAsyncEnumerable<double> StartNormalizedAsync(CancellationToken cancellation = default)
        {
            var start = TimeSpan.Zero;
            var end = _duration;
            var tick = _tick;

            return UniTaskAsyncEnumerable.Create<double>(async (writer, token) =>
            {
                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(0.0);

                    start += tick;
                }

                while (token.IsCancellationRequested is false && start < end)
                {
                    await writer.YieldAsync(start / end);

                    start += tick;
                }

                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(1.0);
                }

            }).TakeUntilCanceled(cancellation);
        }

        public IUniTaskAsyncEnumerable<double> StartNormalizedAsync(Func<TimeSpan> ticker, CancellationToken cancellation = default)
        {
            var start = TimeSpan.Zero;
            var end = _duration;

            return UniTaskAsyncEnumerable.Create<double>(async (writer, token) =>
            {
                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(0.0);

                    start += ticker();
                }

                while (token.IsCancellationRequested is false && start < end)
                {
                    await writer.YieldAsync(start / end);

                    start += ticker();
                }

                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(1.0);
                }

            }).TakeUntilCanceled(cancellation);
        }

        public IUniTaskAsyncEnumerable<TimeSpan> StartAsync(CancellationToken cancellation = default)
        {
            var start = TimeSpan.Zero;
            var end = _duration;
            var tick = _tick;

            return UniTaskAsyncEnumerable.Create<TimeSpan>(async (writer, token) =>
            {
                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(start);

                    start += tick;
                }

                while (token.IsCancellationRequested is false && start < end)
                {
                    await writer.YieldAsync(start);

                    start += tick;
                }

                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(end);
                }

            }).TakeUntilCanceled(cancellation);
        }

        public IUniTaskAsyncEnumerable<TimeSpan> StartAsync(Func<TimeSpan> ticker, CancellationToken cancellation = default)
        {
            var start = TimeSpan.Zero;
            var end = _duration;

            return UniTaskAsyncEnumerable.Create<TimeSpan>(async (writer, token) =>
            {
                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(start);

                    start += ticker();
                }

                while (token.IsCancellationRequested is false && start < end)
                {
                    await writer.YieldAsync(start);

                    start += ticker();
                }

                if (token.IsCancellationRequested is false)
                {
                    await writer.YieldAsync(end);
                }

            }).TakeUntilCanceled(cancellation);
        }
    }
}
