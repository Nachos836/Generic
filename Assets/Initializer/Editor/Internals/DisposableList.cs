using System;
using System.Collections.Generic;

namespace Initializer.Editor.Internals
{
    internal sealed class DisposableList : IDisposable
    {
        private readonly List<IDisposable> _disposables = new ();

        public void Add(IDisposable disposable)
        {
            _disposables.Add(disposable);
        }

        public void Dispose()
        {
            foreach (var disposable in _disposables)
            {
                disposable.Dispose();
            }
            _disposables.Clear();
        }
    }

    internal static class DisposableListExtensions
    {
        public static void AddTo(this IDisposable disposable, DisposableList disposableList)
        {
            disposableList.Add(disposable);
        }
    }
}
