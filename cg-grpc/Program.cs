

public class gRPCHandler
{
    public async Task Run(CancellationToken token)
    {
        try
        {
            FurnaceSet newSet;
            newSet = await _in.ReadAsync(token);
            while (!token.IsCancellationRequested)
            {
                token.ThrowIfCancellationRequested();

                while (_in.TryRead(out var latest))
                {
                    newSet = latest;
                }
            }
        }
        catch (Exception ex)
        {
            
        }
    }
}