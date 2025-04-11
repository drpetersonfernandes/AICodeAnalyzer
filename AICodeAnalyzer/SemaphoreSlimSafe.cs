﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace AICodeAnalyzer;

public class SemaphoreSlimSafe : IDisposable
{
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;
    
    public SemaphoreSlimSafe(int initialCount)
    {
        _semaphore = new SemaphoreSlim(initialCount);
    }
    
    public Task WaitAsync()
    {
        return _semaphore.WaitAsync();
    }
    
    public void Release()
    {
        _semaphore.Release();
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _semaphore?.Dispose();
            }
        
            _disposed = true;
        }
    }
}