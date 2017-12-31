namespace SyncPro.Runtime
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public class ThrottlingManager : IDisposable
    {
        private readonly int tokensPerSecond;
        private readonly int maxTokens;
        private readonly object bucketLock;
        private readonly CancellationTokenSource cancellationTokenSource;

        private int availableTokens;

        private TimeSpan blockDelay = TimeSpan.Zero;

        /// <summary>
        /// Initialize a new throttling manager
        /// </summary>
        /// <param name="tokensPerSecond">The number of tokens per second to add to the queue</param>
        /// <param name="maxTokens">The maximum number of tokens to add to the queue</param>
        public ThrottlingManager(int tokensPerSecond, int maxTokens)
        {
            this.tokensPerSecond = tokensPerSecond;
            this.maxTokens = maxTokens;

            this.availableTokens = 0;
            this.bucketLock = new object();
            this.cancellationTokenSource = new CancellationTokenSource();


            Task.Run(
                () => { this.AddTokensMainThread(); }, 
                this.cancellationTokenSource.Token);
        }

        public int GetTokens(int count)
        {
            lock (this.bucketLock)
            {
                if (this.availableTokens <= 0)
                {
                    return 0;
                }

                if (count > this.availableTokens)
                {
                    int tokens = this.availableTokens;
                    this.availableTokens = 0;
                    return tokens;
                }

                this.availableTokens -= count;
                return count;
            }
        }

        public void BlockFor(TimeSpan timeSpan)
        {
            // Set the available buckets to 0
            lock (this.bucketLock)
            {
                this.availableTokens = 0;
                this.blockDelay = timeSpan;
            }
        }

        public void Dispose()
        {
            this.cancellationTokenSource.Cancel();
        }

        private void AddTokensMainThread()
        {
            // Calculate the number of tokens to add per tick (25ms)
            int tokensPerTick = Convert.ToInt32(Math.Floor(0.0025 * this.tokensPerSecond));
            TimeSpan delayOnNextLoop = TimeSpan.Zero;

            while (!this.cancellationTokenSource.Token.IsCancellationRequested)
            {
                if (delayOnNextLoop != TimeSpan.Zero)
                {
                    while (delayOnNextLoop.TotalMilliseconds > 0 && !this.cancellationTokenSource.IsCancellationRequested)
                    {
                        Thread.Sleep(1);
                        delayOnNextLoop = delayOnNextLoop.Subtract(TimeSpan.FromMilliseconds(1));
                    }

                    delayOnNextLoop = TimeSpan.Zero;
                }

                lock (this.bucketLock)
                {
                    // If a block delay was set (via a separate method), loop and await the delay.
                    if (this.blockDelay != TimeSpan.Zero)
                    {
                        delayOnNextLoop = this.blockDelay;
                        this.blockDelay = TimeSpan.Zero;

                        continue;
                    }

                    // If the bucket is not full, add another token
                    if (this.availableTokens <= this.maxTokens)
                    {
                        this.availableTokens += tokensPerTick;
                    }
                }

                Thread.Sleep(25);
            }
        }
    }
}