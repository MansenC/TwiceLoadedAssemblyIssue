namespace Example
{
    internal struct SemaphoreDisposer : IDisposable
    {
        private SemaphoreSlim _semaphore;

        public SemaphoreDisposer(SemaphoreSlim semaphore)
        {
            _semaphore = semaphore;
        }

        public void Dispose()
        {
            // This allows the semaphore to be disposed only a single time. Dispose() is allowed to be called
            // multiple times but since this is internal we're fine here. Additionally, if it were to be called
            // multilpe times then something is very wrong

            var semaphoreToDispose = Interlocked.Exchange(ref _semaphore, null);
            if (semaphoreToDispose == null)
            {
                throw new ObjectDisposedException($"{nameof(SemaphoreDisposer)} is being disposed twice");
            }

            semaphoreToDispose.Release();
        }
    }
}
