// using System;
// using System.Threading;
// using System.Threading.Tasks;
//
// namespace AICodeAnalyzer;
//
// public class SemaphoreSlimSafe(int initialCount) : IDisposable
// {
//     private readonly SemaphoreSlim _semaphore = new(initialCount);
//     private bool _disposed;
//
//     public Task WaitAsync()
//     {
//         return _semaphore.WaitAsync();
//     }
//
//     public void Release()
//     {
//         _semaphore.Release();
//     }
//
//     public void Dispose()
//     {
//         Dispose(true);
//         GC.SuppressFinalize(this);
//     }
//
//     protected virtual void Dispose(bool disposing)
//     {
//         if (_disposed) return;
//
//         if (disposing)
//         {
//             _semaphore.Dispose();
//         }
//
//         _disposed = true;
//     }
// }