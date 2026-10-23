namespace RotaGame.Core;

public static class LoginCompletion
{
    public static async Task WaitAsync(Task processExit, Func<Task<bool>> connected, CancellationToken ct, bool waitForProcess = false, int pollMs = 500)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            if (!waitForProcess && await connected()) return;
            if (processExit.IsCompleted) { await processExit; if (await connected()) return; throw new InvalidOperationException("A entrada não foi confirmada pelo serviço. Tente entrar novamente."); }
            await Task.WhenAny(processExit, Task.Delay(pollMs, ct));
        }
    }
}
