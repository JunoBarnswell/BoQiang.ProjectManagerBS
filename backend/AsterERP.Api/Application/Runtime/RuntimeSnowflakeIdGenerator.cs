namespace AsterERP.Api.Application.Runtime;

public sealed class RuntimeSnowflakeIdGenerator
{
    private const long EpochMilliseconds = 1704067200000L;
    private const int WorkerId = 13;
    private readonly object syncRoot = new();
    private long lastTimestamp = -1;
    private long sequence;

    public string NextId()
    {
        lock (syncRoot)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (timestamp == lastTimestamp)
            {
                sequence = (sequence + 1) & 4095;
                if (sequence == 0)
                {
                    timestamp = WaitNextMillis(lastTimestamp);
                }
            }
            else
            {
                sequence = 0;
            }

            lastTimestamp = timestamp;
            var id = ((timestamp - EpochMilliseconds) << 22) | ((long)WorkerId << 12) | sequence;
            return id.ToString();
        }
    }

    private static long WaitNextMillis(long previousTimestamp)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        while (timestamp <= previousTimestamp)
        {
            Thread.SpinWait(64);
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        return timestamp;
    }
}
